using System;
using System.Collections.Generic;
using System.Linq;
using DoclingDotNet.Algorithms.Spatial;

namespace DoclingDotNet.Algorithms.ReadingOrder;

public sealed class PageElement : IComparable<PageElement>
{
    public int Cid { get; set; }
    public BoundingBox Bbox { get; set; }
    public string Text { get; set; } = string.Empty;
    public int PageNo { get; set; }
    public string Label { get; set; } = string.Empty;
    public double PageWidth { get; set; }
    public double PageHeight { get; set; }
    
    public int CompareTo(PageElement? other)
    {
        if (other is null) return 1;
        if (PageNo != other.PageNo) return PageNo.CompareTo(other.PageNo);
        if (Bbox.OverlapsHorizontally(other.Bbox))
        {
            return other.Bbox.B.CompareTo(Bbox.B); 
        }
        return Bbox.L.CompareTo(other.Bbox.L);
    }
}

internal sealed class ReadingOrderPredictorState
{
    public Dictionary<int, int> H2IMap { get; } = [];
    public Dictionary<int, int> I2HMap { get; } = [];
    public Dictionary<int, int> L2RMap { get; } = [];
    public Dictionary<int, int> R2LMap { get; } = [];
    public Dictionary<int, List<int>> UpMap { get; } = [];
    public Dictionary<int, List<int>> DnMap { get; } = [];
    public List<int> Heads { get; set; } = [];
}

public sealed class ReadingOrderPredictor
{
    private const double HorizontalDilationThresholdNorm = 0.15;

    public IReadOnlyList<PageElement> PredictReadingOrder(IReadOnlyList<PageElement> pageElements)
    {
        var pageNos = new HashSet<int>();
        foreach (var elem in pageElements) pageNos.Add(elem.PageNo);

        var pageToElems = pageNos.ToDictionary(p => p, _ => new List<PageElement>());
        var pageToHeaders = pageNos.ToDictionary(p => p, _ => new List<PageElement>());
        var pageToFooters = pageNos.ToDictionary(p => p, _ => new List<PageElement>());

        foreach (var elem in pageElements)
        {
            if (elem.Label == "page_header") pageToHeaders[elem.PageNo].Add(elem);
            else if (elem.Label == "page_footer") pageToFooters[elem.PageNo].Add(elem);
            else pageToElems[elem.PageNo].Add(elem);
        }

        foreach (var pageNo in pageNos)
        {
            pageToHeaders[pageNo] = PredictPage(pageToHeaders[pageNo]);
            pageToElems[pageNo] = PredictPage(pageToElems[pageNo]);
            pageToFooters[pageNo] = PredictPage(pageToFooters[pageNo]);
        }

        var sortedElements = new List<PageElement>();
        foreach (var pageNo in pageNos.OrderBy(p => p))
        {
            sortedElements.AddRange(pageToHeaders[pageNo]);
            sortedElements.AddRange(pageToElems[pageNo]);
            sortedElements.AddRange(pageToFooters[pageNo]);
        }

        return sortedElements;
    }

    private List<PageElement> PredictPage(List<PageElement> pageElements)
    {
        if (pageElements.Count == 0) return pageElements;

        var state = new ReadingOrderPredictorState();

        InitH2IMap(pageElements, state);
        InitL2RMap(pageElements, state);
        InitUdMaps(pageElements, state);

        var dilatedPageElements = CloneElements(pageElements);
        dilatedPageElements = DoHorizontalDilation(pageElements, dilatedPageElements, state);

        InitUdMaps(dilatedPageElements, state);
        FindHeads(dilatedPageElements, state);
        SortUdMaps(dilatedPageElements, state);

        var order = FindOrder(dilatedPageElements, state);

        var sortedElements = new List<PageElement>(order.Count);
        foreach (var ind in order)
        {
            sortedElements.Add(pageElements[ind]);
        }

        return sortedElements;
    }

    private void InitH2IMap(List<PageElement> pageElems, ReadingOrderPredictorState state)
    {
        for (int i = 0; i < pageElems.Count; i++)
        {
            state.H2IMap[pageElems[i].Cid] = i;
            state.I2HMap[i] = pageElems[i].Cid;
        }
    }

    private void InitL2RMap(List<PageElement> pageElems, ReadingOrderPredictorState state)
    {
        // Currently empty / disabled by logic in Python
    }

    private void InitUdMaps(List<PageElement> pageElems, ReadingOrderPredictorState state)
    {
        state.UpMap.Clear();
        state.DnMap.Clear();

        for (int i = 0; i < pageElems.Count; i++)
        {
            state.UpMap[i] = new List<int>();
            state.DnMap[i] = new List<int>();
        }

        var spatialIdx = new SpatialIndex<PageElement>();
        for (int i = 0; i < pageElems.Count; i++)
        {
            spatialIdx.Insert(i, pageElems[i].Bbox, pageElems[i]);
        }

        for (int j = 0; j < pageElems.Count; j++)
        {
            if (state.R2LMap.TryGetValue(j, out var leftIdx))
            {
                state.DnMap[leftIdx].Add(j);
                state.UpMap[j].Add(leftIdx);
                continue;
            }

            var pelemJ = pageElems[j];
            var queryBbox = new BoundingBox(pelemJ.Bbox.L - 0.1, pelemJ.Bbox.T, pelemJ.Bbox.R + 0.1, double.PositiveInfinity);

            foreach (var (i, _) in spatialIdx.Intersection(queryBbox))
            {
                if (i == j) continue;

                var pelemI = pageElems[i];

                if (!pelemI.Bbox.IsStrictlyAbove(pelemJ.Bbox) || !pelemI.Bbox.OverlapsHorizontally(pelemJ.Bbox))
                    continue;

                if (!HasSequenceInterruption(spatialIdx, pageElems, i, j, pelemI, pelemJ))
                {
                    var mappedI = i;
                    while (state.L2RMap.TryGetValue(mappedI, out var nextI))
                    {
                        mappedI = nextI;
                    }

                    state.DnMap[mappedI].Add(j);
                    state.UpMap[j].Add(mappedI);
                }
            }
        }
    }

    private bool HasSequenceInterruption(SpatialIndex<PageElement> spatialIdx, List<PageElement> pageElems, int i, int j, PageElement pelemI, PageElement pelemJ)
    {
        var xMin = Math.Min(pelemI.Bbox.L, pelemJ.Bbox.L) - 1.0;
        var xMax = Math.Max(pelemI.Bbox.R, pelemJ.Bbox.R) + 1.0;
        var yMin = pelemJ.Bbox.T;
        var yMax = pelemI.Bbox.B;

        var query = new BoundingBox(xMin, yMin, xMax, yMax);

        foreach (var (w, pelemW) in spatialIdx.Intersection(query))
        {
            if (w == i || w == j) continue;

            if ((pelemI.Bbox.OverlapsHorizontally(pelemW.Bbox) || pelemJ.Bbox.OverlapsHorizontally(pelemW.Bbox)) &&
                pelemI.Bbox.IsStrictlyAbove(pelemW.Bbox) &&
                pelemW.Bbox.IsStrictlyAbove(pelemJ.Bbox))
            {
                return true;
            }
        }

        return false;
    }

    private List<PageElement> DoHorizontalDilation(List<PageElement> pageElems, List<PageElement> dilatedPageElems, ReadingOrderPredictorState state)
    {
        double th = 0.0;
        if (pageElems.Count > 0)
        {
            th = HorizontalDilationThresholdNorm * pageElems[0].PageWidth;
        }

        for (int i = 0; i < dilatedPageElems.Count; i++)
        {
            var pelemI = dilatedPageElems[i];
            var x0 = pelemI.Bbox.L;
            var y0 = pelemI.Bbox.B;
            var x1 = pelemI.Bbox.R;
            var y1 = pelemI.Bbox.T;

            if (state.UpMap[i].Count > 0)
            {
                var pelemUp = pageElems[state.UpMap[i][0]];
                var x0Dil = Math.Min(x0, pelemUp.Bbox.L);
                var x1Dil = Math.Max(x1, pelemUp.Bbox.R);
                if ((x0 - x0Dil) <= th && (x1Dil - x1) <= th)
                {
                    x0 = x0Dil;
                    x1 = x1Dil;
                }
            }

            if (state.DnMap[i].Count > 0)
            {
                var pelemDn = pageElems[state.DnMap[i][0]];
                var x0Dil = Math.Min(x0, pelemDn.Bbox.L);
                var x1Dil = Math.Max(x1, pelemDn.Bbox.R);
                if ((x0 - x0Dil) <= th && (x1Dil - x1) <= th)
                {
                    x0 = x0Dil;
                    x1 = x1Dil;
                }
            }

            pelemI.Bbox = new BoundingBox(x0, y0, x1, y1);

            var overlapsWithRest = false;
            for (int j = 0; j < pageElems.Count; j++)
            {
                if (i == j) continue;
                if (!overlapsWithRest)
                {
                    overlapsWithRest = pageElems[j].Bbox.Overlaps(pelemI.Bbox);
                }
            }

            if (!overlapsWithRest)
            {
                dilatedPageElems[i].Bbox = new BoundingBox(x0, y0, x1, y1);
            }
        }

        return dilatedPageElems;
    }

    private void FindHeads(List<PageElement> pageElems, ReadingOrderPredictorState state)
    {
        var headPageElems = new List<PageElement>();
        foreach (var kvp in state.UpMap)
        {
            if (kvp.Value.Count == 0)
            {
                headPageElems.Add(pageElems[kvp.Key]);
            }
        }

        headPageElems.Sort();
        state.Heads.Clear();
        foreach (var item in headPageElems)
        {
            state.Heads.Add(state.H2IMap[item.Cid]);
        }
    }

    private void SortUdMaps(List<PageElement> provs, ReadingOrderPredictorState state)
    {
        var keys = state.DnMap.Keys.ToList();
        foreach (var indI in keys)
        {
            var vals = state.DnMap[indI];
            var childProvs = new List<PageElement>();
            foreach (var indJ in vals)
            {
                childProvs.Add(provs[indJ]);
            }

            childProvs.Sort();
            state.DnMap[indI].Clear();
            foreach (var child in childProvs)
            {
                state.DnMap[indI].Add(state.H2IMap[child.Cid]);
            }
        }
    }

    private List<int> FindOrder(List<PageElement> provs, ReadingOrderPredictorState state)
    {
        var order = new List<int>();
        var visited = new bool[provs.Count];

        foreach (var j in state.Heads)
        {
            if (!visited[j])
            {
                order.Add(j);
                visited[j] = true;
                DepthFirstSearchDownwards(j, order, visited, state);
            }
        }

        return order;
    }

    private int DepthFirstSearchUpwards(int j, bool[] visited, ReadingOrderPredictorState state)
    {
        var k = j;
        while (true)
        {
            var inds = state.UpMap[k];
            var foundNotVisited = false;
            foreach (var ind in inds)
            {
                if (!visited[ind])
                {
                    k = ind;
                    foundNotVisited = true;
                    break;
                }
            }

            if (!foundNotVisited) return k;
        }
    }

    private void DepthFirstSearchDownwards(int j, List<int> order, bool[] visited, ReadingOrderPredictorState state)
    {
        var stack = new Stack<(List<int> Inds, int Offset)>();
        stack.Push((state.DnMap[j], 0));

        while (stack.Count > 0)
        {
            var tuple = stack.Pop();
            var inds = tuple.Inds;
            var offset = tuple.Offset;
            var foundNonVisited = false;

            if (offset < inds.Count)
            {
                for (int m = offset; m < inds.Count; m++)
                {
                    var i = inds[m];
                    var k = DepthFirstSearchUpwards(i, visited, state);

                    if (!visited[k])
                    {
                        order.Add(k);
                        visited[k] = true;
                        stack.Push((inds, m + 1));
                        stack.Push((state.DnMap[k], 0));
                        foundNonVisited = true;
                        break;
                    }
                }
            }

            if (!foundNonVisited)
            {
                // Equivalent to pop, which we already did
            }
        }
    }

    private List<PageElement> CloneElements(List<PageElement> source)
    {
        return source.Select(e => new PageElement
        {
            Cid = e.Cid,
            Bbox = e.Bbox,
            Text = e.Text,
            PageNo = e.PageNo,
            Label = e.Label,
            PageWidth = e.PageWidth,
            PageHeight = e.PageHeight
        }).ToList();
    }
}
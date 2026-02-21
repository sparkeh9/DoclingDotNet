using System;
using System.Collections.Generic;
using System.Linq;
using DoclingDotNet.Algorithms.Spatial;
using DoclingDotNet.Models;

namespace DoclingDotNet.Algorithms.Layout;

public sealed class LayoutCluster
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public BoundingBox Bbox { get; set; }
    public double Confidence { get; set; }
    public List<PdfTextCellDto> Cells { get; set; } = [];
    public List<LayoutCluster> Children { get; set; } = [];
}

public sealed class LayoutPostprocessorOptions
{
    public bool SkipCellAssignment { get; set; }
    public bool KeepEmptyClusters { get; set; }
    public bool CreateOrphanClusters { get; set; } = true;
}

public sealed class LayoutPostprocessor
{
    private static readonly HashSet<string> WrapperTypes =
    [
        "form",
        "key_value_region",
        "table",
        "document_index"
    ];

    private static readonly HashSet<string> SpecialTypes =
    [
        "form",
        "key_value_region",
        "table",
        "document_index",
        "picture"
    ];

    private static readonly Dictionary<string, double> ConfidenceThresholds = new(StringComparer.OrdinalIgnoreCase)
    {
        { "caption", 0.5 },
        { "footnote", 0.5 },
        { "formula", 0.5 },
        { "list_item", 0.5 },
        { "page_footer", 0.5 },
        { "page_header", 0.5 },
        { "picture", 0.5 },
        { "section_header", 0.45 },
        { "table", 0.5 },
        { "text", 0.5 },
        { "title", 0.45 },
        { "code", 0.45 },
        { "checkbox_selected", 0.45 },
        { "checkbox_unselected", 0.45 },
        { "form", 0.45 },
        { "key_value_region", 0.45 },
        { "document_index", 0.45 }
    };

    private static readonly Dictionary<string, string> LabelRemapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "title", "section_header" }
    };

    private readonly PdfPageGeometryDto _pageSize;
    private readonly List<PdfTextCellDto> _cells;
    private readonly List<LayoutCluster> _allClusters;
    private readonly LayoutPostprocessorOptions _options;

    private List<LayoutCluster> _regularClusters;
    private List<LayoutCluster> _specialClusters;

    private readonly SpatialClusterIndex _regularIndex;
    private readonly SpatialClusterIndex _pictureIndex;
    private readonly SpatialClusterIndex _wrapperIndex;

    public LayoutPostprocessor(
        PdfPageGeometryDto pageSize,
        IEnumerable<PdfTextCellDto> cells,
        IEnumerable<LayoutCluster> clusters,
        LayoutPostprocessorOptions options)
    {
        _pageSize = pageSize;
        _cells = cells.ToList();
        _allClusters = clusters.ToList();
        _options = options;

        _regularClusters = _allClusters.Where(c => !SpecialTypes.Contains(c.Label)).ToList();
        _specialClusters = _allClusters.Where(c => SpecialTypes.Contains(c.Label)).ToList();

        _regularIndex = new SpatialClusterIndex(_regularClusters);
        _pictureIndex = new SpatialClusterIndex(_specialClusters.Where(c => c.Label == "picture"));
        _wrapperIndex = new SpatialClusterIndex(_specialClusters.Where(c => WrapperTypes.Contains(c.Label)));
    }

    public (IReadOnlyList<LayoutCluster> Clusters, IReadOnlyList<PdfTextCellDto> Cells) Postprocess()
    {
        _regularClusters = ProcessRegularClusters();
        _specialClusters = ProcessSpecialClusters();

        var containedIds = new HashSet<int>();
        foreach (var wrapper in _specialClusters)
        {
            if (SpecialTypes.Contains(wrapper.Label))
            {
                foreach (var child in wrapper.Children)
                {
                    containedIds.Add(child.Id);
                }
            }
        }

        _regularClusters.RemoveAll(c => containedIds.Contains(c.Id));

        var finalClusters = SortClusters(_regularClusters.Concat(_specialClusters).ToList(), "id");

        if (!_options.SkipCellAssignment)
        {
            foreach (var cluster in finalClusters)
            {
                cluster.Cells = SortCells(cluster.Cells);
                foreach (var child in cluster.Children)
                {
                    child.Cells = SortCells(child.Cells);
                }
            }
        }

        return (finalClusters, _cells);
    }

    private List<LayoutCluster> ProcessRegularClusters()
    {
        var clusters = _regularClusters.Where(c =>
        {
            if (!ConfidenceThresholds.TryGetValue(c.Label, out var threshold)) threshold = 0.0;
            return c.Confidence >= threshold;
        }).ToList();

        foreach (var cluster in clusters)
        {
            if (LabelRemapping.TryGetValue(cluster.Label, out var mapped))
            {
                cluster.Label = mapped;
            }
        }

        if (!_options.SkipCellAssignment)
        {
            clusters = AssignCellsToClusters(clusters);

            if (!_options.KeepEmptyClusters)
            {
                clusters = clusters.Where(c => c.Cells.Count > 0 || c.Label == "formula").ToList();
            }

            var unassigned = FindUnassignedCells(clusters);
            if (unassigned.Count > 0 && _options.CreateOrphanClusters)
            {
                int nextId = _allClusters.Count > 0 ? _allClusters.Max(c => c.Id) + 1 : 1;
                foreach (var cell in unassigned)
                {
                    clusters.Add(new LayoutCluster
                    {
                        Id = nextId++,
                        Label = "text",
                        Bbox = cell.Rect.ToBoundingBox(),
                        Confidence = cell.Confidence,
                        Cells = [cell]
                    });
                }
            }
        }

        var prevCount = clusters.Count + 1;
        for (int i = 0; i < 3; i++)
        {
            if (prevCount == clusters.Count) break;
            prevCount = clusters.Count;
            clusters = AdjustClusterBboxes(clusters);
            clusters = RemoveOverlappingClusters(clusters, "regular");
        }

        return clusters;
    }

    private List<LayoutCluster> ProcessSpecialClusters()
    {
        var specialClusters = _specialClusters.Where(c =>
        {
            if (!ConfidenceThresholds.TryGetValue(c.Label, out var threshold)) threshold = 0.0;
            return c.Confidence >= threshold;
        }).ToList();

        specialClusters = HandleCrossTypeOverlaps(specialClusters);

        var bounds = _pageSize.ToBoundingBox();
        var pageArea = bounds.Width * bounds.Height;
        if (pageArea > 0)
        {
            specialClusters.RemoveAll(c => c.Label == "picture" && c.Bbox.Area / pageArea > 0.90);
        }

        foreach (var special in specialClusters)
        {
            var contained = new List<LayoutCluster>();
            foreach (var cluster in _regularClusters)
            {
                if (cluster.Bbox.IntersectionOverSelf(special.Bbox) > 0.8)
                {
                    contained.Add(cluster);
                }
            }

            if (contained.Count > 0)
            {
                contained = SortClusters(contained, "id");
                special.Children = contained;

                if (special.Label == "form" || special.Label == "key_value_region")
                {
                    var l = contained.Min(c => c.Bbox.L);
                    var t = contained.Max(c => c.Bbox.T);
                    var r = contained.Max(c => c.Bbox.R);
                    var b = contained.Min(c => c.Bbox.B);
                    special.Bbox = new BoundingBox(l, b, r, t);
                }

                if (!_options.SkipCellAssignment)
                {
                    var allCells = new List<PdfTextCellDto>();
                    foreach (var child in contained) allCells.AddRange(child.Cells);
                    special.Cells = SortCells(DeduplicateCells(allCells));
                }
                else
                {
                    special.Cells = [];
                }
            }
        }

        var pictureClusters = specialClusters.Where(c => c.Label == "picture").ToList();
        pictureClusters = RemoveOverlappingClusters(pictureClusters, "picture");

        var wrapperClusters = specialClusters.Where(c => WrapperTypes.Contains(c.Label)).ToList();
        wrapperClusters = RemoveOverlappingClusters(wrapperClusters, "wrapper");

        return pictureClusters.Concat(wrapperClusters).ToList();
    }

    private List<LayoutCluster> HandleCrossTypeOverlaps(List<LayoutCluster> specialClusters)
    {
        var toRemove = new HashSet<int>();

        foreach (var wrapper in specialClusters)
        {
            if (wrapper.Label != "key_value_region") continue;

            foreach (var regular in _regularClusters)
            {
                if (regular.Label == "table")
                {
                    var overlap = wrapper.Bbox.IntersectionOverSelf(regular.Bbox);
                    var confDiff = wrapper.Confidence - regular.Confidence;

                    if (overlap > 0.9 && confDiff < 0.1)
                    {
                        toRemove.Add(wrapper.Id);
                        break;
                    }
                }
            }
        }

        return specialClusters.Where(c => !toRemove.Contains(c.Id)).ToList();
    }

    private List<LayoutCluster> AssignCellsToClusters(List<LayoutCluster> clusters, double minOverlap = 0.2)
    {
        foreach (var cluster in clusters) cluster.Cells.Clear();

        foreach (var cell in _cells)
        {
            if (string.IsNullOrWhiteSpace(cell.Text)) continue;

            var bestOverlap = minOverlap;
            LayoutCluster? bestCluster = null;
            var cellBox = cell.Rect.ToBoundingBox();

            foreach (var cluster in clusters)
            {
                if (cellBox.Area <= 0) continue;

                var overlap = cellBox.IntersectionOverSelf(cluster.Bbox);
                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    bestCluster = cluster;
                }
            }

            bestCluster?.Cells.Add(cell);
        }

        foreach (var cluster in clusters)
        {
            cluster.Cells = DeduplicateCells(cluster.Cells);
        }

        return clusters;
    }

    private List<PdfTextCellDto> FindUnassignedCells(List<LayoutCluster> clusters)
    {
        var assigned = new HashSet<long>();
        foreach (var cluster in clusters)
        {
            foreach (var cell in cluster.Cells) assigned.Add(cell.Index);
        }

        return _cells.Where(c => !assigned.Contains(c.Index) && !string.IsNullOrWhiteSpace(c.Text)).ToList();
    }

    private List<LayoutCluster> AdjustClusterBboxes(List<LayoutCluster> clusters)
    {
        foreach (var cluster in clusters)
        {
            if (cluster.Cells.Count == 0) continue;

            var l = cluster.Cells.Min(c => c.Rect.ToBoundingBox().L);
            var t = cluster.Cells.Max(c => c.Rect.ToBoundingBox().T);
            var r = cluster.Cells.Max(c => c.Rect.ToBoundingBox().R);
            var b = cluster.Cells.Min(c => c.Rect.ToBoundingBox().B);

            var cellsBox = new BoundingBox(l, b, r, t);

            if (cluster.Label == "table")
            {
                cluster.Bbox = new BoundingBox(
                    Math.Min(cluster.Bbox.L, cellsBox.L),
                    Math.Min(cluster.Bbox.B, cellsBox.B),
                    Math.Max(cluster.Bbox.R, cellsBox.R),
                    Math.Max(cluster.Bbox.T, cellsBox.T));
            }
            else
            {
                cluster.Bbox = cellsBox;
            }
        }
        return clusters;
    }

    private List<LayoutCluster> RemoveOverlappingClusters(List<LayoutCluster> clusters, string clusterType, double overlapThreshold = 0.8, double containmentThreshold = 0.8)
    {
        if (clusters.Count == 0) return [];

        var spatialIndex = clusterType == "regular" ? _regularIndex : (clusterType == "picture" ? _pictureIndex : _wrapperIndex);
        var validClusters = clusters.ToDictionary(c => c.Id);
        var uf = new UnionFind(validClusters.Keys);

        var areaThreshold = clusterType == "regular" ? 1.3 : 2.0;
        var confThreshold = clusterType == "regular" ? 0.05 : (clusterType == "picture" ? 0.3 : 0.2);

        foreach (var cluster in clusters)
        {
            var candidates = spatialIndex.FindCandidates(cluster.Bbox);
            candidates.IntersectWith(validClusters.Keys);
            candidates.Remove(cluster.Id);

            foreach (var otherId in candidates)
            {
                if (spatialIndex.CheckOverlap(cluster.Bbox, validClusters[otherId].Bbox, overlapThreshold, containmentThreshold))
                {
                    uf.Union(cluster.Id, otherId);
                }
            }
        }

        var result = new List<LayoutCluster>();
        foreach (var group in uf.GetGroups().Values)
        {
            if (group.Count == 1)
            {
                result.Add(validClusters[group[0]]);
                continue;
            }

            var groupClusters = group.Select(id => validClusters[id]).ToList();
            var best = SelectBestClusterFromGroup(groupClusters, areaThreshold, confThreshold);

            foreach (var cluster in groupClusters)
            {
                if (cluster != best) best.Cells.AddRange(cluster.Cells);
            }

            best.Cells = SortCells(DeduplicateCells(best.Cells));
            result.Add(best);
        }

        return result;
    }

    private LayoutCluster SelectBestClusterFromGroup(List<LayoutCluster> groupClusters, double areaThreshold, double confThreshold)
    {
        LayoutCluster? currentBest = null;

        foreach (var candidate in groupClusters)
        {
            var shouldSelect = true;

            foreach (var other in groupClusters)
            {
                if (other == candidate) continue;

                if (!ShouldPreferCluster(candidate, other, areaThreshold, confThreshold))
                {
                    shouldSelect = false;
                    break;
                }
            }

            if (shouldSelect)
            {
                if (currentBest == null)
                {
                    currentBest = candidate;
                }
                else
                {
                    if (candidate.Bbox.Area > currentBest.Bbox.Area &&
                        currentBest.Confidence - candidate.Confidence <= confThreshold)
                    {
                        currentBest = candidate;
                    }
                }
            }
        }

        return currentBest ?? groupClusters[0];
    }

    private bool ShouldPreferCluster(LayoutCluster candidate, LayoutCluster other, double areaThreshold, double confThreshold)
    {
        if (candidate.Label == "list_item" && other.Label == "text")
        {
            var ratio = candidate.Bbox.Area / other.Bbox.Area;
            if (Math.Abs(1 - ratio) < 0.2) return true;
        }

        if (candidate.Label == "code")
        {
            if (other.Bbox.IntersectionOverSelf(candidate.Bbox) > 0.8) return true;
        }

        var areaRatio = candidate.Bbox.Area / other.Bbox.Area;
        var confDiff = other.Confidence - candidate.Confidence;

        if (areaRatio <= areaThreshold && confDiff > confThreshold)
        {
            return false;
        }

        return true;
    }

    private List<PdfTextCellDto> DeduplicateCells(List<PdfTextCellDto> cells)
    {
        var seenIds = new HashSet<long>();
        var unique = new List<PdfTextCellDto>();
        foreach (var cell in cells)
        {
            if (seenIds.Add(cell.Index))
            {
                unique.Add(cell);
            }
        }
        return unique;
    }

    private List<PdfTextCellDto> SortCells(List<PdfTextCellDto> cells)
    {
        return cells.OrderBy(c => c.Index).ToList();
    }

    private List<LayoutCluster> SortClusters(List<LayoutCluster> clusters, string mode)
    {
        if (mode == "id")
        {
            return clusters.OrderBy(c => c.Cells.Count > 0 ? c.Cells.Min(cell => cell.Index) : long.MaxValue)
                           .ThenByDescending(c => c.Bbox.T)
                           .ThenBy(c => c.Bbox.L)
                           .ToList();
        }
        if (mode == "tblr")
        {
            return clusters.OrderByDescending(c => c.Bbox.T).ThenBy(c => c.Bbox.L).ToList();
        }
        if (mode == "lrtb")
        {
            return clusters.OrderBy(c => c.Bbox.L).ThenByDescending(c => c.Bbox.T).ToList();
        }
        return clusters;
    }

    private sealed class SpatialClusterIndex
    {
        private readonly SpatialIndex<LayoutCluster> _spatialIndex = new();
        private readonly IntervalTree _xIntervals = new();
        private readonly IntervalTree _yIntervals = new();
        private readonly Dictionary<int, LayoutCluster> _clustersById = new();

        public SpatialClusterIndex(IEnumerable<LayoutCluster> clusters)
        {
            foreach (var cluster in clusters)
            {
                AddCluster(cluster);
            }
        }

        public void AddCluster(LayoutCluster cluster)
        {
            _spatialIndex.Insert(cluster.Id, cluster.Bbox, cluster);
            _xIntervals.Insert(cluster.Bbox.L, cluster.Bbox.R, cluster.Id);
            _yIntervals.Insert(cluster.Bbox.B, cluster.Bbox.T, cluster.Id);
            _clustersById[cluster.Id] = cluster;
        }

        public HashSet<int> FindCandidates(BoundingBox bbox)
        {
            var result = new HashSet<int>();
            foreach (var item in _spatialIndex.Intersection(bbox))
            {
                result.Add(item.Id);
            }

            foreach (var id in _xIntervals.FindContaining(bbox.L)) result.Add(id);
            foreach (var id in _xIntervals.FindContaining(bbox.R)) result.Add(id);
            foreach (var id in _yIntervals.FindContaining(bbox.B)) result.Add(id);
            foreach (var id in _yIntervals.FindContaining(bbox.T)) result.Add(id);

            return result;
        }

        public bool CheckOverlap(BoundingBox bbox1, BoundingBox bbox2, double overlapThreshold, double containmentThreshold)
        {
            if (bbox1.Area <= 0 || bbox2.Area <= 0) return false;

            var iou = bbox1.IntersectionOverUnion(bbox2);
            var containment1 = bbox1.IntersectionOverSelf(bbox2);
            var containment2 = bbox2.IntersectionOverSelf(bbox1);

            return iou > overlapThreshold || containment1 > containmentThreshold || containment2 > containmentThreshold;
        }
    }
}
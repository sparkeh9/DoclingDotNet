using System.Collections.Generic;

namespace DoclingDotNet.Algorithms.Spatial;

public sealed class UnionFind
{
    private readonly Dictionary<int, int> _parent;
    private readonly Dictionary<int, int> _rank;

    public UnionFind(IEnumerable<int> elements)
    {
        _parent = new Dictionary<int, int>();
        _rank = new Dictionary<int, int>();
        foreach (var elem in elements)
        {
            _parent[elem] = elem;
            _rank[elem] = 0;
        }
    }

    public int Find(int x)
    {
        if (_parent[x] != x)
        {
            _parent[x] = Find(_parent[x]);
        }
        return _parent[x];
    }

    public void Union(int x, int y)
    {
        var rootX = Find(x);
        var rootY = Find(y);

        if (rootX == rootY) return;

        if (_rank[rootX] > _rank[rootY])
        {
            _parent[rootY] = rootX;
        }
        else if (_rank[rootX] < _rank[rootY])
        {
            _parent[rootX] = rootY;
        }
        else
        {
            _parent[rootY] = rootX;
            _rank[rootX]++;
        }
    }

    public Dictionary<int, List<int>> GetGroups()
    {
        var groups = new Dictionary<int, List<int>>();
        foreach (var elem in _parent.Keys)
        {
            var root = Find(elem);
            if (!groups.TryGetValue(root, out var list))
            {
                list = new List<int>();
                groups[root] = list;
            }
            list.Add(elem);
        }
        return groups;
    }
}
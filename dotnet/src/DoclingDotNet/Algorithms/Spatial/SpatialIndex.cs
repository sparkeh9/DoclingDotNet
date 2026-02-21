using System.Collections.Generic;

namespace DoclingDotNet.Algorithms.Spatial;

public sealed class SpatialIndex<T>
{
    private readonly List<(int Id, BoundingBox Bounds, T Item)> _items = [];

    public void Insert(int id, BoundingBox bounds, T item)
    {
        _items.Add((id, bounds, item));
    }

    public void Remove(int id)
    {
        var index = _items.FindIndex(i => i.Id == id);
        if (index >= 0)
        {
            _items.RemoveAt(index);
        }
    }

    public IEnumerable<(int Id, T Item)> Intersection(BoundingBox query)
    {
        foreach (var item in _items)
        {
            if (item.Bounds.Overlaps(query))
            {
                yield return (item.Id, item.Item);
            }
        }
    }
}
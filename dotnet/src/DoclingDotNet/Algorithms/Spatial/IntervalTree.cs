using System;
using System.Collections.Generic;

namespace DoclingDotNet.Algorithms.Spatial;

public readonly struct Interval(double minVal, double maxVal, int id) : IComparable<Interval>
{
    public readonly double MinVal = minVal;
    public readonly double MaxVal = maxVal;
    public readonly int Id = id;

    public int CompareTo(Interval other)
    {
        return MinVal.CompareTo(other.MinVal);
    }
}

public sealed class IntervalTree
{
    private readonly List<Interval> _intervals = [];

    public void Insert(double minVal, double maxVal, int id)
    {
        var interval = new Interval(minVal, maxVal, id);
        var index = _intervals.BinarySearch(interval);
        if (index < 0) index = ~index;
        _intervals.Insert(index, interval);
    }

    public HashSet<int> FindContaining(double point)
    {
        var result = new HashSet<int>();
        var searchInterval = new Interval(point, point, 0);
        var pos = _intervals.BinarySearch(searchInterval);
        if (pos < 0) pos = ~pos;

        for (int i = pos - 1; i >= 0; i--)
        {
            var interval = _intervals[i];
            if (interval.MinVal <= point && point <= interval.MaxVal)
            {
                result.Add(interval.Id);
            }
            else if (interval.MinVal > point)
            {
                // Unreachable due to sorted property
            }
        }

        for (int i = pos; i < _intervals.Count; i++)
        {
            var interval = _intervals[i];
            if (interval.MinVal <= point && point <= interval.MaxVal)
            {
                result.Add(interval.Id);
            }
            else if (interval.MinVal > point)
            {
                break;
            }
        }

        return result;
    }
}
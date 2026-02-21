using System;
using System.Diagnostics.CodeAnalysis;

namespace DoclingDotNet.Algorithms.Spatial;

public readonly struct BoundingBox : IEquatable<BoundingBox>
{
    public readonly double L;
    public readonly double B;
    public readonly double R;
    public readonly double T;

    public BoundingBox(double l, double b, double r, double t)
    {
        L = l;
        B = b;
        R = r;
        T = t;
    }

    public double Width => Math.Max(0, R - L);
    public double Height => Math.Max(0, T - B);
    public double Area => Width * Height;

    public BoundingBox Intersect(BoundingBox other)
    {
        var l = Math.Max(L, other.L);
        var b = Math.Max(B, other.B);
        var r = Math.Min(R, other.R);
        var t = Math.Min(T, other.T);

        if (l < r && b < t)
        {
            return new BoundingBox(l, b, r, t);
        }

        return new BoundingBox(0, 0, 0, 0);
    }

    public double IntersectionArea(BoundingBox other) => Intersect(other).Area;

    public double IntersectionOverUnion(BoundingBox other)
    {
        var intersectionArea = IntersectionArea(other);
        if (intersectionArea <= 0) return 0;

        var unionArea = Area + other.Area - intersectionArea;
        if (unionArea <= 0) return 0;

        return intersectionArea / unionArea;
    }

    public double IntersectionOverSelf(BoundingBox other)
    {
        var area = Area;
        if (area <= 0) return 0;
        return IntersectionArea(other) / area;
    }

    public bool Overlaps(BoundingBox other)
    {
        return L < other.R && R > other.L && B < other.T && T > other.B;
    }

    public bool OverlapsHorizontally(BoundingBox other)
    {
        return L < other.R && R > other.L;
    }

    public bool OverlapsVertically(BoundingBox other)
    {
        return B < other.T && T > other.B;
    }

    public bool IsStrictlyLeftOf(BoundingBox other)
    {
        return R <= other.L;
    }

    public bool IsStrictlyAbove(BoundingBox other)
    {
        return B >= other.T;
    }

    public bool Equals(BoundingBox other)
    {
        return L.Equals(other.L) && B.Equals(other.B) && R.Equals(other.R) && T.Equals(other.T);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is BoundingBox other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(L, B, R, T);
    }
}
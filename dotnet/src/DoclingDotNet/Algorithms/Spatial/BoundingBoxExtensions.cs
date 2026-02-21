using System;
using DoclingDotNet.Models;

namespace DoclingDotNet.Algorithms.Spatial;

public static class BoundingBoxExtensions
{
    public static BoundingBox ToBoundingBox(this BoundingRectangleDto dto)
    {
        var l = Math.Min(Math.Min(dto.RX0, dto.RX1), Math.Min(dto.RX2, dto.RX3));
        var b = Math.Min(Math.Min(dto.RY0, dto.RY1), Math.Min(dto.RY2, dto.RY3));
        var r = Math.Max(Math.Max(dto.RX0, dto.RX1), Math.Max(dto.RX2, dto.RX3));
        var t = Math.Max(Math.Max(dto.RY0, dto.RY1), Math.Max(dto.RY2, dto.RY3));
        return new BoundingBox(l, b, r, t);
    }

    public static BoundingBox ToBoundingBox(this PdfPageGeometryDto dto)
    {
        return dto.Rect.ToBoundingBox();
    }
}
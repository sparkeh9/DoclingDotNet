using DoclingDotNet.Models;

namespace DoclingDotNet.Projection;

public static class NativeDecodedPageProjector
{
    public static SegmentedPdfPageDto ToSegmentedPdfPage(NativeDecodedPageDto nativePage)
    {
        var payload = nativePage.Original ?? nativePage.Sanitized;
        if (payload is null)
        {
            throw new InvalidOperationException(
                "decode_page_json payload did not contain either 'original' or 'sanitized' sections.");
        }

        var charCells = payload.Cells ?? [];
        var bitmapResources = payload.Images ?? [];
        var shapes = payload.Shapes ?? [];
        var widgets = payload.Widgets ?? [];
        var hyperlinks = payload.Hyperlinks ?? [];

        // Note: current C ABI payload does not include word/line cells directly.
        return new SegmentedPdfPageDto
        {
            Dimension = payload.Dimension ?? new PdfPageGeometryDto(),
            BitmapResources = bitmapResources,
            CharCells = charCells,
            WordCells = [],
            TextlineCells = [],
            HasChars = charCells.Count > 0,
            HasWords = false,
            HasLines = false,
            Widgets = widgets,
            Hyperlinks = hyperlinks,
            Lines = [],
            Shapes = shapes
        };
    }
}

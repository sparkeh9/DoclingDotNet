using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DoclingDotNet.Models;

namespace DoclingDotNet.Backends;

public sealed class MarkdownDocumentBackend : IDocumentBackend
{
    public string Name => "md_backend";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".md", ".markdown", ".adoc", ".asciidoc"];
    public DocumentBackendCapabilities Capabilities { get; } = new() { SupportsPagination = false };

    public async Task<IReadOnlyList<SegmentedPdfPageDto>> ConvertAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pageDto = new SegmentedPdfPageDto
        {
            Dimension = new PdfPageGeometryDto
            {
                Angle = 0,
                Rect = new BoundingRectangleDto { RX0 = 0, RY0 = 0, RX1 = 1000, RY1 = 0, RX2 = 1000, RY2 = 1000, RX3 = 0, RY3 = 1000, CoordOrigin = "BOTTOMLEFT" },
                BoundaryType = "crop_box"
            },
            HasChars = true,
            HasLines = true,
            HasWords = true
        };

        var textlineCells = new List<PdfTextCellDto>();
        long cellIndex = 0;
        double currentY = 1000.0;

        var lines = await ReadLinesAsync(stream, cancellationToken).ConfigureAwait(false);

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = line.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            // Optional: minimal markdown stripping (e.g., headers #)
            var fontName = "BodyFont";
            if (text.StartsWith("#"))
            {
                text = text.TrimStart('#').Trim();
                fontName = "HeadingFont";
            }
            if (text.StartsWith("* ") || text.StartsWith("- "))
            {
                text = text[2..].Trim();
            }

            textlineCells.Add(new PdfTextCellDto
            {
                Index = cellIndex++,
                Text = text,
                Orig = text,
                TextDirection = "left_to_right",
                Confidence = 1.0,
                Rect = new BoundingRectangleDto
                {
                    RX0 = 10, RY0 = currentY - 12,
                    RX1 = 900, RY1 = currentY - 12,
                    RX2 = 900, RY2 = currentY,
                    RX3 = 10, RY3 = currentY,
                    CoordOrigin = "BOTTOMLEFT"
                },
                FontName = fontName
            });
            currentY -= 14.0;
        }

        pageDto.TextlineCells = textlineCells;
        return [pageDto];
    }
    
    private static async Task<List<string>> ReadLinesAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        using var reader = new StreamReader(stream, leaveOpen: true);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            lines.Add(line);
        }
        return lines;
    }
}
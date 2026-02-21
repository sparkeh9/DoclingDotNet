using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using DoclingDotNet.Models;

namespace DoclingDotNet.Backends;

public sealed class LatexDocumentBackend : IDocumentBackend
{
    public string Name => "latex_backend";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".tex", ".latex"];
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

        var text = await new StreamReader(stream).ReadToEndAsync(cancellationToken);
        
        // Very basic LaTeX stripping for stub implementation
        var strippedText = Regex.Replace(text, @"\[a-zA-Z]+\{.*?\}", "");
        strippedText = Regex.Replace(strippedText, @"\[a-zA-Z]+", "");
        
        var lines = strippedText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cleanLine = line.Trim();
            if (!string.IsNullOrWhiteSpace(cleanLine))
            {
                textlineCells.Add(new PdfTextCellDto
                {
                    Index = cellIndex++,
                    Text = cleanLine,
                    Orig = cleanLine,
                    TextDirection = "left_to_right",
                    Confidence = 1.0,
                    Rect = new BoundingRectangleDto { RX0 = 10, RY0 = currentY - 12, RX1 = 900, RY1 = currentY - 12, RX2 = 900, RY2 = currentY, RX3 = 10, RY3 = currentY, CoordOrigin = "BOTTOMLEFT" }
                });
                currentY -= 14.0;
            }
        }

        pageDto.TextlineCells = textlineCells;
        return [pageDto];
    }
}
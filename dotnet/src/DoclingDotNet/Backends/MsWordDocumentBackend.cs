using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DoclingDotNet.Models;

namespace DoclingDotNet.Backends;

public sealed class MsWordDocumentBackend : IDocumentBackend
{
    public string Name => "msword_backend";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".docx", ".docm", ".dotx", ".dotm"];
    public DocumentBackendCapabilities Capabilities { get; } = new() { SupportsPagination = false };

    public Task<IReadOnlyList<SegmentedPdfPageDto>> ConvertAsync(Stream stream, CancellationToken cancellationToken = default)
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
        double currentY = 1000.0; // Start at top

        using (var wordDocument = WordprocessingDocument.Open(stream, false))
        {
            var body = wordDocument.MainDocumentPart?.Document.Body;
            if (body != null)
            {
                foreach (var element in body.ChildElements)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (element is Paragraph paragraph)
                    {
                        var text = GetParagraphText(paragraph);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var isHeading = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value?.StartsWith("Heading") == true;

                            textlineCells.Add(new PdfTextCellDto
                            {
                                Index = cellIndex++,
                                Text = text,
                                Orig = text,
                                TextDirection = "left_to_right",
                                Confidence = 1.0,
                                FromOcr = false,
                                RenderingMode = 0,
                                Widget = false,
                                Rect = new BoundingRectangleDto
                                {
                                    RX0 = 10, RY0 = currentY - 12,
                                    RX1 = 900, RY1 = currentY - 12,
                                    RX2 = 900, RY2 = currentY,
                                    RX3 = 10, RY3 = currentY,
                                    CoordOrigin = "BOTTOMLEFT" 
                                },
                                FontName = isHeading ? "HeadingFont" : "BodyFont",
                                Rgba = new ColorRgbaDto { R = 0, G = 0, B = 0, A = 255 }
                            });
                            currentY -= 14.0;
                        }
                    }
                    else if (element is Table table)
                    {
                        foreach (var row in table.Elements<TableRow>())
                        {
                            foreach (var cell in row.Elements<TableCell>())
                            {
                                var text = string.Join(" ", cell.Descendants<Paragraph>().Select(GetParagraphText).Where(t => !string.IsNullOrWhiteSpace(t)));
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    textlineCells.Add(new PdfTextCellDto
                                    {
                                        Index = cellIndex++,
                                        Text = text,
                                        Orig = text,
                                        TextDirection = "left_to_right",
                                        Confidence = 1.0,
                                        Rect = new BoundingRectangleDto { RX0 = 10, RY0 = currentY - 12, RX1 = 900, RY1 = currentY - 12, RX2 = 900, RY2 = currentY, RX3 = 10, RY3 = currentY, CoordOrigin = "BOTTOMLEFT" }
                                    });
                                    currentY -= 14.0;
                                }
                            }
                        }
                    }
                }
            }
        }

        pageDto.TextlineCells = textlineCells;
        return Task.FromResult<IReadOnlyList<SegmentedPdfPageDto>>([pageDto]);
    }

    private static string GetParagraphText(Paragraph paragraph)
    {
        var text = string.Join("", paragraph.Descendants<Text>().Select(t => t.Text));
        // Remove weird non-breaking spaces or tabs
        return text.Trim();
    }
}
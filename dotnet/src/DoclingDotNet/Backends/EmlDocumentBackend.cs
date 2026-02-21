using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MimeKit;
using DoclingDotNet.Models;

namespace DoclingDotNet.Backends;

public sealed class EmlDocumentBackend : IDocumentBackend
{
    public string Name => "eml_backend";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".eml", ".msg"];
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

        var message = await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);

        // Add headers
            AddText(textlineCells, $"Subject: {message.Subject}", ref cellIndex, ref currentY, true);
            AddText(textlineCells, $"From: {message.From}", ref cellIndex, ref currentY);
            AddText(textlineCells, $"To: {message.To}", ref cellIndex, ref currentY);
            AddText(textlineCells, $"Date: {message.Date}", ref cellIndex, ref currentY);
            currentY -= 14.0; // Spacer

            if (!string.IsNullOrWhiteSpace(message.HtmlBody))
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(message.HtmlBody);

                var nodes = doc.DocumentNode.SelectNodes("//p | //h1 | //h2 | //h3 | //h4 | //h5 | //h6 | //li | //td | //th");
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var isHeading = node.Name.StartsWith("h", StringComparison.OrdinalIgnoreCase);
                            AddText(textlineCells, text, ref cellIndex, ref currentY, isHeading);
                        }
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(message.TextBody))
            {
                var lines = message.TextBody.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var text = line.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        AddText(textlineCells, text, ref cellIndex, ref currentY);
                    }
                }
            }

        pageDto.TextlineCells = textlineCells;
        return [pageDto];
    }

    private static void AddText(List<PdfTextCellDto> cells, string text, ref long cellIndex, ref double currentY, bool isHeading = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        cells.Add(new PdfTextCellDto
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
            FontName = isHeading ? "HeadingFont" : "BodyFont"
        });
        currentY -= 14.0;
    }
}
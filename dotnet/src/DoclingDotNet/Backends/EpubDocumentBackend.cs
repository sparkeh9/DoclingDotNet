using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using DoclingDotNet.Models;

namespace DoclingDotNet.Backends;

public sealed class EpubDocumentBackend : IDocumentBackend
{
    public string Name => "epub_backend";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".epub"];
    public DocumentBackendCapabilities Capabilities { get; } = new() { SupportsPagination = true };

    public Task<IReadOnlyList<SegmentedPdfPageDto>> ConvertAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pages = new List<SegmentedPdfPageDto>();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.FullName.EndsWith(".html", System.StringComparison.OrdinalIgnoreCase) ||
                    entry.FullName.EndsWith(".xhtml", System.StringComparison.OrdinalIgnoreCase))
                {
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

                    using var entryStream = entry.Open();
                    var doc = new HtmlDocument();
                    doc.Load(entryStream);

                    var nodes = doc.DocumentNode.SelectNodes("//p | //h1 | //h2 | //h3 | //h4 | //h5 | //h6 | //li | //td | //th");
                    if (nodes != null)
                    {
                        foreach (var node in nodes)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                var isHeading = node.Name.StartsWith("h", System.StringComparison.OrdinalIgnoreCase);

                                textlineCells.Add(new PdfTextCellDto
                                {
                                    Index = cellIndex++,
                                    Text = text,
                                    Orig = text,
                                    TextDirection = "left_to_right",
                                    Confidence = 1.0,
                                    Rect = new BoundingRectangleDto { RX0 = 10, RY0 = currentY - 12, RX1 = 900, RY1 = currentY - 12, RX2 = 900, RY2 = currentY, RX3 = 10, RY3 = currentY, CoordOrigin = "BOTTOMLEFT" },
                                    FontName = isHeading ? "HeadingFont" : "BodyFont"
                                });
                                currentY -= 14.0;
                            }
                        }
                    }

                    pageDto.TextlineCells = textlineCells;
                    pages.Add(pageDto);
                }
            }
        }

        if (pages.Count == 0)
        {
            // Add an empty page if nothing was found
            pages.Add(new SegmentedPdfPageDto
            {
                Dimension = new PdfPageGeometryDto
                {
                    Angle = 0,
                    Rect = new BoundingRectangleDto { RX0 = 0, RY0 = 0, RX1 = 1000, RY1 = 0, RX2 = 1000, RY2 = 1000, RX3 = 0, RY3 = 1000, CoordOrigin = "BOTTOMLEFT" },
                    BoundaryType = "crop_box"
                }
            });
        }

        return Task.FromResult<IReadOnlyList<SegmentedPdfPageDto>>(pages);
    }
}
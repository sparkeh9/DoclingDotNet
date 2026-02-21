using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoclingDotNet.Models;

namespace DoclingDotNet.Backends;

public sealed class CsvDocumentBackend : IDocumentBackend
{
    public string Name => "csv_backend";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".csv", ".tsv"];
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
                var delimiter = ',';
        
                foreach (var line in lines)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(line)) continue;
        
                    // Basic parsing; real implementation might need CsvHelper for quotes/escapes
                    var cells = line.Split(delimiter);
                    var combinedText = string.Join(" \t ", cells.Select(c => c.Trim()));
        
                    if (!string.IsNullOrWhiteSpace(combinedText))
                    {
                        textlineCells.Add(new PdfTextCellDto
                        {
                            Index = cellIndex++,
                            Text = combinedText,
                            Orig = combinedText,
                            TextDirection = "left_to_right",
                            Confidence = 1.0,
                            Rect = new BoundingRectangleDto
                            {
                                RX0 = 10, RY0 = currentY - 12,
                                RX1 = 900, RY1 = currentY - 12,
                                RX2 = 900, RY2 = currentY,
                                RX3 = 10, RY3 = currentY,
                                CoordOrigin = "BOTTOMLEFT"
                            }
                        });
                        currentY -= 14.0;
                    }
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
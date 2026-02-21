using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DoclingDotNet.Models;

namespace DoclingDotNet.Backends;

public sealed class MsExcelDocumentBackend : IDocumentBackend
{
    public string Name => "msexcel_backend";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".xlsx", ".xlsm", ".xltx", ".xltm"];
    public DocumentBackendCapabilities Capabilities { get; } = new() { SupportsPagination = true };

    public Task<IReadOnlyList<SegmentedPdfPageDto>> ConvertAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pages = new List<SegmentedPdfPageDto>();

        using (var spreadsheetDocument = SpreadsheetDocument.Open(stream, false))
        {
            var workbookPart = spreadsheetDocument.WorkbookPart;
            if (workbookPart?.Workbook?.Sheets != null)
            {
                var sheets = workbookPart.Workbook.Sheets.Elements<Sheet>().ToList();
                var sharedStringTable = workbookPart.GetPartsOfType<SharedStringTablePart>()
                                                    .FirstOrDefault()?.SharedStringTable;

                foreach (var sheet in sheets)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var relationshipId = sheet.Id?.Value;
                    if (string.IsNullOrWhiteSpace(relationshipId)) continue;

                    var worksheetPart = (WorksheetPart)workbookPart.GetPartById(relationshipId);
                    var sheetData = worksheetPart.Worksheet.Elements<SheetData>().FirstOrDefault();
                    if (sheetData == null) continue;

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

                    // Treat the sheet title as a heading
                    var sheetName = sheet.Name?.Value;
                    if (!string.IsNullOrWhiteSpace(sheetName))
                    {
                        textlineCells.Add(CreateTextCell(ref cellIndex, ref currentY, sheetName));
                    }

                    foreach (var row in sheetData.Elements<Row>())
                    {
                        var rowValues = new List<string>();
                        foreach (var cell in row.Elements<Cell>())
                        {
                            var cellValue = GetCellValue(cell, sharedStringTable);
                            if (!string.IsNullOrWhiteSpace(cellValue))
                            {
                                rowValues.Add(cellValue);
                            }
                        }

                        if (rowValues.Count > 0)
                        {
                            var combinedText = string.Join(" 	 ", rowValues);
                            textlineCells.Add(CreateTextCell(ref cellIndex, ref currentY, combinedText));
                        }
                    }

                    pageDto.TextlineCells = textlineCells;
                    pages.Add(pageDto);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<SegmentedPdfPageDto>>(pages);
    }

    private static string GetCellValue(Cell cell, SharedStringTable? sharedStringTable)
    {
        var value = cell.CellValue?.Text;
        if (value != null && cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            if (sharedStringTable != null && int.TryParse(value, out var index))
            {
                var sharedStringItem = sharedStringTable.ElementAt(index);
                return sharedStringItem.InnerText;
            }
        }
        return value ?? string.Empty;
    }

    private static PdfTextCellDto CreateTextCell(ref long cellIndex, ref double currentY, string text)
    {
        var rect = new BoundingRectangleDto
        {
            RX0 = 10,
            RY0 = currentY - 12,
            RX1 = 900,
            RY1 = currentY - 12,
            RX2 = 900,
            RY2 = currentY,
            RX3 = 10,
            RY3 = currentY,
            CoordOrigin = "BOTTOMLEFT"
        };
        currentY -= 14.0;

        return new PdfTextCellDto
        {
            Index = cellIndex++,
            Text = text,
            Orig = text,
            TextDirection = "left_to_right",
            Confidence = 1.0,
            FromOcr = false,
            RenderingMode = 0,
            Widget = false,
            Rect = rect,
            FontName = "BodyFont",
            Rgba = new ColorRgbaDto { R = 0, G = 0, B = 0, A = 255 }
        };
    }
}
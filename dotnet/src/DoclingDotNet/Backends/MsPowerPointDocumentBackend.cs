using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DoclingDotNet.Models;

namespace DoclingDotNet.Backends;

public sealed class MsPowerPointDocumentBackend : IDocumentBackend
{
    public string Name => "msppt_backend";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".pptx", ".pptm", ".potx", ".potm"];
    public DocumentBackendCapabilities Capabilities { get; } = new() { SupportsPagination = true };

    public Task<IReadOnlyList<SegmentedPdfPageDto>> ConvertAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pages = new List<SegmentedPdfPageDto>();

        using (var presentationDocument = PresentationDocument.Open(stream, false))
        {
            var presentationPart = presentationDocument.PresentationPart;
            if (presentationPart?.Presentation?.SlideIdList != null)
            {
                long cellIndex = 0;
                foreach (var slideId in presentationPart.Presentation.SlideIdList.Elements<SlideId>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId!);
                    
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
                    double currentY = 1000.0;
                    
                    if (slidePart.Slide != null)
                    {
                        foreach (var shape in slidePart.Slide.Descendants<Shape>())
                        {
                            var text = shape.TextBody?.InnerText;
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
                    
                    pageDto.TextlineCells = textlineCells;
                    pages.Add(pageDto);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<SegmentedPdfPageDto>>(pages);
    }
}
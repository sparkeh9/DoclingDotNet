using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoclingDotNet.Models;
using SkiaSharp;
using Tesseract;

namespace DoclingDotNet.Backends;

public sealed class ImageDocumentBackend : IDocumentBackend
{
    public string Name => "image_native_backend";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".webp"];
    public DocumentBackendCapabilities Capabilities { get; } = new() { SupportsPagination = false };

    private readonly string _tessDataPath;

    public ImageDocumentBackend(string? tessDataPath = null)
    {
        _tessDataPath = tessDataPath ?? Path.Combine(System.AppContext.BaseDirectory, "tessdata");
    }

    public Task<IReadOnlyList<SegmentedPdfPageDto>> ConvertAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Get image dimensions using SkiaSharp to establish the logical "page" bounds
        int width = 0, height = 0;
        using (var codec = SKCodec.Create(stream))
        {
            if (codec != null)
            {
                width = codec.Info.Width;
                height = codec.Info.Height;
            }
            else
            {
                throw new InvalidDataException("Unsupported image format.");
            }
        }

        var pageDto = new SegmentedPdfPageDto
        {
            Dimension = new PdfPageGeometryDto
            {
                Angle = 0,
                Rect = new BoundingRectangleDto 
                { 
                    RX0 = 0, RY0 = 0, 
                    RX1 = width, RY1 = 0, 
                    RX2 = width, RY2 = height, 
                    RX3 = 0, RY3 = height, 
                    CoordOrigin = "BOTTOMLEFT" 
                },
                BoundaryType = "crop_box"
            },
            HasChars = true,
            HasLines = true,
            HasWords = true,
            BitmapResources = new List<BitmapResourceDto>
            {
                new BitmapResourceDto
                {
                    Index = 0,
                    Uri = "stream://image",
                    Image = new BitmapImageDto { Size = new BitmapImageSizeDto { Width = width, Height = height } }
                }
            }
        };

        var textlineCells = new List<PdfTextCellDto>();

        // Optional OCR extraction directly on the image
        if (Directory.Exists(_tessDataPath) && File.Exists(Path.Combine(_tessDataPath, "eng.traineddata")))
        {
            using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
            using var pix = Pix.LoadFromMemory(GetStreamBytes(stream));
            using var page = engine.Process(pix);

            var text = page.GetText()?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Simple block extraction for the whole image text since it's a native image
                textlineCells.Add(new PdfTextCellDto
                {
                    Index = 0,
                    Text = text,
                    Orig = text,
                    TextDirection = "left_to_right",
                    Confidence = page.GetMeanConfidence(),
                    FromOcr = true,
                    Rect = new BoundingRectangleDto 
                    { 
                        RX0 = 10, RY0 = 10, 
                        RX1 = width - 10, RY1 = 10, 
                        RX2 = width - 10, RY2 = height - 10, 
                        RX3 = 10, RY3 = height - 10, 
                        CoordOrigin = "BOTTOMLEFT" 
                    },
                    FontName = "ocr"
                });
            }
        }

        pageDto.TextlineCells = textlineCells;
        return Task.FromResult<IReadOnlyList<SegmentedPdfPageDto>>([pageDto]);
    }
    
    private static byte[] GetStreamBytes(Stream stream)
    {
        if (stream is MemoryStream ms)
        {
            return ms.ToArray();
        }

        using var memoryStream = new MemoryStream();
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}
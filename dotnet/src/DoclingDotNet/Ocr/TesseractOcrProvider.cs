using System.Text.Json;
using DoclingDotNet.Models;
using DoclingDotNet.Serialization;
using Tesseract;

namespace DoclingDotNet.Ocr;

public sealed class TesseractOcrProviderOptions
{
    public string Name { get; init; } = "tesseract";

    public int Priority { get; init; } = 100;

    public string DataPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "tessdata");

    public string DefaultLanguage { get; init; } = "eng";

    public EngineMode EngineMode { get; init; } = EngineMode.Default;

    public bool InjectWordsWhenMissing { get; init; } = true;
    
    public bool AutoDownloadMissingLanguages { get; init; } = true;
}

public sealed class TesseractOcrProvider : IDoclingOcrProvider
{
    private readonly TesseractOcrProviderOptions _options;
    private static readonly HttpClient HttpClient = new();

    public TesseractOcrProvider(TesseractOcrProviderOptions? options = null)
    {
        _options = options ?? new TesseractOcrProviderOptions();
    }

    public string Name => _options.Name;

    public int Priority => _options.Priority;

    public OcrProviderCapabilities Capabilities { get; } = new()
    {
        SupportsLanguageSelection = true,
        SupportsIncrementalPageProcessing = true,
        IsTrusted = true,
        IsExternal = false
    };

    public bool IsAvailable()
    {
        return _options.AutoDownloadMissingLanguages || HasLanguageData(_options.DefaultLanguage);
    }

    public async Task<OcrProcessResult> ProcessAsync(
        OcrProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Pages.Count == 0)
        {
            return OcrProcessResult.NoChanges("No pages to OCR.");
        }

        var language = string.IsNullOrWhiteSpace(request.Language)
            ? _options.DefaultLanguage
            : request.Language!.Trim();

        if (!HasLanguageData(language))
        {
            if (_options.AutoDownloadMissingLanguages)
            {
                try
                {
                    await DownloadLanguageDataAsync(language, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    return OcrProcessResult.RecoverableFailure(
                        "LanguageDownloadFailed",
                        $"Failed to auto-download Tesseract language data for '{language}': {ex.Message}");
                }
            }
            else
            {
                return OcrProcessResult.RecoverableFailure(
                    "MissingLanguageData",
                    $"Tesseract language data not found for '{language}' at '{_options.DataPath}'. Auto-download is disabled.");
            }
        }

        try
        {
            using var engine = new TesseractEngine(_options.DataPath, language, _options.EngineMode);
            var updatedPages = request.Pages.Select(ClonePage).ToList();
            var hasChanges = false;

            for (var pageIndex = 0; pageIndex < updatedPages.Count; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_options.InjectWordsWhenMissing || updatedPages[pageIndex].WordCells.Count > 0)
                {
                    continue;
                }

                var imagePath = ResolveBestImagePath(updatedPages[pageIndex], request.FilePath);
                if (imagePath is null)
                {
                    continue;
                }

                using var pix = Pix.LoadFromFile(imagePath);
                using var page = engine.Process(pix);
                var text = page.GetText()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var syntheticCell = BuildSyntheticOcrWordCell(updatedPages[pageIndex], text, page.GetMeanConfidence());
                updatedPages[pageIndex].WordCells.Add(syntheticCell);
                updatedPages[pageIndex].HasWords = true;
                hasChanges = true;
            }

            if (hasChanges)
            {
                return OcrProcessResult.Succeeded(
                    updatedPages,
                    "Tesseract OCR fallback applied.");
            }

            return OcrProcessResult.NoChanges("No OCR-compatible page images were found.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return OcrProcessResult.RecoverableFailure(
                ex.GetType().Name,
                ex.Message);
        }
    }

    private bool HasLanguageData(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return false;
        }

        if (!Directory.Exists(_options.DataPath))
        {
            return false;
        }

        var trainedDataPath = Path.Combine(_options.DataPath, $"{language}.traineddata");
        return File.Exists(trainedDataPath);
    }
    
    private async Task DownloadLanguageDataAsync(string language, CancellationToken cancellationToken)
    {
        var trainedDataPath = Path.Combine(_options.DataPath, $"{language}.traineddata");
        var url = $"https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/{language}.traineddata";

        if (!Directory.Exists(_options.DataPath))
        {
            Directory.CreateDirectory(_options.DataPath);
        }

        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var tempPath = trainedDataPath + ".tmp";
        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
        {
            await response.Content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, trainedDataPath, overwrite: true);
    }

    private static SegmentedPdfPageDto ClonePage(SegmentedPdfPageDto page)
    {
        var json = JsonSerializer.Serialize(page, DoclingJson.SerializerOptions);
        var cloned = JsonSerializer.Deserialize<SegmentedPdfPageDto>(json, DoclingJson.SerializerOptions);
        return cloned ?? throw new InvalidOperationException("Failed to clone segmented page.");
    }

    private static string? ResolveBestImagePath(SegmentedPdfPageDto page, string pdfFilePath)
    {
        foreach (var resource in page.BitmapResources)
        {
            var candidates = new[] { resource.Image?.Uri, resource.Uri };
            foreach (var candidate in candidates)
            {
                if (TryResolveLocalPath(candidate, pdfFilePath, out var localPath))
                {
                    return localPath;
                }
            }
        }

        return null;
    }

    private static bool TryResolveLocalPath(
        string? candidate,
        string pdfFilePath,
        out string localPath)
    {
        localPath = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var absoluteUri) && absoluteUri.IsFile)
        {
            var filePath = absoluteUri.LocalPath;
            if (File.Exists(filePath))
            {
                localPath = filePath;
                return true;
            }
        }

        if (Path.IsPathRooted(candidate) && File.Exists(candidate))
        {
            localPath = candidate;
            return true;
        }

        var pdfDir = Path.GetDirectoryName(Path.GetFullPath(pdfFilePath));
        if (string.IsNullOrWhiteSpace(pdfDir))
        {
            return false;
        }

        var combined = Path.Combine(pdfDir, candidate);
        var full = Path.GetFullPath(combined);
        if (!File.Exists(full))
        {
            return false;
        }

        localPath = full;
        return true;
    }

    private static PdfTextCellDto BuildSyntheticOcrWordCell(
        SegmentedPdfPageDto page,
        string text,
        float confidence)
    {
        var pageRect = page.Dimension.Rect;

        return new PdfTextCellDto
        {
            Index = page.WordCells.Count == 0 ? 0 : page.WordCells.Max(c => c.Index) + 1,
            Rgba = new ColorRgbaDto { R = 0, G = 0, B = 0, A = 255 },
            Rect = new BoundingRectangleDto
            {
                RX0 = pageRect.RX0,
                RY0 = pageRect.RY0,
                RX1 = pageRect.RX1,
                RY1 = pageRect.RY1,
                RX2 = pageRect.RX2,
                RY2 = pageRect.RY2,
                RX3 = pageRect.RX3,
                RY3 = pageRect.RY3,
                CoordOrigin = pageRect.CoordOrigin
            },
            Text = text,
            Orig = text,
            TextDirection = "unknown",
            Confidence = confidence,
            FromOcr = true,
            RenderingMode = 0,
            Widget = false,
            FontKey = "ocr",
            FontName = "ocr"
        };
    }
}
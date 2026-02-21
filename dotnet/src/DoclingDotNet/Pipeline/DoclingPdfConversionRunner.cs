using System.Text.Json;
using DoclingDotNet.Algorithms.Layout;
using DoclingDotNet.Algorithms.ReadingOrder;
using DoclingDotNet.Algorithms.Spatial;
using DoclingDotNet.Layout;
using DoclingDotNet.Models;
using DoclingDotNet.Ocr;
using DoclingDotNet.Parsing;
using DoclingDotNet.Serialization;

namespace DoclingDotNet.Pipeline;

public static class PdfConversionStageNames
{
    public const string InitSession = "init_session";
    public const string LoadDocument = "load_document";
    public const string DecodePages = "decode_pages";
    public const string ApplyOcrFallback = "apply_ocr_fallback";
    public const string ApplyLayoutInference = "apply_layout_inference";
    public const string ApplyLayoutPostprocessing = "apply_layout_postprocessing";
    public const string ApplyReadingOrder = "apply_reading_order";
    public const string TransformPages = "transform_pages";
    public const string ExtractAnnotations = "extract_annotations";
    public const string ExtractTableOfContents = "extract_table_of_contents";
    public const string ExtractMetaXml = "extract_meta_xml";
    public const string UnloadDocument = "unload_document";
}

public sealed record PdfPageDecodeError(
    int PageNumber,
    string ErrorType,
    string Message);

public sealed record PdfConversionDiagnostic(
    string Code,
    string? StageName,
    int? PageNumber,
    string ErrorType,
    string Message,
    bool Recoverable);

public sealed class PdfPageDecodeException : Exception
{
    public PdfPageDecodeException(
        string documentKey,
        int pageNumber,
        Exception innerException)
        : base(
            $"Decode failed for document '{documentKey}', page {pageNumber}: {innerException.Message}",
            innerException)
    {
        DocumentKey = documentKey;
        PageNumber = pageNumber;
    }

    public string DocumentKey { get; }

    public int PageNumber { get; }
}

public sealed class PdfTransformException : Exception
{
    public PdfTransformException(
        string documentKey,
        Exception innerException)
        : base(
            $"Transform failed for document '{documentKey}': {innerException.Message}",
            innerException)
    {
        DocumentKey = documentKey;
    }

    public string DocumentKey { get; }
}

public sealed class PdfContextExtractionException : Exception
{
    public PdfContextExtractionException(
        string documentKey,
        string stageName,
        Exception innerException)
        : base(
            $"Context extraction stage '{stageName}' failed for document '{documentKey}': {innerException.Message}",
            innerException)
    {
        DocumentKey = documentKey;
        StageName = stageName;
    }

    public string DocumentKey { get; }

    public string StageName { get; }
}

public sealed class PdfOcrProcessingException : Exception
{
    public PdfOcrProcessingException(
        string documentKey,
        string providerName,
        Exception innerException)
        : base(
            $"OCR provider '{providerName}' failed for document '{documentKey}': {innerException.Message}",
            innerException)
    {
        DocumentKey = documentKey;
        ProviderName = providerName;
    }

    public string DocumentKey { get; }

    public string ProviderName { get; }
}

public sealed class PdfConversionRequest
{
    public string? FilePath { get; init; }

    public Stream? InputStream { get; init; }

    public string? InputFormat { get; init; }

    public string? RunId { get; init; }

    public string? DocumentKey { get; init; }

    public string? ResourcesDir { get; init; }

    public string? Password { get; init; }

    public string SessionLogLevel { get; init; } = "error";

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public int? MaxPages { get; init; }

    public bool ContinueOnPageDecodeError { get; init; }

    public bool EnableOcrFallback { get; init; }

    public bool RequireOcrSuccess { get; init; }

    public string? OcrLanguage { get; init; }

    public IReadOnlyList<string>? PreferredOcrProviders { get; init; }

    public IReadOnlyList<string>? AllowedOcrProviders { get; init; }

    public bool AllowExternalOcrProviders { get; init; }

    public bool AllowUntrustedOcrProviders { get; init; }

    public bool EnableLayoutInference { get; init; }

    public IReadOnlyList<string>? PreferredLayoutProviders { get; init; }

    public Func<IReadOnlyList<SegmentedPdfPageDto>, IReadOnlyList<SegmentedPdfPageDto>>? TransformPages { get; init; }

    public bool IncludeAnnotations { get; init; }

    public bool IncludeTableOfContents { get; init; }

    public bool IncludeMetaXml { get; init; }
}

public sealed class PdfConversionRunResult
{
    public required string RunId { get; init; }

    public required string DocumentKey { get; init; }

    public required string FilePath { get; init; }

    public required int PageCount { get; init; }

    public required IReadOnlyList<SegmentedPdfPageDto> Pages { get; init; }

    public required IReadOnlyList<PdfPageDecodeError> PageErrors { get; init; }

    public required int DecodeAttemptedPageCount { get; init; }

    public required int DecodeSucceededPageCount { get; init; }

    public required bool OcrApplied { get; init; }

    public string? OcrProviderName { get; init; }

    public required bool LayoutInferenceApplied { get; init; }

    public string? LayoutProviderName { get; init; }

    public required bool LayoutPostprocessingApplied { get; init; }

    public required bool ReadingOrderApplied { get; init; }

    public string? AnnotationsJson { get; init; }

    public string? TableOfContentsJson { get; init; }

    public string? MetaXmlJson { get; init; }

    public required IReadOnlyList<PdfConversionDiagnostic> Diagnostics { get; init; }

    public required PipelineRunResult Pipeline { get; init; }
}

public enum PdfBatchDocumentStatus
{
    Succeeded = 0,
    Failed = 1,
    Skipped = 2
}

public sealed class PdfBatchConversionRequest
{
    public required IReadOnlyList<PdfConversionRequest> Documents { get; init; }

    public string? BatchRunId { get; init; }

    public bool ContinueOnDocumentFailure { get; init; } = true;

    public string? ArtifactOutputDirectory { get; init; }

    public bool CleanArtifactOutputDirectory { get; init; } = true;
}

public sealed class PdfBatchDocumentResult
{
    public required int Index { get; init; }

    public required string FilePath { get; init; }

    public required PdfBatchDocumentStatus Status { get; init; }

    public PdfConversionRunResult? Conversion { get; init; }

    public required IReadOnlyList<PdfConversionDiagnostic> Diagnostics { get; init; }
}

public sealed record PdfConversionArtifactFile(
    string RelativePath,
    string ContentType,
    string Content);

public sealed class PdfConversionArtifactBundle
{
    public required string BatchRunId { get; init; }

    public required IReadOnlyList<PdfConversionArtifactFile> Files { get; init; }
}

public sealed class PdfBatchAggregationSummary
{
    public required int DocumentCount { get; init; }

    public required int SucceededDocumentCount { get; init; }

    public required int FailedDocumentCount { get; init; }

    public required int SkippedDocumentCount { get; init; }

    public required int TotalPageCount { get; init; }

    public required int TotalDecodeAttemptedPageCount { get; init; }

    public required int TotalDecodeSucceededPageCount { get; init; }

    public required int TotalDiagnosticsCount { get; init; }

    public required int RecoverableDiagnosticsCount { get; init; }

    public required int NonRecoverableDiagnosticsCount { get; init; }

    public required IReadOnlyDictionary<string, int> DocumentStatusCounts { get; init; }

    public required IReadOnlyDictionary<string, int> PipelineStatusCounts { get; init; }

    public required IReadOnlyDictionary<string, int> DiagnosticCodeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> DiagnosticStageCounts { get; init; }
}

public sealed class PdfBatchConversionResult
{
    public required string BatchRunId { get; init; }

    public required IReadOnlyList<PdfBatchDocumentResult> Documents { get; init; }

    public required IReadOnlyList<PdfConversionDiagnostic> Diagnostics { get; init; }

    public required PdfConversionArtifactBundle ArtifactBundle { get; init; }

    public required PdfBatchAggregationSummary Aggregation { get; init; }

    public string? PersistedArtifactDirectory { get; init; }
}

public sealed class DoclingPdfConversionRunner
{
    private readonly PipelineExecutor _pipelineExecutor;
    private readonly Func<string, IDoclingParseSession> _sessionFactory;
    private readonly IReadOnlyList<IDoclingOcrProvider> _ocrProviders;
    private readonly IReadOnlyList<IDoclingLayoutProvider> _layoutProviders;

    public static IReadOnlyList<IDoclingOcrProvider> CreateDefaultOcrProviders()
    {
        return
        [
            new TesseractOcrProvider()
        ];
    }

    public static IReadOnlyList<IDoclingLayoutProvider> CreateDefaultLayoutProviders()
    {
        return [];
    }

    public DoclingPdfConversionRunner(
        Func<string, IDoclingParseSession>? sessionFactory = null,
        PipelineExecutor? pipelineExecutor = null,
        IEnumerable<IDoclingOcrProvider>? ocrProviders = null,
        bool includeDefaultOcrProviders = true,
        IEnumerable<IDoclingLayoutProvider>? layoutProviders = null,
        bool includeDefaultLayoutProviders = true)
    {
        _sessionFactory = sessionFactory ?? (logLevel => DoclingParseSession.Create(logLevel));
        _pipelineExecutor = pipelineExecutor ?? new PipelineExecutor();
        _ocrProviders = BuildConfiguredOcrProviders(ocrProviders, includeDefaultOcrProviders);
        _layoutProviders = BuildConfiguredLayoutProviders(layoutProviders, includeDefaultLayoutProviders);
    }

    public async Task<PdfConversionRunResult> ExecuteAsync(
        PdfConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.FilePath) && request.InputStream == null)
        {
            throw new ArgumentException("Either FilePath or InputStream must be provided.", nameof(request));
        }
        if (request.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Timeout must be greater than zero.");
        }
        if (request.MaxPages.HasValue && request.MaxPages.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "MaxPages must be greater than zero when provided.");
        }

        var runId = request.RunId ?? $"run_{Guid.NewGuid():N}";
        var documentKey = request.DocumentKey ?? CreateDocumentKey(request.FilePath ?? "stream", runId);
        var pages = new List<SegmentedPdfPageDto>();
        var pageErrors = new List<PdfPageDecodeError>();
        var diagnostics = new List<PdfConversionDiagnostic>();

        IDoclingParseSession? session = null;
        var documentLoaded = false;
        var pageCount = 0;
        var decodeAttemptedPageCount = 0;
        var decodeSucceededPageCount = 0;
        var ocrApplied = false;
        string? ocrProviderName = null;
        var layoutInferenceApplied = false;
        string? layoutProviderName = null;
        var layoutPostprocessingApplied = false;
        var readingOrderApplied = false;
        string? annotationsJson = null;
        string? tableOfContentsJson = null;
        string? metaXmlJson = null;
        IReadOnlyDictionary<int, IReadOnlyList<LayoutCluster>>? pageClusters = null;

        try
        {
            var stages = new List<PipelineStageDefinition>
            {
                new()
                {
                    Name = PdfConversionStageNames.InitSession,
                    ExecuteAsync = (_, _) =>
                    {
                        session = _sessionFactory(request.SessionLogLevel);
                        if (!string.IsNullOrWhiteSpace(request.ResourcesDir))
                        {
                            session.SetResourcesDir(request.ResourcesDir);
                        }

                        return Task.CompletedTask;
                    }
                },
                                  new()
                                  {
                                      Name = PdfConversionStageNames.LoadDocument,
                                      ExecuteAsync = async (_, token) =>
                                      {
                                          var parseSession = EnsureSession(session);
                                          if (request.InputStream != null)
                                          {
                                              using var ms = new MemoryStream();
                                              await request.InputStream.CopyToAsync(ms, token).ConfigureAwait(false);
                                              parseSession.LoadDocumentFromBytes(documentKey, ms.ToArray(), "stream", request.Password);
                                          }
                                          else
                                          {
                                              parseSession.LoadDocument(documentKey, request.FilePath!, request.Password);
                                          }
                                          
                                          documentLoaded = true;
                                      }
                                  },                new()
                {
                    Name = PdfConversionStageNames.DecodePages,
                    ExecuteAsync = (_, token) =>
                    {
                        var parseSession = EnsureSession(session);
                        pageCount = parseSession.GetPageCount(documentKey);
                        var decodeLimit = request.MaxPages.HasValue
                            ? Math.Min(pageCount, request.MaxPages.Value)
                            : pageCount;

                        for (var pageNumber = 0; pageNumber < decodeLimit; pageNumber++)
                        {
                            token.ThrowIfCancellationRequested();
                            decodeAttemptedPageCount++;

                            try
                            {
                                pages.Add(parseSession.DecodeSegmentedPage(documentKey, pageNumber));
                                decodeSucceededPageCount++;
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                if (!request.ContinueOnPageDecodeError)
                                {
                                    throw new PdfPageDecodeException(documentKey, pageNumber, ex);
                                }

                                pageErrors.Add(new PdfPageDecodeError(
                                    pageNumber,
                                    ex.GetType().Name,
                                    ex.Message));
                                diagnostics.Add(new PdfConversionDiagnostic(
                                    Code: "decode_page_failed",
                                    StageName: PdfConversionStageNames.DecodePages,
                                    PageNumber: pageNumber,
                                    ErrorType: ex.GetType().Name,
                                    Message: ex.Message,
                                    Recoverable: true));
                            }
                        }

                        return Task.CompletedTask;
                    }
                },
                new()
                {
                    Name = PdfConversionStageNames.ApplyOcrFallback,
                    ExecuteAsync = async (_, token) =>
                    {
                        if (!request.EnableOcrFallback)
                        {
                            return;
                        }

                        var providerChain = ResolveOcrProviders(
                            request.PreferredOcrProviders,
                            _ocrProviders);

                        if (providerChain.Count == 0)
                        {
                            const string message = "No OCR providers are configured.";
                            diagnostics.Add(new PdfConversionDiagnostic(
                                Code: "ocr_provider_unavailable",
                                StageName: PdfConversionStageNames.ApplyOcrFallback,
                                PageNumber: null,
                                ErrorType: "NoOcrProviderConfigured",
                                Message: message,
                                Recoverable: !request.RequireOcrSuccess));

                            if (request.RequireOcrSuccess)
                            {
                                throw new InvalidOperationException(message);
                            }

                            return;
                        }

                        var ocrRequest = new OcrProcessRequest
                        {
                            RunId = runId,
                            DocumentKey = documentKey,
                            FilePath = request.FilePath ?? "stream",
                            Language = request.OcrLanguage,
                            Pages = pages.ToArray()
                        };
                        HashSet<string>? allowedProviders = null;
                        if (request.AllowedOcrProviders is { Count: > 0 })
                        {
                            allowedProviders = new HashSet<string>(
                                request.AllowedOcrProviders,
                                StringComparer.OrdinalIgnoreCase);
                        }

                        foreach (var provider in providerChain)
                        {
                            token.ThrowIfCancellationRequested();
                            if (allowedProviders is not null && !allowedProviders.Contains(provider.Name))
                            {
                                diagnostics.Add(new PdfConversionDiagnostic(
                                    Code: "ocr_provider_not_allowed",
                                    StageName: PdfConversionStageNames.ApplyOcrFallback,
                                    PageNumber: null,
                                    ErrorType: "OcrProviderNotAllowed",
                                    Message: $"OCR provider '{provider.Name}' is not in the configured allowlist.",
                                    Recoverable: true));
                                continue;
                            }

                            if (provider.Capabilities.IsExternal && !request.AllowExternalOcrProviders)
                            {
                                diagnostics.Add(new PdfConversionDiagnostic(
                                    Code: "ocr_provider_external_blocked",
                                    StageName: PdfConversionStageNames.ApplyOcrFallback,
                                    PageNumber: null,
                                    ErrorType: "OcrExternalProviderBlocked",
                                    Message: $"OCR provider '{provider.Name}' is marked external and is not allowed.",
                                    Recoverable: true));
                                continue;
                            }

                            if (!provider.Capabilities.IsTrusted && !request.AllowUntrustedOcrProviders)
                            {
                                diagnostics.Add(new PdfConversionDiagnostic(
                                    Code: "ocr_provider_untrusted",
                                    StageName: PdfConversionStageNames.ApplyOcrFallback,
                                    PageNumber: null,
                                    ErrorType: "OcrUntrustedProviderBlocked",
                                    Message: $"OCR provider '{provider.Name}' is untrusted and was blocked by policy.",
                                    Recoverable: true));
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(request.OcrLanguage)
                                && !provider.Capabilities.SupportsLanguageSelection)
                            {
                                diagnostics.Add(new PdfConversionDiagnostic(
                                    Code: "ocr_provider_capability_mismatch",
                                    StageName: PdfConversionStageNames.ApplyOcrFallback,
                                    PageNumber: null,
                                    ErrorType: "OcrLanguageSelectionUnsupported",
                                    Message: $"OCR provider '{provider.Name}' does not support explicit language selection.",
                                    Recoverable: true));
                                continue;
                            }

                            if (!provider.IsAvailable())
                            {
                                diagnostics.Add(new PdfConversionDiagnostic(
                                    Code: "ocr_provider_unavailable",
                                    StageName: PdfConversionStageNames.ApplyOcrFallback,
                                    PageNumber: null,
                                    ErrorType: "ProviderUnavailable",
                                    Message: $"OCR provider '{provider.Name}' is unavailable.",
                                    Recoverable: true));
                                continue;
                            }

                            OcrProcessResult providerResult;
                            try
                            {
                                providerResult = await provider.ProcessAsync(ocrRequest, token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                throw new PdfOcrProcessingException(documentKey, provider.Name, ex);
                            }

                            if (providerResult.Status == OcrProcessStatus.Succeeded)
                            {
                                if (providerResult.Pages is not null)
                                {
                                    pages.Clear();
                                    pages.AddRange(providerResult.Pages);
                                }

                                ocrApplied = true;
                                ocrProviderName = provider.Name;
                                diagnostics.Add(new PdfConversionDiagnostic(
                                    Code: "ocr_provider_succeeded",
                                    StageName: PdfConversionStageNames.ApplyOcrFallback,
                                    PageNumber: null,
                                    ErrorType: "OcrProviderSucceeded",
                                    Message: providerResult.Message ?? $"OCR provider '{provider.Name}' succeeded.",
                                    Recoverable: true));
                                return;
                            }

                            if (providerResult.Status == OcrProcessStatus.NoChanges)
                            {
                                ocrProviderName = provider.Name;
                                diagnostics.Add(new PdfConversionDiagnostic(
                                    Code: "ocr_provider_no_changes",
                                    StageName: PdfConversionStageNames.ApplyOcrFallback,
                                    PageNumber: null,
                                    ErrorType: "OcrProviderNoChanges",
                                    Message: providerResult.Message ?? $"OCR provider '{provider.Name}' reported no changes.",
                                    Recoverable: true));
                                return;
                            }

                            if (providerResult.Status == OcrProcessStatus.RecoverableFailure)
                            {
                                diagnostics.Add(new PdfConversionDiagnostic(
                                    Code: "ocr_provider_recoverable_failure",
                                    StageName: PdfConversionStageNames.ApplyOcrFallback,
                                    PageNumber: null,
                                    ErrorType: providerResult.ErrorType ?? "OcrProviderRecoverableFailure",
                                    Message: providerResult.Message ?? $"OCR provider '{provider.Name}' failed recoverably.",
                                    Recoverable: true));
                                continue;
                            }

                            if (providerResult.Status == OcrProcessStatus.FatalFailure)
                            {
                                throw new PdfOcrProcessingException(
                                    documentKey,
                                    provider.Name,
                                    new InvalidOperationException(
                                        providerResult.Message ?? $"OCR provider '{provider.Name}' failed."));
                            }

                            throw new InvalidOperationException($"Unsupported OCR provider status: {providerResult.Status}");
                        }

                        const string exhaustedMessage = "No OCR provider succeeded.";
                        diagnostics.Add(new PdfConversionDiagnostic(
                            Code: "ocr_provider_exhausted",
                            StageName: PdfConversionStageNames.ApplyOcrFallback,
                            PageNumber: null,
                            ErrorType: "OcrProviderChainExhausted",
                            Message: exhaustedMessage,
                            Recoverable: !request.RequireOcrSuccess));

                        if (request.RequireOcrSuccess)
                        {
                            throw new InvalidOperationException(exhaustedMessage);
                        }
                    }
                },
                new()
                {
                    Name = PdfConversionStageNames.ApplyLayoutInference,
                    ExecuteAsync = async (_, token) =>
                    {
                        if (!request.EnableLayoutInference) return;

                        var providerChain = ResolveLayoutProviders(request.PreferredLayoutProviders, _layoutProviders);
                        if (providerChain.Count == 0)
                        {
                            diagnostics.Add(new PdfConversionDiagnostic("layout_provider_unavailable", PdfConversionStageNames.ApplyLayoutInference, null, "NoLayoutProviderConfigured", "No layout providers are configured.", true));
                            return;
                        }

                        var layoutRequest = new LayoutProcessRequest
                        {
                            RunId = runId,
                            DocumentKey = documentKey,
                            FilePath = request.FilePath ?? "stream",
                            Pages = pages.ToArray()
                        };

                        foreach (var provider in providerChain)
                        {
                            token.ThrowIfCancellationRequested();

                            if (!provider.IsAvailable())
                            {
                                diagnostics.Add(new PdfConversionDiagnostic("layout_provider_unavailable", PdfConversionStageNames.ApplyLayoutInference, null, "ProviderUnavailable", $"Layout provider '{provider.Name}' is unavailable.", true));
                                continue;
                            }

                            LayoutProcessResult providerResult;
                            try
                            {
                                providerResult = await provider.ProcessAsync(layoutRequest, token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) { throw; }
                            catch (Exception ex)
                            {
                                diagnostics.Add(new PdfConversionDiagnostic("layout_provider_exception", PdfConversionStageNames.ApplyLayoutInference, null, "ProviderException", ex.Message, true));
                                continue;
                            }

                            if (providerResult.Status == LayoutProcessStatus.Succeeded)
                            {
                                pageClusters = providerResult.PageClusters;
                                layoutInferenceApplied = true;
                                layoutProviderName = provider.Name;
                                return;
                            }
                            
                            if (providerResult.Status == LayoutProcessStatus.FatalFailure)
                            {
                                throw new InvalidOperationException($"Layout provider '{provider.Name}' failed: {providerResult.Message}");
                            }
                        }
                    }
                },
                new()
                {
                    Name = PdfConversionStageNames.ApplyLayoutPostprocessing,
                    ExecuteAsync = (_, token) =>
                    {
                        if (pageClusters == null || pageClusters.Count == 0) return Task.CompletedTask;

                        var options = new LayoutPostprocessorOptions
                        {
                            SkipCellAssignment = false,
                            KeepEmptyClusters = false,
                            CreateOrphanClusters = true
                        };

                        foreach (var page in pages)
                        {
                            token.ThrowIfCancellationRequested();
                            
                            var cells = page.CharCells.Concat(page.WordCells).Concat(page.TextlineCells).ToList();
                            if (cells.Count == 0) continue;

                            if (!pageClusters.TryGetValue((int)page.Dimension.Angle, out var clusters)) // mock fallback logic, typically would use page index
                            {
                                clusters = [];
                            }

                            var postprocessor = new LayoutPostprocessor(page.Dimension, cells, clusters, options);
                            var (finalClusters, finalCells) = postprocessor.Postprocess();
                            
                            // Emitting output clusters is currently mocked. 
                            // The true pipeline drops finalClusters into `page.Lines` or a similar property.
                            page.TextlineCells = finalCells.ToList();
                        }
                        layoutPostprocessingApplied = true;
                        return Task.CompletedTask;
                    }
                },
                new()
                {
                    Name = PdfConversionStageNames.ApplyReadingOrder,
                    ExecuteAsync = (_, token) =>
                    {
                        if (!layoutPostprocessingApplied) return Task.CompletedTask;

                        var predictor = new ReadingOrderPredictor();
                        
                        foreach (var page in pages)
                        {
                            token.ThrowIfCancellationRequested();

                            var elements = new List<PageElement>();
                            int cid = 0;
                            
                            foreach (var cell in page.TextlineCells)
                            {
                                elements.Add(new PageElement
                                {
                                    Cid = cid++,
                                    Bbox = cell.Rect.ToBoundingBox(),
                                    Text = cell.Text,
                                    PageNo = 1, // Mock
                                    Label = "text",
                                    PageWidth = page.Dimension.Rect.RX2,
                                    PageHeight = page.Dimension.Rect.RY2
                                });
                            }

                            var sortedElements = predictor.PredictReadingOrder(elements);
                            var sortedCells = new List<PdfTextCellDto>(page.TextlineCells.Count);
                            
                            foreach (var elem in sortedElements)
                            {
                                sortedCells.Add(page.TextlineCells[elem.Cid]);
                            }

                            page.TextlineCells = sortedCells;
                        }

                        readingOrderApplied = true;
                        return Task.CompletedTask;
                    }
                },
                new()
                {
                    Name = PdfConversionStageNames.TransformPages,
                    ExecuteAsync = (_, _) =>
                    {
                        if (request.TransformPages is null)
                        {
                            return Task.CompletedTask;
                        }

                        IReadOnlyList<SegmentedPdfPageDto> transformedPages;
                        try
                        {
                            transformedPages = request.TransformPages(pages);
                            if (transformedPages is null)
                            {
                                throw new InvalidOperationException("TransformPages returned null.");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            throw new PdfTransformException(documentKey, ex);
                        }

                        var replacement = transformedPages.ToList();
                        if (pages.Count > 0 && replacement.Count == 0)
                        {
                            diagnostics.Add(new PdfConversionDiagnostic(
                                Code: "transform_returned_empty",
                                StageName: PdfConversionStageNames.TransformPages,
                                PageNumber: null,
                                ErrorType: "TransformReturnedEmpty",
                                Message: "TransformPages returned an empty page set.",
                                Recoverable: true));
                        }

                        pages.Clear();
                        pages.AddRange(replacement);
                        return Task.CompletedTask;
                    }
                },
                new()
                {
                    Name = PdfConversionStageNames.ExtractAnnotations,
                    ExecuteAsync = (_, _) =>
                    {
                        if (!request.IncludeAnnotations)
                        {
                            return Task.CompletedTask;
                        }

                        var parseSession = EnsureSession(session);
                        try
                        {
                            annotationsJson = parseSession.GetAnnotationsJson(documentKey);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            throw new PdfContextExtractionException(documentKey, PdfConversionStageNames.ExtractAnnotations, ex);
                        }

                        return Task.CompletedTask;
                    }
                },
                new()
                {
                    Name = PdfConversionStageNames.ExtractTableOfContents,
                    ExecuteAsync = (_, _) =>
                    {
                        if (!request.IncludeTableOfContents)
                        {
                            return Task.CompletedTask;
                        }

                        var parseSession = EnsureSession(session);
                        try
                        {
                            tableOfContentsJson = parseSession.GetTableOfContentsJson(documentKey);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            throw new PdfContextExtractionException(documentKey, PdfConversionStageNames.ExtractTableOfContents, ex);
                        }

                        return Task.CompletedTask;
                    }
                },
                new()
                {
                    Name = PdfConversionStageNames.ExtractMetaXml,
                    ExecuteAsync = (_, _) =>
                    {
                        if (!request.IncludeMetaXml)
                        {
                            return Task.CompletedTask;
                        }

                        var parseSession = EnsureSession(session);
                        try
                        {
                            metaXmlJson = parseSession.GetMetaXmlJson(documentKey);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            throw new PdfContextExtractionException(documentKey, PdfConversionStageNames.ExtractMetaXml, ex);
                        }

                        return Task.CompletedTask;
                    }
                },
                new()
                {
                    Name = PdfConversionStageNames.UnloadDocument,
                    Kind = PipelineStageKind.Cleanup,
                    ExecuteAsync = (_, _) =>
                    {
                        if (session is not null && documentLoaded)
                        {
                            session.UnloadDocument(documentKey);
                            documentLoaded = false;
                        }

                        return Task.CompletedTask;
                    }
                }
            };

            var pipelineResult = await _pipelineExecutor.ExecuteAsync(
                runId,
                stages,
                request.Timeout,
                cancellationToken).ConfigureAwait(false);

            diagnostics.AddRange(CreatePipelineDiagnostics(pipelineResult));

            return new PdfConversionRunResult
            {
                RunId = runId,
                DocumentKey = documentKey,
                FilePath = request.FilePath ?? "stream",
                PageCount = pageCount,
                Pages = pages,
                PageErrors = pageErrors,
                DecodeAttemptedPageCount = decodeAttemptedPageCount,
                DecodeSucceededPageCount = decodeSucceededPageCount,
                OcrApplied = ocrApplied,
                OcrProviderName = ocrProviderName,
                LayoutInferenceApplied = layoutInferenceApplied,
                LayoutProviderName = layoutProviderName,
                LayoutPostprocessingApplied = layoutPostprocessingApplied,
                ReadingOrderApplied = readingOrderApplied,
                AnnotationsJson = annotationsJson,
                TableOfContentsJson = tableOfContentsJson,
                MetaXmlJson = metaXmlJson,
                Diagnostics = diagnostics,
                Pipeline = pipelineResult
            };
        }
        finally
        {
            if (session is not null)
            {
                if (documentLoaded)
                {
                    try
                    {
                        session.UnloadDocument(documentKey);
                    }
                    catch
                    {
                        // Best-effort cleanup: preserve primary pipeline outcome.
                    }
                }

                session.Dispose();
            }
        }
    }

    public async Task<PdfBatchConversionResult> ExecuteBatchAsync(
        PdfBatchConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Documents);
        if (request.Documents.Count == 0)
        {
            throw new ArgumentException("Batch must include at least one document.", nameof(request));
        }

        var batchRunId = request.BatchRunId ?? $"batch_{Guid.NewGuid():N}";
        var documents = new List<PdfBatchDocumentResult>(request.Documents.Count);
        var diagnostics = new List<PdfConversionDiagnostic>();
        var stopFurtherExecution = false;

        for (var index = 0; index < request.Documents.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var documentRequest = request.Documents[index];

            if (stopFurtherExecution)
            {
                var skippedDiagnostic = new PdfConversionDiagnostic(
                    Code: "batch_document_skipped",
                    StageName: null,
                    PageNumber: null,
                    ErrorType: "BatchExecutionStopped",
                    Message: "Skipped because batch execution was stopped after previous failure.",
                    Recoverable: true);
                documents.Add(new PdfBatchDocumentResult
                {
                    Index = index,
                    FilePath = documentRequest.FilePath ?? "stream",
                    Status = PdfBatchDocumentStatus.Skipped,
                    Conversion = null,
                    Diagnostics = [skippedDiagnostic]
                });
                diagnostics.Add(skippedDiagnostic);
                continue;
            }

            try
            {
                var runResult = await ExecuteAsync(documentRequest, cancellationToken).ConfigureAwait(false);
                var status = runResult.Pipeline.Status == PipelineRunStatus.Succeeded
                    ? PdfBatchDocumentStatus.Succeeded
                    : PdfBatchDocumentStatus.Failed;

                documents.Add(new PdfBatchDocumentResult
                {
                    Index = index,
                    FilePath = documentRequest.FilePath ?? "stream",
                    Status = status,
                    Conversion = runResult,
                    Diagnostics = runResult.Diagnostics
                });
                diagnostics.AddRange(runResult.Diagnostics);

                if (status == PdfBatchDocumentStatus.Failed && !request.ContinueOnDocumentFailure)
                {
                    stopFurtherExecution = true;
                }
            }
            catch (Exception ex)
            {
                var filePath = string.IsNullOrWhiteSpace(documentRequest.FilePath)
                    ? "stream"
                    : documentRequest.FilePath;

                var failureDiagnostic = new PdfConversionDiagnostic(
                    Code: "batch_document_unhandled_exception",
                    StageName: null,
                    PageNumber: null,
                    ErrorType: ex.GetType().Name,
                    Message: ex.Message,
                    Recoverable: false);

                documents.Add(new PdfBatchDocumentResult
                {
                    Index = index,
                    FilePath = filePath,
                    Status = PdfBatchDocumentStatus.Failed,
                    Conversion = null,
                    Diagnostics = [failureDiagnostic]
                });
                diagnostics.Add(failureDiagnostic);

                if (!request.ContinueOnDocumentFailure)
                {
                    stopFurtherExecution = true;
                }
            }
        }

        var aggregation = CreateBatchAggregation(documents, diagnostics);
        var artifactBundle = CreateArtifactBundle(batchRunId, documents, aggregation);
        string? persistedArtifactDirectory = null;

        if (!string.IsNullOrWhiteSpace(request.ArtifactOutputDirectory))
        {
            persistedArtifactDirectory = await PersistArtifactBundleAsync(
                artifactBundle,
                request.ArtifactOutputDirectory,
                request.CleanArtifactOutputDirectory,
                cancellationToken).ConfigureAwait(false);
        }

        return new PdfBatchConversionResult
        {
            BatchRunId = batchRunId,
            Documents = documents,
            Diagnostics = diagnostics,
            ArtifactBundle = artifactBundle,
            Aggregation = aggregation,
            PersistedArtifactDirectory = persistedArtifactDirectory
        };
    }

    private static IReadOnlyList<IDoclingOcrProvider> BuildConfiguredOcrProviders(
        IEnumerable<IDoclingOcrProvider>? configuredProviders,
        bool includeDefaultOcrProviders)
    {
        var providers = new List<IDoclingOcrProvider>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (configuredProviders is not null)
        {
            foreach (var provider in configuredProviders)
            {
                if (names.Add(provider.Name))
                {
                    providers.Add(provider);
                }
            }
        }

        if (includeDefaultOcrProviders)
        {
            foreach (var provider in CreateDefaultOcrProviders())
            {
                if (names.Add(provider.Name))
                {
                    providers.Add(provider);
                }
            }
        }

        return providers.ToArray();
    }

    private static IReadOnlyList<IDoclingOcrProvider> ResolveOcrProviders(
        IReadOnlyList<string>? preferredProviders,
        IReadOnlyList<IDoclingOcrProvider> configuredProviders)
    {
        var orderedProviders = configuredProviders
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (preferredProviders is null || preferredProviders.Count == 0)
        {
            return orderedProviders;
        }

        var providerByName = orderedProviders
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
        var selectedProviders = new List<IDoclingOcrProvider>(orderedProviders.Length);
        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var preferred in preferredProviders)
        {
            if (providerByName.TryGetValue(preferred, out var provider)
                && selectedNames.Add(provider.Name))
            {
                selectedProviders.Add(provider);
            }
        }

        foreach (var provider in orderedProviders)
        {
            if (selectedNames.Add(provider.Name))
            {
                selectedProviders.Add(provider);
            }
        }

        return selectedProviders;
    }

    private static IReadOnlyList<IDoclingLayoutProvider> BuildConfiguredLayoutProviders(
        IEnumerable<IDoclingLayoutProvider>? configuredProviders,
        bool includeDefaultLayoutProviders)
    {
        var providers = new List<IDoclingLayoutProvider>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (configuredProviders is not null)
        {
            foreach (var provider in configuredProviders)
            {
                if (names.Add(provider.Name)) providers.Add(provider);
            }
        }

        if (includeDefaultLayoutProviders)
        {
            foreach (var provider in CreateDefaultLayoutProviders())
            {
                if (names.Add(provider.Name)) providers.Add(provider);
            }
        }

        return providers.ToArray();
    }

    private static IReadOnlyList<IDoclingLayoutProvider> ResolveLayoutProviders(
        IReadOnlyList<string>? preferredProviders,
        IReadOnlyList<IDoclingLayoutProvider> configuredProviders)
    {
        var orderedProviders = configuredProviders
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (preferredProviders is null || preferredProviders.Count == 0)
        {
            return orderedProviders;
        }

        var providerByName = orderedProviders.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
        var selectedProviders = new List<IDoclingLayoutProvider>(orderedProviders.Length);
        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var preferred in preferredProviders)
        {
            if (providerByName.TryGetValue(preferred, out var provider) && selectedNames.Add(provider.Name))
            {
                selectedProviders.Add(provider);
            }
        }

        foreach (var provider in orderedProviders)
        {
            if (selectedNames.Add(provider.Name)) selectedProviders.Add(provider);
        }

        return selectedProviders;
    }

    private static string CreateDocumentKey(string filePath, string runId)
    {
        var stem = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = "document";
        }

        var normalizedStem = stem
            .Replace(' ', '_')
            .Replace('.', '_');

        return $"{normalizedStem}_{runId}";
    }

    private static IDoclingParseSession EnsureSession(IDoclingParseSession? session)
    {
        if (session is null)
        {
            throw new InvalidOperationException("Pipeline stage requires an initialized parser session.");
        }

        return session;
    }

    private static IReadOnlyList<PdfConversionDiagnostic> CreatePipelineDiagnostics(
        PipelineRunResult pipelineResult)
    {
        var diagnostics = new List<PdfConversionDiagnostic>();

        foreach (var stage in pipelineResult.StageResults)
        {
            if (stage.Status == PipelineStageStatus.Failed)
            {
                diagnostics.Add(new PdfConversionDiagnostic(
                    Code: "stage_failed",
                    StageName: stage.StageName,
                    PageNumber: null,
                    ErrorType: "StageFailure",
                    Message: stage.ErrorMessage ?? "Stage failed.",
                    Recoverable: false));
            }
            else if (stage.Status == PipelineStageStatus.Cancelled)
            {
                diagnostics.Add(new PdfConversionDiagnostic(
                    Code: "stage_cancelled",
                    StageName: stage.StageName,
                    PageNumber: null,
                    ErrorType: "StageCancelled",
                    Message: stage.ErrorMessage ?? "Stage cancelled.",
                    Recoverable: false));
            }
        }

        return diagnostics;
    }

    private static PdfConversionArtifactBundle CreateArtifactBundle(
        string batchRunId,
        IReadOnlyList<PdfBatchDocumentResult> documents,
        PdfBatchAggregationSummary aggregation)
    {
        var files = new List<PdfConversionArtifactFile>();
        var manifestDocuments = new List<object>(documents.Count);

        foreach (var document in documents.OrderBy(d => d.Index))
        {
            var slug = BuildDocumentSlug(document.Index, document.FilePath ?? "stream");
            var basePath = $"documents/{slug}";

            var summary = new
            {
                index = document.Index,
                file_path = document.FilePath ?? "stream",
                status = document.Status.ToString().ToLowerInvariant(),
                run_id = document.Conversion?.RunId,
                document_key = document.Conversion?.DocumentKey,
                pipeline_status = document.Conversion?.Pipeline.Status.ToString().ToLowerInvariant(),
                page_count = document.Conversion?.PageCount,
                decode_attempted_page_count = document.Conversion?.DecodeAttemptedPageCount,
                decode_succeeded_page_count = document.Conversion?.DecodeSucceededPageCount
            };
            files.Add(new PdfConversionArtifactFile(
                RelativePath: $"{basePath}/summary.json",
                ContentType: "application/json",
                Content: JsonSerializer.Serialize(summary, DoclingJson.SerializerOptions)));

            var diagnosticsPayload = document.Diagnostics.Select(d => new
            {
                code = d.Code,
                stage_name = d.StageName,
                page_number = d.PageNumber,
                error_type = d.ErrorType,
                message = d.Message,
                recoverable = d.Recoverable
            });
            files.Add(new PdfConversionArtifactFile(
                RelativePath: $"{basePath}/diagnostics.json",
                ContentType: "application/json",
                Content: JsonSerializer.Serialize(diagnosticsPayload, DoclingJson.SerializerOptions)));

            if (document.Conversion is not null)
            {
                files.Add(new PdfConversionArtifactFile(
                    RelativePath: $"{basePath}/pages.segmented.json",
                    ContentType: "application/json",
                    Content: JsonSerializer.Serialize(document.Conversion.Pages, DoclingJson.SerializerOptions)));
            }

            manifestDocuments.Add(new
            {
                index = document.Index,
                file_path = document.FilePath ?? "stream",
                status = document.Status.ToString().ToLowerInvariant(),
                artifact_prefix = basePath
            });
        }

        var manifest = new
        {
            batch_run_id = batchRunId,
            document_count = documents.Count,
            succeeded_count = documents.Count(d => d.Status == PdfBatchDocumentStatus.Succeeded),
            failed_count = documents.Count(d => d.Status == PdfBatchDocumentStatus.Failed),
            skipped_count = documents.Count(d => d.Status == PdfBatchDocumentStatus.Skipped),
            aggregation = new
            {
                document_status_counts = aggregation.DocumentStatusCounts,
                pipeline_status_counts = aggregation.PipelineStatusCounts,
                total_page_count = aggregation.TotalPageCount,
                total_decode_attempted_page_count = aggregation.TotalDecodeAttemptedPageCount,
                total_decode_succeeded_page_count = aggregation.TotalDecodeSucceededPageCount,
                total_diagnostics_count = aggregation.TotalDiagnosticsCount,
                recoverable_diagnostics_count = aggregation.RecoverableDiagnosticsCount,
                non_recoverable_diagnostics_count = aggregation.NonRecoverableDiagnosticsCount,
                diagnostic_code_counts = aggregation.DiagnosticCodeCounts,
                diagnostic_stage_counts = aggregation.DiagnosticStageCounts
            },
            documents = manifestDocuments
        };

        files.Add(new PdfConversionArtifactFile(
            RelativePath: "manifest.json",
            ContentType: "application/json",
            Content: JsonSerializer.Serialize(manifest, DoclingJson.SerializerOptions)));

        return new PdfConversionArtifactBundle
        {
            BatchRunId = batchRunId,
            Files = files.OrderBy(f => f.RelativePath, StringComparer.Ordinal).ToArray()
        };
    }

    private static PdfBatchAggregationSummary CreateBatchAggregation(
        IReadOnlyList<PdfBatchDocumentResult> documents,
        IReadOnlyList<PdfConversionDiagnostic> diagnostics)
    {
        var documentStatusCounts = documents
            .GroupBy(d => d.Status.ToString().ToLowerInvariant(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var pipelineStatusCounts = documents
            .Where(d => d.Conversion is not null)
            .GroupBy(d => d.Conversion!.Pipeline.Status.ToString().ToLowerInvariant(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var diagnosticCodeCounts = diagnostics
            .GroupBy(d => d.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var diagnosticStageCounts = diagnostics
            .Where(d => !string.IsNullOrWhiteSpace(d.StageName))
            .GroupBy(d => d.StageName!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var conversions = documents
            .Where(d => d.Conversion is not null)
            .Select(d => d.Conversion!)
            .ToArray();

        return new PdfBatchAggregationSummary
        {
            DocumentCount = documents.Count,
            SucceededDocumentCount = documents.Count(d => d.Status == PdfBatchDocumentStatus.Succeeded),
            FailedDocumentCount = documents.Count(d => d.Status == PdfBatchDocumentStatus.Failed),
            SkippedDocumentCount = documents.Count(d => d.Status == PdfBatchDocumentStatus.Skipped),
            TotalPageCount = conversions.Sum(c => c.PageCount),
            TotalDecodeAttemptedPageCount = conversions.Sum(c => c.DecodeAttemptedPageCount),
            TotalDecodeSucceededPageCount = conversions.Sum(c => c.DecodeSucceededPageCount),
            TotalDiagnosticsCount = diagnostics.Count,
            RecoverableDiagnosticsCount = diagnostics.Count(d => d.Recoverable),
            NonRecoverableDiagnosticsCount = diagnostics.Count(d => !d.Recoverable),
            DocumentStatusCounts = documentStatusCounts,
            PipelineStatusCounts = pipelineStatusCounts,
            DiagnosticCodeCounts = diagnosticCodeCounts,
            DiagnosticStageCounts = diagnosticStageCounts
        };
    }

    private static async Task<string> PersistArtifactBundleAsync(
        PdfConversionArtifactBundle artifactBundle,
        string outputDirectory,
        bool cleanOutputDirectory,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(outputDirectory);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (cleanOutputDirectory && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.CreateDirectory(root);

        foreach (var file in artifactBundle.Files.OrderBy(f => f.RelativePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidatePath = Path.GetFullPath(
                Path.Combine(root, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidatePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Artifact path escapes output root: {file.RelativePath}");
            }

            var directory = Path.GetDirectoryName(candidatePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(candidatePath, file.Content, cancellationToken).ConfigureAwait(false);
        }

        return root;
    }

    private static string BuildDocumentSlug(int index, string filePath)
    {
        var stem = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = "document";
        }

        var normalized = new string(
            stem.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray());

        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "document";
        }

        return $"{index:D4}-{normalized}";
    }
}

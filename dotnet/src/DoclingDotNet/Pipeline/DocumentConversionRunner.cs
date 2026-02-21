using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoclingDotNet.Backends;
using DoclingDotNet.Models;

namespace DoclingDotNet.Pipeline;

public sealed class DocumentConversionRunner
{
    private readonly DoclingPdfConversionRunner _pdfRunner;
    private readonly Dictionary<string, IDocumentBackend> _backends = new(StringComparer.OrdinalIgnoreCase);

    public DocumentConversionRunner(
        DoclingPdfConversionRunner? pdfRunner = null,
        IEnumerable<IDocumentBackend>? additionalBackends = null)
    {
        _pdfRunner = pdfRunner ?? new DoclingPdfConversionRunner();
        
        var defaultBackends = new IDocumentBackend[]
        {
            new MsWordDocumentBackend(),
            new MsPowerPointDocumentBackend(),
            new MsExcelDocumentBackend(),
            new HtmlDocumentBackend(),
            new XmlDocumentBackend(),
            new CsvDocumentBackend(),
            new MarkdownDocumentBackend(),
            new LatexDocumentBackend(),
            new ImageDocumentBackend(),
            new TextDocumentBackend(),
            new EpubDocumentBackend(),
            new EmlDocumentBackend()
        };

        foreach (var backend in defaultBackends)
        {
            foreach (var ext in backend.SupportedExtensions)
            {
                _backends[ext] = backend;
            }
        }

        if (additionalBackends != null)
        {
            foreach (var backend in additionalBackends)
            {
                foreach (var ext in backend.SupportedExtensions)
                {
                    _backends[ext] = backend;
                }
            }
        }
    }

        public async Task<PdfConversionRunResult> ExecuteAsync(
            PdfConversionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.FilePath) && request.InputStream == null)
            {
                throw new ArgumentException("Either FilePath or InputStream must be provided.");
            }
    
            var extension = !string.IsNullOrWhiteSpace(request.InputFormat) 
                ? request.InputFormat 
                : Path.GetExtension(request.FilePath);
    
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new ArgumentException("Could not determine document format. Provide a FilePath with an extension or specify InputFormat.");
            }
    
            if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return await _pdfRunner.ExecuteAsync(request, cancellationToken);
            }
    
            if (_backends.TryGetValue(extension, out var backend))
            {
                var runId = request.RunId ?? $"run_{Guid.NewGuid():N}";
                var documentKey = request.DocumentKey ?? (request.FilePath != null ? Path.GetFileNameWithoutExtension(request.FilePath) : "stream") + "_" + runId;
    
                try
                {
                    IReadOnlyList<SegmentedPdfPageDto> pages;
                    if (request.InputStream != null)
                    {
                        pages = await backend.ConvertAsync(request.InputStream, cancellationToken);
                    }
                    else
                    {
                        using var stream = File.OpenRead(request.FilePath!);
                        pages = await backend.ConvertAsync(stream, cancellationToken);
                    }
    
                    var pipelineResult = new PipelineRunResult
                    {
                        RunId = runId,
                        Status = PipelineRunStatus.Succeeded,
                        Events = [],
                        StageResults = []
                    };
    
                    return new PdfConversionRunResult
                    {
                        RunId = runId,
                        DocumentKey = documentKey,
                        FilePath = request.FilePath ?? "stream",
                        PageCount = pages.Count,
                        Pages = pages,
                        PageErrors = [],
                        DecodeAttemptedPageCount = pages.Count,
                        DecodeSucceededPageCount = pages.Count,
                        OcrApplied = false,
                        OcrProviderName = null,
                        LayoutInferenceApplied = false,
                        LayoutProviderName = null,
                        LayoutPostprocessingApplied = false,
                        ReadingOrderApplied = false,
                        AnnotationsJson = null,
                        TableOfContentsJson = null,
                        MetaXmlJson = null,
                        Diagnostics = [],
                        Pipeline = pipelineResult
                    };
                }
                catch (Exception ex)
                {
                    var pipelineResult = new PipelineRunResult
                    {
                        RunId = runId,
                        Status = PipelineRunStatus.Failed,
                        Events = [],
                        StageResults =
                        [
                            new PipelineStageResult(
                                RunId: runId,
                                StageName: "backend_convert",
                                Kind: PipelineStageKind.Regular,
                                Status: PipelineStageStatus.Failed,
                                StartedUtc: DateTimeOffset.UtcNow,
                                CompletedUtc: DateTimeOffset.UtcNow,
                                Duration: TimeSpan.Zero,
                                ErrorMessage: ex.Message)
                        ]
                    };
    
                    return new PdfConversionRunResult
                    {
                        RunId = runId,
                        DocumentKey = documentKey,
                        FilePath = request.FilePath ?? "stream",
                        PageCount = 0,
                        Pages = [],
                        PageErrors = [],
                        DecodeAttemptedPageCount = 0,
                        DecodeSucceededPageCount = 0,
                        OcrApplied = false,
                        OcrProviderName = null,
                        LayoutInferenceApplied = false,
                        LayoutProviderName = null,
                        LayoutPostprocessingApplied = false,
                        ReadingOrderApplied = false,
                        AnnotationsJson = null,
                        TableOfContentsJson = null,
                        MetaXmlJson = null,
                        Diagnostics =
                        [
                            new PdfConversionDiagnostic(
                                Code: "backend_failure",
                                StageName: "backend_convert",
                                PageNumber: null,
                                ErrorType: ex.GetType().Name,
                                Message: ex.Message,
                                Recoverable: false)
                        ],
                        Pipeline = pipelineResult
                    };
                }
            }
    
            throw new NotSupportedException($"Unsupported file extension or format hint: {extension}");
        }}
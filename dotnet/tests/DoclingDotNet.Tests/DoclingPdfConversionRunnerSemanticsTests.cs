using System.Text.Json;
using DoclingDotNet.Models;
using DoclingDotNet.Parsing;
using DoclingDotNet.Ocr;
using DoclingDotNet.Pipeline;
using Xunit;

namespace DoclingDotNet.Tests;

public sealed class DoclingPdfConversionRunnerSemanticsTests
{
    [Fact]
    public void CreateDefaultOcrProviders_IncludesTesseract()
    {
        var providers = DoclingPdfConversionRunner.CreateDefaultOcrProviders();
        Assert.Contains(providers, p => string.Equals(p.Name, "tesseract", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_WhenDecodeFails_PropagatesFailureAndStops()
    {
        var fake = new FakeParseSession
        {
            PageCount = 3,
            DecodeFailureOnPage = 1
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fake);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "input.pdf",
            RunId = "run-fail",
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Failed, result.Pipeline.Status);
        Assert.Equal(PdfConversionStageNames.DecodePages, result.Pipeline.FailureStage);
        Assert.Single(result.Pages);
        Assert.Empty(result.PageErrors);
        Assert.Equal(2, result.DecodeAttemptedPageCount);
        Assert.Equal(1, result.DecodeSucceededPageCount);
        Assert.Contains("page 1", result.Pipeline.ErrorMessage);
        Assert.Contains(
            result.Diagnostics,
            d => d.Code == "stage_failed"
                 && d.StageName == PdfConversionStageNames.DecodePages
                 && d.Recoverable == false);
        Assert.Equal(1, fake.UnloadCalls);
        Assert.True(fake.Disposed);
        Assert.Contains(
            result.Pipeline.Events,
            e => e.StageName == PdfConversionStageNames.UnloadDocument && e.Kind == PipelineEventKind.StageCompleted);
        Assert.Contains(
            result.Pipeline.StageResults,
            s => s.StageName == PdfConversionStageNames.UnloadDocument && s.Status == PipelineStageStatus.Succeeded);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTimeoutOccurs_ReturnsTimedOutAndCleansUp()
    {
        var fake = new FakeParseSession
        {
            PageCount = 10,
            DecodeDelay = TimeSpan.FromMilliseconds(40)
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fake);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "input.pdf",
            RunId = "run-timeout",
            Timeout = TimeSpan.FromMilliseconds(50)
        });

        Assert.Equal(PipelineRunStatus.TimedOut, result.Pipeline.Status);
        Assert.Equal(PdfConversionStageNames.DecodePages, result.Pipeline.FailureStage);
        Assert.InRange(result.Pages.Count, 1, 2);
        Assert.Equal(result.Pages.Count, result.DecodeSucceededPageCount);
        Assert.InRange(result.DecodeAttemptedPageCount, 1, 2);
        Assert.Contains(
            result.Diagnostics,
            d => d.Code == "stage_cancelled"
                 && d.StageName == PdfConversionStageNames.DecodePages
                 && d.Recoverable == false);
        Assert.Equal(1, fake.UnloadCalls);
        Assert.True(fake.Disposed);
        Assert.Contains(
            result.Pipeline.Events,
            e => e.StageName == PdfConversionStageNames.DecodePages && e.Kind == PipelineEventKind.StageCancelled);
        Assert.Contains(
            result.Pipeline.Events,
            e => e.StageName == PdfConversionStageNames.UnloadDocument && e.Kind == PipelineEventKind.StageCompleted);
        Assert.Contains(
            result.Pipeline.StageResults,
            s => s.StageName == PdfConversionStageNames.UnloadDocument && s.Status == PipelineStageStatus.Succeeded);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentRuns_IsolateRunIdsAndSessionState()
    {
        var sessions = new List<FakeParseSession>();
        var sync = new object();

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ =>
            {
                var session = new FakeParseSession { PageCount = 1 };
                lock (sync)
                {
                    sessions.Add(session);
                }

                return session;
            });

        var run1 = runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "a.pdf",
            RunId = "run-1",
            Timeout = TimeSpan.FromSeconds(2)
        });
        var run2 = runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "b.pdf",
            RunId = "run-2",
            Timeout = TimeSpan.FromSeconds(2)
        });

        var results = await Task.WhenAll(run1, run2);

        Assert.Equal(2, sessions.Count);
        Assert.All(results, r => Assert.Equal(PipelineRunStatus.Succeeded, r.Pipeline.Status));
        Assert.All(results, r => Assert.All(r.Pipeline.Events, e => Assert.Equal(r.RunId, e.RunId)));
        Assert.All(results, r => Assert.All(r.Pipeline.StageResults, s => Assert.Equal(r.RunId, s.RunId)));
        Assert.All(sessions, s => Assert.Equal(1, s.UnloadCalls));
        Assert.All(sessions, s => Assert.True(s.Disposed));

        Assert.Contains(results, r => r.RunId == "run-1");
        Assert.Contains(results, r => r.RunId == "run-2");
    }

    [Fact]
    public async Task ExecuteAsync_WhenContinueOnPageDecodeError_ReturnsPartialOutputsAndPageErrors()
    {
        var fake = new FakeParseSession
        {
            PageCount = 4,
            DecodeFailureOnPage = 2
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fake);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "input.pdf",
            RunId = "run-continue",
            ContinueOnPageDecodeError = true,
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.Equal(4, result.DecodeAttemptedPageCount);
        Assert.Equal(3, result.DecodeSucceededPageCount);
        Assert.Equal(3, result.Pages.Count);
        var pageError = Assert.Single(result.PageErrors);
        Assert.Equal(2, pageError.PageNumber);
        Assert.Equal("InvalidOperationException", pageError.ErrorType);
        Assert.Contains(
            result.Diagnostics,
            d => d.Code == "decode_page_failed"
                 && d.StageName == PdfConversionStageNames.DecodePages
                 && d.PageNumber == 2
                 && d.Recoverable);
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Code == "stage_failed");
        Assert.Contains(
            result.Pipeline.Events,
            e => e.StageName == PdfConversionStageNames.TransformPages && e.Kind == PipelineEventKind.StageCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMaxPagesProvided_DecodesOnlyRequestedSubset()
    {
        var fake = new FakeParseSession
        {
            PageCount = 10
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fake);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "input.pdf",
            RunId = "run-max-pages",
            MaxPages = 3,
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.Equal(3, result.Pages.Count);
        Assert.Equal(3, result.DecodeAttemptedPageCount);
        Assert.Equal(3, result.DecodeSucceededPageCount);
        Assert.Equal(new[] { 0, 1, 2 }, fake.DecodeCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOcrFallbackEnabled_UsesProviderFallbackChain()
    {
        var fakeSession = new FakeParseSession
        {
            PageCount = 1,
            PageFactory = _ => new SegmentedPdfPageDto
            {
                HasChars = true,
                HasWords = false,
                HasLines = true
            }
        };

        var providers = new IDoclingOcrProvider[]
        {
            new FakeOcrProvider(
                name: "offline",
                priority: 0,
                isAvailable: false,
                process: _ => OcrProcessResult.NoChanges("offline provider not active")),
            new FakeOcrProvider(
                name: "recoverable",
                priority: 1,
                isAvailable: true,
                process: _ => OcrProcessResult.RecoverableFailure("Recoverable", "temporary OCR outage")),
            new FakeOcrProvider(
                name: "tesseract",
                priority: 2,
                isAvailable: true,
                process: req =>
                {
                    var updated = req.Pages
                        .Select(page =>
                        {
                            page.HasWords = true;
                            return page;
                        })
                        .ToArray();
                    return OcrProcessResult.Succeeded(updated, "tesseract OCR completed");
                })
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fakeSession,
            ocrProviders: providers);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "ocr.pdf",
            RunId = "run-ocr-fallback",
            EnableOcrFallback = true,
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.True(result.OcrApplied);
        Assert.Equal("tesseract", result.OcrProviderName);
        Assert.Single(result.Pages);
        Assert.True(result.Pages[0].HasWords);
        Assert.Contains(result.Diagnostics, d => d.Code == "ocr_provider_unavailable");
        Assert.Contains(result.Diagnostics, d => d.Code == "ocr_provider_recoverable_failure");
        Assert.Contains(result.Diagnostics, d => d.Code == "ocr_provider_succeeded");
        Assert.Contains(
            result.Pipeline.Events,
            e => e.StageName == PdfConversionStageNames.ApplyOcrFallback
                 && e.Kind == PipelineEventKind.StageCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOcrRequiredAndProvidersExhausted_FailsPipeline()
    {
        var fakeSession = new FakeParseSession
        {
            PageCount = 1,
            PageFactory = _ => new SegmentedPdfPageDto
            {
                HasChars = false,
                HasWords = false,
                HasLines = false
            }
        };

        var providers = new IDoclingOcrProvider[]
        {
            new FakeOcrProvider(
                name: "recoverable",
                priority: 0,
                isAvailable: true,
                process: _ => OcrProcessResult.RecoverableFailure("Recoverable", "ocr retry later"))
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fakeSession,
            ocrProviders: providers,
            includeDefaultOcrProviders: false);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "ocr-required.pdf",
            RunId = "run-ocr-required",
            EnableOcrFallback = true,
            RequireOcrSuccess = true,
            Timeout = TimeSpan.FromSeconds(5)
        });

        if (result.Pipeline.Status == PipelineRunStatus.Succeeded)
        {
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            throw new Exception("Pipeline succeeded unexpectedly. Result: " + json);
        }

        Assert.Equal(PipelineRunStatus.Failed, result.Pipeline.Status);
        Assert.Equal(PdfConversionStageNames.ApplyOcrFallback, result.Pipeline.FailureStage);
        Assert.False(result.OcrApplied);
        Assert.Null(result.OcrProviderName);
        Assert.Contains(result.Diagnostics, d => d.Code == "ocr_provider_recoverable_failure");
        Assert.Contains(result.Diagnostics, d => d.Code == "ocr_provider_exhausted");
        Assert.Contains(
            result.Diagnostics,
            d => d.Code == "stage_failed"
                 && d.StageName == PdfConversionStageNames.ApplyOcrFallback);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreferredOcrProviderSpecified_UsesPreferredBeforePriority()
    {
        var fakeSession = new FakeParseSession
        {
            PageCount = 1
        };

        var providers = new IDoclingOcrProvider[]
        {
            new FakeOcrProvider(
                name: "priority-first",
                priority: 0,
                isAvailable: true,
                process: _ => OcrProcessResult.Succeeded(message: "priority provider")),
            new FakeOcrProvider(
                name: "preferred-provider",
                priority: 10,
                isAvailable: true,
                process: _ => OcrProcessResult.Succeeded(message: "preferred provider"))
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fakeSession,
            ocrProviders: providers);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "ocr-preferred.pdf",
            RunId = "run-ocr-preferred",
            EnableOcrFallback = true,
            PreferredOcrProviders = ["preferred-provider"],
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.True(result.OcrApplied);
        Assert.Equal("preferred-provider", result.OcrProviderName);
        Assert.Contains(
            result.Diagnostics,
            d => d.Code == "ocr_provider_succeeded"
                 && d.Message.Contains("preferred provider", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllowedOcrProvidersConfigured_FiltersProviderChain()
    {
        var fakeSession = new FakeParseSession
        {
            PageCount = 1
        };

        var providers = new IDoclingOcrProvider[]
        {
            new FakeOcrProvider(
                name: "blocked-provider",
                priority: 0,
                isAvailable: true,
                process: _ => OcrProcessResult.Succeeded(message: "blocked provider should not execute")),
            new FakeOcrProvider(
                name: "allowed-provider",
                priority: 1,
                isAvailable: true,
                process: _ => OcrProcessResult.Succeeded(message: "allowed provider executed"))
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fakeSession,
            ocrProviders: providers);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "ocr-allowlist.pdf",
            RunId = "run-ocr-allowlist",
            EnableOcrFallback = true,
            AllowedOcrProviders = ["allowed-provider"],
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.True(result.OcrApplied);
        Assert.Equal("allowed-provider", result.OcrProviderName);
        Assert.Contains(result.Diagnostics, d => d.Code == "ocr_provider_not_allowed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTrustPolicyBlocksProvider_UsesTrustedInternalProvider()
    {
        var fakeSession = new FakeParseSession
        {
            PageCount = 1
        };

        var providers = new IDoclingOcrProvider[]
        {
            new FakeOcrProvider(
                name: "untrusted-internal",
                priority: 0,
                isAvailable: true,
                process: _ => OcrProcessResult.Succeeded(message: "should be blocked"),
                isTrusted: false,
                isExternal: false),
            new FakeOcrProvider(
                name: "trusted-external",
                priority: 1,
                isAvailable: true,
                process: _ => OcrProcessResult.Succeeded(message: "should be blocked"),
                isTrusted: true,
                isExternal: true),
            new FakeOcrProvider(
                name: "trusted-internal",
                priority: 2,
                isAvailable: true,
                process: _ => OcrProcessResult.Succeeded(message: "trusted internal executed"),
                isTrusted: true,
                isExternal: false)
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fakeSession,
            ocrProviders: providers);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "ocr-trust-policy.pdf",
            RunId = "run-ocr-trust-policy",
            EnableOcrFallback = true,
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.Equal("trusted-internal", result.OcrProviderName);
        Assert.Contains(result.Diagnostics, d => d.Code == "ocr_provider_untrusted");
        Assert.Contains(result.Diagnostics, d => d.Code == "ocr_provider_external_blocked");
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderDoesNotSupportLanguageSelection_SkipsToCapableProvider()
    {
        var fakeSession = new FakeParseSession
        {
            PageCount = 1
        };

        var providers = new IDoclingOcrProvider[]
        {
            new FakeOcrProvider(
                name: "language-agnostic",
                priority: 0,
                isAvailable: true,
                process: _ => OcrProcessResult.Succeeded(message: "should be skipped"),
                supportsLanguageSelection: false),
            new FakeOcrProvider(
                name: "language-capable",
                priority: 1,
                isAvailable: true,
                process: _ => OcrProcessResult.Succeeded(message: "language-capable provider executed"),
                supportsLanguageSelection: true)
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fakeSession,
            ocrProviders: providers);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "ocr-language-selection.pdf",
            RunId = "run-ocr-language-selection",
            EnableOcrFallback = true,
            OcrLanguage = "eng",
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.Equal("language-capable", result.OcrProviderName);
        Assert.Contains(result.Diagnostics, d => d.Code == "ocr_provider_capability_mismatch");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoProvidersInjected_UsesDefaultTesseractRegistration()
    {
        var fakeSession = new FakeParseSession
        {
            PageCount = 1
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fakeSession);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "ocr-default-provider.pdf",
            RunId = "run-ocr-default-provider",
            EnableOcrFallback = true,
            AllowedOcrProviders = ["non-default-provider"],
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.Contains(
            result.Diagnostics,
            d => d.Code == "ocr_provider_not_allowed"
                 && d.Message.Contains("'tesseract'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Code == "ocr_provider_exhausted");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDefaultProvidersDisabled_ReportsNoConfiguredProviders()
    {
        var fakeSession = new FakeParseSession
        {
            PageCount = 1
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fakeSession,
            includeDefaultOcrProviders: false);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "ocr-no-default-provider.pdf",
            RunId = "run-ocr-no-default-provider",
            EnableOcrFallback = true,
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.Contains(
            result.Diagnostics,
            d => d.Code == "ocr_provider_unavailable"
                 && d.ErrorType == "NoOcrProviderConfigured");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTransformPagesProvided_AppliesTransformStage()
    {
        var fake = new FakeParseSession
        {
            PageCount = 3,
            PageFactory = pageNumber => new SegmentedPdfPageDto
            {
                HasChars = pageNumber % 2 == 0,
                HasWords = false,
                HasLines = false
            }
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fake);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "input.pdf",
            RunId = "run-transform",
            TransformPages = input => input.Where(page => page.HasChars).ToList(),
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.Equal(3, result.DecodeAttemptedPageCount);
        Assert.Equal(3, result.DecodeSucceededPageCount);
        Assert.Equal(2, result.Pages.Count);
        Assert.All(result.Pages, page => Assert.True(page.HasChars));
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Code == "stage_failed" || d.Code == "stage_cancelled");
        Assert.Contains(
            result.Pipeline.Events,
            e => e.StageName == PdfConversionStageNames.TransformPages && e.Kind == PipelineEventKind.StageCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_WhenContextExtractionEnabled_CollectsAnnotationsTocAndMeta()
    {
        var fake = new FakeParseSession
        {
            PageCount = 1,
            AnnotationsJson = "{\"ann\":1}",
            TableOfContentsJson = "{\"toc\":1}",
            MetaXmlJson = "{\"meta\":1}"
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fake);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "input.pdf",
            RunId = "run-context",
            IncludeAnnotations = true,
            IncludeTableOfContents = true,
            IncludeMetaXml = true,
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.Equal("{\"ann\":1}", result.AnnotationsJson);
        Assert.Equal("{\"toc\":1}", result.TableOfContentsJson);
        Assert.Equal("{\"meta\":1}", result.MetaXmlJson);
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Code == "stage_failed" || d.Code == "stage_cancelled");
        Assert.Equal(1, fake.AnnotationCalls);
        Assert.Equal(1, fake.TocCalls);
        Assert.Equal(1, fake.MetaCalls);
        Assert.Contains(
            result.Pipeline.Events,
            e => e.StageName == PdfConversionStageNames.ExtractAnnotations && e.Kind == PipelineEventKind.StageCompleted);
        Assert.Contains(
            result.Pipeline.Events,
            e => e.StageName == PdfConversionStageNames.ExtractTableOfContents && e.Kind == PipelineEventKind.StageCompleted);
        Assert.Contains(
            result.Pipeline.Events,
            e => e.StageName == PdfConversionStageNames.ExtractMetaXml && e.Kind == PipelineEventKind.StageCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_WhenContextExtractionFails_PropagatesFailureAndStillCleansUp()
    {
        var fake = new FakeParseSession
        {
            PageCount = 1,
            FailOnMetaXml = true
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fake);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "input.pdf",
            RunId = "run-context-fail",
            IncludeAnnotations = true,
            IncludeMetaXml = true,
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Failed, result.Pipeline.Status);
        Assert.Equal(PdfConversionStageNames.ExtractMetaXml, result.Pipeline.FailureStage);
        Assert.Single(result.Pages);
        Assert.Equal("{}", result.AnnotationsJson);
        Assert.Null(result.MetaXmlJson);
        Assert.Contains(
            result.Diagnostics,
            d => d.Code == "stage_failed"
                 && d.StageName == PdfConversionStageNames.ExtractMetaXml
                 && d.Recoverable == false);
        Assert.Equal(1, fake.UnloadCalls);
        Assert.True(fake.Disposed);
        Assert.Contains(
            result.Pipeline.Events,
            e => e.StageName == PdfConversionStageNames.UnloadDocument && e.Kind == PipelineEventKind.StageCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTransformReturnsEmpty_SucceedsWithRecoverableDiagnostic()
    {
        var fake = new FakeParseSession
        {
            PageCount = 2
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fake);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "input.pdf",
            RunId = "run-transform-empty",
            TransformPages = _ => [],
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.Empty(result.Pages);
        Assert.Contains(
            result.Diagnostics,
            d => d.Code == "transform_returned_empty"
                 && d.StageName == PdfConversionStageNames.TransformPages
                 && d.Recoverable);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTransformReturnsNull_FailsWithTransformDiagnostic()
    {
        var fake = new FakeParseSession
        {
            PageCount = 1
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fake);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "input.pdf",
            RunId = "run-transform-null",
#pragma warning disable CS8603
            TransformPages = _ => null,
#pragma warning restore CS8603
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Failed, result.Pipeline.Status);
        Assert.Equal(PdfConversionStageNames.TransformPages, result.Pipeline.FailureStage);
        Assert.Contains(
            result.Diagnostics,
            d => d.Code == "stage_failed"
                 && d.StageName == PdfConversionStageNames.TransformPages
                 && d.Recoverable == false);
        Assert.Equal(1, fake.UnloadCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTransformThrows_FailsWithTransformDiagnostic()
    {
        var fake = new FakeParseSession
        {
            PageCount = 1
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => fake);

        var result = await runner.ExecuteAsync(new PdfConversionRequest
        {
            FilePath = "input.pdf",
            RunId = "run-transform-throws",
            TransformPages = _ => throw new InvalidOperationException("transform boom"),
            Timeout = TimeSpan.FromSeconds(2)
        });

        Assert.Equal(PipelineRunStatus.Failed, result.Pipeline.Status);
        Assert.Equal(PdfConversionStageNames.TransformPages, result.Pipeline.FailureStage);
        Assert.Contains(
            result.Diagnostics,
            d => d.Code == "stage_failed"
                 && d.StageName == PdfConversionStageNames.TransformPages
                 && d.Recoverable == false);
        Assert.Equal(1, fake.UnloadCalls);
    }

    [Fact]
    public async Task ExecuteBatchAsync_WhenContinueOnDocumentFailureIsFalse_SkipsRemainingDocuments()
    {
        var first = new FakeParseSession
        {
            PageCount = 1,
            DecodeFailureOnPage = 0
        };
        var second = new FakeParseSession
        {
            PageCount = 1
        };
        var sessions = new Queue<IDoclingParseSession>([first, second]);

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => sessions.Dequeue());

        var result = await runner.ExecuteBatchAsync(new PdfBatchConversionRequest
        {
            BatchRunId = "batch-stop",
            ContinueOnDocumentFailure = false,
            Documents =
            [
                new PdfConversionRequest
                {
                    FilePath = "first.pdf",
                    RunId = "run-first",
                    Timeout = TimeSpan.FromSeconds(2)
                },
                new PdfConversionRequest
                {
                    FilePath = "second.pdf",
                    RunId = "run-second",
                    Timeout = TimeSpan.FromSeconds(2)
                }
            ]
        });

        Assert.Equal("batch-stop", result.BatchRunId);
        Assert.Equal(2, result.Documents.Count);
        Assert.Equal(PdfBatchDocumentStatus.Failed, result.Documents[0].Status);
        Assert.Equal(PdfBatchDocumentStatus.Skipped, result.Documents[1].Status);
        Assert.Contains(result.Documents[1].Diagnostics, d => d.Code == "batch_document_skipped");
        Assert.Single(sessions);
        Assert.Contains(result.ArtifactBundle.Files, f => f.RelativePath == "manifest.json");
    }

    [Fact]
    public async Task ExecuteBatchAsync_WhenSingleDocumentThrows_RecordsFailureAndContinues()
    {
        var session = new FakeParseSession
        {
            PageCount = 1
        };

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => session);

        var result = await runner.ExecuteBatchAsync(new PdfBatchConversionRequest
        {
            BatchRunId = "batch-continue",
            ContinueOnDocumentFailure = true,
            Documents =
            [
                new PdfConversionRequest
                {
                    FilePath = string.Empty,
                    RunId = "run-bad",
                    Timeout = TimeSpan.FromSeconds(2)
                },
                new PdfConversionRequest
                {
                    FilePath = "good.pdf",
                    RunId = "run-good",
                    Timeout = TimeSpan.FromSeconds(2)
                }
            ]
        });

        Assert.Equal(PdfBatchDocumentStatus.Failed, result.Documents[0].Status);
        Assert.Contains(result.Documents[0].Diagnostics, d => d.Code == "batch_document_unhandled_exception");
        Assert.Equal(PdfBatchDocumentStatus.Succeeded, result.Documents[1].Status);
    }

    [Fact]
    public async Task ExecuteBatchAsync_ProducesDeterministicArtifactContract()
    {
        var sessions = new Queue<IDoclingParseSession>(
        [
            new FakeParseSession { PageCount = 1 },
            new FakeParseSession { PageCount = 1 }
        ]);

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => sessions.Dequeue());

        var result = await runner.ExecuteBatchAsync(new PdfBatchConversionRequest
        {
            BatchRunId = "batch-artifacts",
            Documents =
            [
                new PdfConversionRequest
                {
                    FilePath = "B File.pdf",
                    RunId = "run-b1",
                    Timeout = TimeSpan.FromSeconds(2)
                },
                new PdfConversionRequest
                {
                    FilePath = "a-file.pdf",
                    RunId = "run-a1",
                    Timeout = TimeSpan.FromSeconds(2)
                }
            ]
        });

        var ordered = result.ArtifactBundle.Files
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .Select(f => f.RelativePath)
            .ToArray();

        Assert.Equal(7, result.ArtifactBundle.Files.Count);
        Assert.Equal(ordered, result.ArtifactBundle.Files.Select(f => f.RelativePath).ToArray());

        var manifestFile = Assert.Single(result.ArtifactBundle.Files, f => f.RelativePath == "manifest.json");
        using var manifest = JsonDocument.Parse(manifestFile.Content);
        var root = manifest.RootElement;
        Assert.Equal("batch-artifacts", root.GetProperty("batch_run_id").GetString());
        Assert.Equal(2, root.GetProperty("document_count").GetInt32());
        Assert.Equal(2, root.GetProperty("succeeded_count").GetInt32());
        Assert.Equal(0, root.GetProperty("failed_count").GetInt32());
        Assert.Equal(0, root.GetProperty("skipped_count").GetInt32());
        Assert.Equal(2, root.GetProperty("documents").GetArrayLength());

        Assert.Contains(result.ArtifactBundle.Files, f => f.RelativePath == "documents/0000-b_file/summary.json");
        Assert.Contains(result.ArtifactBundle.Files, f => f.RelativePath == "documents/0000-b_file/pages.segmented.json");
        Assert.Contains(result.ArtifactBundle.Files, f => f.RelativePath == "documents/0001-a_file/summary.json");
    }

    [Fact]
    public async Task ExecuteBatchAsync_ProducesBatchAggregationSummary()
    {
        var first = new FakeParseSession
        {
            PageCount = 1,
            DecodeFailureOnPage = 0
        };
        var second = new FakeParseSession
        {
            PageCount = 1
        };
        var sessions = new Queue<IDoclingParseSession>([first, second]);

        var runner = new DoclingPdfConversionRunner(
            sessionFactory: _ => sessions.Dequeue());

        var result = await runner.ExecuteBatchAsync(new PdfBatchConversionRequest
        {
            BatchRunId = "batch-aggregation",
            ContinueOnDocumentFailure = false,
            Documents =
            [
                new PdfConversionRequest
                {
                    FilePath = "first.pdf",
                    RunId = "run-first",
                    Timeout = TimeSpan.FromSeconds(2)
                },
                new PdfConversionRequest
                {
                    FilePath = "second.pdf",
                    RunId = "run-second",
                    Timeout = TimeSpan.FromSeconds(2)
                }
            ]
        });

        Assert.Equal(2, result.Aggregation.DocumentCount);
        Assert.Equal(0, result.Aggregation.SucceededDocumentCount);
        Assert.Equal(1, result.Aggregation.FailedDocumentCount);
        Assert.Equal(1, result.Aggregation.SkippedDocumentCount);
        Assert.Equal(1, result.Aggregation.TotalPageCount);
        Assert.Equal(1, result.Aggregation.TotalDecodeAttemptedPageCount);
        Assert.Equal(0, result.Aggregation.TotalDecodeSucceededPageCount);
        Assert.Equal(2, result.Aggregation.TotalDiagnosticsCount);
        Assert.Equal(1, result.Aggregation.RecoverableDiagnosticsCount);
        Assert.Equal(1, result.Aggregation.NonRecoverableDiagnosticsCount);
        Assert.Equal(1, result.Aggregation.DocumentStatusCounts["failed"]);
        Assert.Equal(1, result.Aggregation.DocumentStatusCounts["skipped"]);
        Assert.Equal(1, result.Aggregation.PipelineStatusCounts["failed"]);
        Assert.Equal(1, result.Aggregation.DiagnosticCodeCounts["stage_failed"]);
        Assert.Equal(1, result.Aggregation.DiagnosticCodeCounts["batch_document_skipped"]);
        Assert.Equal(1, result.Aggregation.DiagnosticStageCounts[PdfConversionStageNames.DecodePages]);

        var manifestFile = Assert.Single(result.ArtifactBundle.Files, f => f.RelativePath == "manifest.json");
        using var manifest = JsonDocument.Parse(manifestFile.Content);
        var aggregation = manifest.RootElement.GetProperty("aggregation");
        Assert.Equal(2, aggregation.GetProperty("total_diagnostics_count").GetInt32());
        Assert.Equal(1, aggregation.GetProperty("recoverable_diagnostics_count").GetInt32());
        Assert.Equal(1, aggregation.GetProperty("non_recoverable_diagnostics_count").GetInt32());
    }

    [Fact]
    public async Task ExecuteBatchAsync_WhenArtifactOutputDirectoryProvided_PersistsBundleToDisk()
    {
        var sessions = new Queue<IDoclingParseSession>(
        [
            new FakeParseSession { PageCount = 1 }
        ]);

        var outputRoot = Path.Combine(
            Path.GetTempPath(),
            $"doclingdotnet-artifacts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputRoot);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "stale.txt"), "stale");

        try
        {
            var runner = new DoclingPdfConversionRunner(
                sessionFactory: _ => sessions.Dequeue());

            var result = await runner.ExecuteBatchAsync(new PdfBatchConversionRequest
            {
                BatchRunId = "batch-persist",
                ArtifactOutputDirectory = outputRoot,
                CleanArtifactOutputDirectory = true,
                Documents =
                [
                    new PdfConversionRequest
                    {
                        FilePath = "persist.pdf",
                        RunId = "run-persist",
                        Timeout = TimeSpan.FromSeconds(2)
                    }
                ]
            });

            Assert.NotNull(result.PersistedArtifactDirectory);
            Assert.Equal(Path.GetFullPath(outputRoot), result.PersistedArtifactDirectory);
            Assert.False(File.Exists(Path.Combine(outputRoot, "stale.txt")));

            foreach (var artifactFile in result.ArtifactBundle.Files)
            {
                var diskPath = Path.Combine(
                    outputRoot,
                    artifactFile.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(diskPath), $"Expected artifact file not found: {diskPath}");
            }

            var manifestFromBundle = Assert.Single(
                result.ArtifactBundle.Files,
                f => f.RelativePath == "manifest.json").Content;
            var manifestFromDisk = await File.ReadAllTextAsync(Path.Combine(outputRoot, "manifest.json"));
            Assert.Equal(manifestFromBundle, manifestFromDisk);
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private sealed class FakeOcrProvider : IDoclingOcrProvider
    {
        private readonly Func<OcrProcessRequest, OcrProcessResult> _process;

        public FakeOcrProvider(
            string name,
            int priority,
            bool isAvailable,
            Func<OcrProcessRequest, OcrProcessResult> process,
            bool supportsLanguageSelection = true,
            bool supportsIncrementalPageProcessing = false,
            bool isTrusted = true,
            bool isExternal = false)
        {
            Name = name;
            Priority = priority;
            _process = process;
            IsProviderAvailable = isAvailable;
            Capabilities = new OcrProviderCapabilities
            {
                SupportsLanguageSelection = supportsLanguageSelection,
                SupportsIncrementalPageProcessing = supportsIncrementalPageProcessing,
                IsTrusted = isTrusted,
                IsExternal = isExternal
            };
        }

        public string Name { get; }

        public int Priority { get; }

        public OcrProviderCapabilities Capabilities { get; }

        private bool IsProviderAvailable { get; }

        public bool IsAvailable()
        {
            return IsProviderAvailable;
        }

        public Task<OcrProcessResult> ProcessAsync(
            OcrProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_process(request));
        }
    }

    private sealed class FakeParseSession : IDoclingParseSession
    {
        public int PageCount { get; init; } = 1;

        public int? DecodeFailureOnPage { get; init; }

        public TimeSpan DecodeDelay { get; init; } = TimeSpan.Zero;

        public Func<int, SegmentedPdfPageDto>? PageFactory { get; init; }

        public List<int> DecodeCalls { get; } = [];

        public string AnnotationsJson { get; init; } = "{}";

        public string TableOfContentsJson { get; init; } = "{}";

        public string MetaXmlJson { get; init; } = "{}";

        public bool FailOnMetaXml { get; init; }

        public int AnnotationCalls { get; private set; }

        public int TocCalls { get; private set; }

        public int MetaCalls { get; private set; }

        public int UnloadCalls { get; private set; }

        public bool Disposed { get; private set; }

        public void SetResourcesDir(string resourcesDir)
        {
        }

        public void LoadDocument(string key, string filePath, string? password = null)
        {
        }

        public void LoadDocumentFromBytes(string key, byte[] bytes, string? description = null, string? password = null)
        {
        }

        public void UnloadDocument(string key)
        {
            UnloadCalls++;
        }

        public int GetPageCount(string key)
        {
            return PageCount;
        }

        public SegmentedPdfPageDto DecodeSegmentedPage(string key, int pageNumber)
        {
            DecodeCalls.Add(pageNumber);

            if (DecodeDelay > TimeSpan.Zero)
            {
                Thread.Sleep(DecodeDelay);
            }

            if (DecodeFailureOnPage.HasValue && pageNumber == DecodeFailureOnPage.Value)
            {
                throw new InvalidOperationException("decode failed");
            }

            return PageFactory?.Invoke(pageNumber) ?? new SegmentedPdfPageDto
            {
                HasChars = true,
                HasWords = false,
                HasLines = false
            };
        }

        public string GetAnnotationsJson(string key)
        {
            AnnotationCalls++;
            return AnnotationsJson;
        }

        public string GetTableOfContentsJson(string key)
        {
            TocCalls++;
            return TableOfContentsJson;
        }

        public string GetMetaXmlJson(string key)
        {
            MetaCalls++;
            if (FailOnMetaXml)
            {
                throw new InvalidOperationException("meta extraction failed");
            }

            return MetaXmlJson;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}

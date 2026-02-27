#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoclingDotNet.Asr;
using DoclingDotNet.Models;

namespace DoclingDotNet.Pipeline;

public static class AudioConversionStageNames
{
    public const string InitSession = "init_session";
    public const string LoadAudio = "load_audio";
    public const string TranscribeAudio = "transcribe_audio";
    public const string HydrateDocument = "hydrate_document";
}

/// <summary>
/// Declares the format of the audio input so the pipeline knows whether transcoding is needed.
/// </summary>
public enum AudioInputFormat
{
    /// <summary>
    /// The input is an arbitrary audio format (MP3, M4A, WAV, FLAC, etc.).
    /// The pipeline will transcode it to 16kHz 16-bit Mono PCM via FFmpeg before passing to Whisper.
    /// This is the safe default for any input source.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// The caller guarantees the input is already 16kHz 16-bit Mono PCM WAV.
    /// The pipeline will pass it directly to Whisper without transcoding.
    /// Use this only when you control the audio source and know its exact format.
    /// </summary>
    RawPcm16kMono = 1
}

public sealed class AudioConversionRequest
{
    public string? FilePath { get; init; }
    public Stream? InputStream { get; init; }

    /// <summary>
    /// Declares the format of the audio input. Defaults to <see cref="AudioInputFormat.Auto"/>,
    /// which transcodes all inputs (files and streams) to Whisper-compatible PCM via FFmpeg.
    /// Set to <see cref="AudioInputFormat.RawPcm16kMono"/> only if the input is already 16kHz 16-bit Mono PCM.
    /// </summary>
    public AudioInputFormat InputFormat { get; init; } = AudioInputFormat.Auto;

    public string? RunId { get; init; }
    public string? DocumentKey { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(300);
    public IDoclingAsrProvider? AsrProvider { get; init; }
}

public sealed class AudioConversionRunResult
{
    public required string RunId { get; init; }
    public required string DocumentKey { get; init; }
    public required string FilePath { get; init; }
    public required IReadOnlyList<SegmentedPdfPageDto> Pages { get; init; }
    public required string AsrProviderName { get; init; }
    public required IReadOnlyList<PdfConversionDiagnostic> Diagnostics { get; init; }
    public required PipelineRunResult Pipeline { get; init; }
}

public sealed class DoclingAudioConversionRunner
{
    private readonly PipelineExecutor _pipelineExecutor;

    public DoclingAudioConversionRunner(PipelineExecutor? pipelineExecutor = null)
    {
        _pipelineExecutor = pipelineExecutor ?? new PipelineExecutor();
    }

    public async Task<AudioConversionRunResult> ExecuteAsync(
        AudioConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.FilePath) && request.InputStream == null)
        {
            throw new ArgumentException("Either FilePath or InputStream must be provided.", nameof(request));
        }

        var runId = request.RunId ?? $"run_{Guid.NewGuid():N}";
        var documentKey = request.DocumentKey ?? (request.FilePath ?? "stream");
        var diagnostics = new List<PdfConversionDiagnostic>();
        var pages = new List<SegmentedPdfPageDto>();
        string? providerName = null;

        // Validate FilePath to prevent path traversal and UNC-based attacks.
        if (!string.IsNullOrWhiteSpace(request.FilePath))
        {
            var fullPath = Path.GetFullPath(request.FilePath);
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
            {
                throw new ArgumentException("UNC paths are not permitted for audio input.", nameof(request));
            }

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Audio file not found: {fullPath}", fullPath);
            }
        }

        Stream? audioStream = null;
        AsrExecutionResult? asrResult = null;

        try
        {
            var stages = new List<PipelineStageDefinition>
            {
                new()
                {
                    Name = AudioConversionStageNames.InitSession,
                    ExecuteAsync = (_, _) =>
                    {
                        if (request.AsrProvider == null)
                        {
                            diagnostics.Add(new PdfConversionDiagnostic(
                                "asr_provider_missing",
                                AudioConversionStageNames.InitSession,
                                null,
                                "ProviderConfigurationError",
                                "No ASR Provider was configured for this audio runner.",
                                false));
                            throw new InvalidOperationException("An ASR Provider must be configured.");
                        }

                        if (!request.AsrProvider.IsAvailable())
                        {
                            throw new InvalidOperationException($"The ASR provider '{request.AsrProvider.Name}' is not available (e.g., missing model weights).");
                        }

                        return Task.CompletedTask;
                    }
                },
                new()
                {
                    Name = AudioConversionStageNames.LoadAudio,
                    ExecuteAsync = async (_, token) =>
                    {
                        Stream rawInput;
                        if (request.InputStream != null)
                        {
                            rawInput = request.InputStream;
                        }
                        else
                        {
                            rawInput = File.OpenRead(request.FilePath!);
                        }

                        if (request.InputFormat == AudioInputFormat.RawPcm16kMono)
                        {
                            // Caller guarantees this is already 16kHz 16-bit Mono PCM — skip transcoding.
                            audioStream = rawInput;
                        }
                        else
                        {
                            // Transcode any format (MP3, M4A, WAV, FLAC, etc.) to Whisper-compatible PCM via FFmpeg.
                            audioStream = await AudioTranscoder.NormalizeToWhisperPcmAsync(rawInput, token).ConfigureAwait(false);

                            // If we opened a file stream for transcoding, dispose it now — the output is a new MemoryStream.
                            if (request.InputStream == null)
                            {
                                rawInput.Dispose();
                            }
                        }
                    }
                },
                new()
                {
                    Name = AudioConversionStageNames.TranscribeAudio,
                    ExecuteAsync = async (_, token) =>
                    {
                        if (audioStream == null) throw new InvalidOperationException("Audio stream not loaded.");

                        try
                        {
                            asrResult = await request.AsrProvider!.ProcessAudioAsync(audioStream, token).ConfigureAwait(false);
                            providerName = asrResult.ProviderName;
                            
                            diagnostics.Add(new PdfConversionDiagnostic(
                                "asr_transcription_success",
                                AudioConversionStageNames.TranscribeAudio,
                                null,
                                "Success",
                                $"Successfully transcribed {asrResult.Segments.Count} segments using {providerName}.",
                                true));
                        }
                        catch (Exception ex)
                        {
                            diagnostics.Add(new PdfConversionDiagnostic(
                                "asr_transcription_failed",
                                AudioConversionStageNames.TranscribeAudio,
                                null,
                                ex.GetType().Name,
                                ex.Message,
                                false));
                            throw;
                        }
                    }
                },
                new()
                {
                    Name = AudioConversionStageNames.HydrateDocument,
                    ExecuteAsync = (_, _) =>
                    {
                        if (asrResult == null) return Task.CompletedTask;

                        // Docling handles audio by mapping it to a single pseudo-page of text elements
                        var cells = new List<PdfTextCellDto>(asrResult.Segments.Count);
                        long index = 0;

                        foreach (var segment in asrResult.Segments)
                        {
                            var cell = new PdfTextCellDto
                            {
                                Index = index++,
                                Text = segment.Text,
                                Confidence = 1.0, // Whisper doesn't emit precise word confidence in default track
                                RenderingMode = 0,
                                FromOcr = false,
                                Source = new TrackSourceDto
                                {
                                    StartTime = segment.StartTime,
                                    EndTime = segment.EndTime,
                                    Voice = segment.Voice
                                }
                            };
                            cells.Add(cell);
                        }

                        var page = new SegmentedPdfPageDto
                        {
                            Dimension = new PdfPageGeometryDto
                            {
                                Rect = new BoundingRectangleDto { RX1 = 0, RY1 = 0, RX2 = 0, RY2 = 0 }
                            },
                            TextlineCells = cells,
                            HasLines = cells.Count > 0,
                            HasWords = false // WordCells is not populated in the audio path
                        };

                        pages.Add(page);
                        return Task.CompletedTask;
                    }
                },
                new()
                {
                    Name = "unload_audio",
                    Kind = PipelineStageKind.Cleanup,
                    ExecuteAsync = (_, _) =>
                    {
                        if (request.InputStream == null && audioStream != null)
                        {
                            // We created the memory stream, we should dispose it.
                            audioStream.Dispose();
                        }
                        return Task.CompletedTask;
                    }
                }
            };

            var pipelineResult = await _pipelineExecutor.ExecuteAsync(runId, stages, request.Timeout, cancellationToken).ConfigureAwait(false);

            return new AudioConversionRunResult
            {
                RunId = runId,
                DocumentKey = documentKey,
                FilePath = request.FilePath ?? "stream",
                Pages = pages,
                AsrProviderName = providerName ?? "Unknown",
                Diagnostics = diagnostics,
                Pipeline = pipelineResult
            };
        }
        catch (OperationCanceledException)
        {
            diagnostics.Add(new PdfConversionDiagnostic("timeout", null, null, "Timeout", "The audio pipeline timed out.", false));
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Audio pipeline failed for document '{documentKey}': {ex.Message}", ex);
        }
    }
}

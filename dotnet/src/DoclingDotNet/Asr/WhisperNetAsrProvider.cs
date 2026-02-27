#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;

namespace DoclingDotNet.Asr;

/// <summary>
/// A native .NET wrapper around whisper.cpp via Whisper.net to process audio
/// transcriptions synchronously or asynchronously without Python overhead.
/// </summary>
public sealed class WhisperNetAsrProvider : IDoclingAsrProvider, IDisposable
{
    private readonly WhisperFactory _whisperFactory;
    private readonly WhisperProcessor _processor;
    private readonly string _modelPath;
    private readonly string _language;
    
    public string Name => "Whisper.net (whisper.cpp native)";

    public WhisperNetAsrProvider(string modelPath, string language = "en")
    {
        _modelPath = modelPath;
        _language = language;
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"The specified Whisper GGML model was not found: {modelPath}");
        }

        _whisperFactory = WhisperFactory.FromPath(modelPath);
        _processor = _whisperFactory.CreateBuilder()
            .WithLanguage(language)
            .Build();
    }

    public bool IsAvailable()
    {
        return File.Exists(_modelPath);
    }

    public async Task<AsrExecutionResult> ProcessAudioAsync(Stream audioStream, CancellationToken cancellationToken = default)
    {
        var segments = new List<AsrSegment>();

        await foreach (var result in _processor.ProcessAsync(audioStream, cancellationToken))
        {
            segments.Add(new AsrSegment(
                StartTime: result.Start.TotalSeconds,
                EndTime: result.End.TotalSeconds,
                Text: result.Text,
                Voice: null // Whisper.net does not currently support speaker diarization out of the box
            ));
        }

        return new AsrExecutionResult(segments, Name, _language);
    }

    public void Dispose()
    {
        _processor.Dispose();
        _whisperFactory.Dispose();
    }
}

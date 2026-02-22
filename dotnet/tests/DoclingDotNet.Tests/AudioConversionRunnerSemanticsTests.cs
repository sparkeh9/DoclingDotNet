using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoclingDotNet.Asr;
using DoclingDotNet.Pipeline;

using Xunit;

namespace DoclingDotNet.Tests;

public class AudioConversionRunnerSemanticsTests
{
    private readonly string _assetsDir;
    private readonly string _audioSamplePath;
    private readonly string _modelPath;

    public AudioConversionRunnerSemanticsTests()
    {
        _assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
        _audioSamplePath = Path.Combine(_assetsDir, "audio_sample.wav");
        _modelPath = Path.Combine(_assetsDir, "ggml-tiny.en.bin");
    }

    [Theory]
    [InlineData("audio_sample.wav")]
    [InlineData("audio_sample.mp3")]
    public async Task ExecuteAsync_WhenGivenValidAudio_ShouldTranscribeAndHydrateDocument(string fileName)
    {
        var inputFilePath = Path.Combine(_assetsDir, fileName);

        // Skip if model hasn't been downloaded yet
        if (!File.Exists(_modelPath)) return;

        if (!File.Exists(inputFilePath)) return;

        using var provider = new WhisperNetAsrProvider(_modelPath);

        var request = new AudioConversionRequest
        {
            FilePath = inputFilePath,
            AsrProvider = provider
        };

        var runner = new DoclingAudioConversionRunner();
        var result = await runner.ExecuteAsync(request);

        // Assert Runner Status
        Assert.Equal(PipelineRunStatus.Succeeded, result.Pipeline.Status);
        Assert.DoesNotContain(result.Diagnostics, d => !d.Recoverable);
        Assert.Equal("Whisper.net (whisper.cpp native)", result.AsrProviderName);

        // Assert Document Hydration Parity
        Assert.Single(result.Pages);
        var pseudoPage = result.Pages[0];

        Assert.True(pseudoPage.HasLines);
        Assert.True(pseudoPage.TextlineCells.Count > 0);

        // Assert TrackSource Parity
        var firstCell = pseudoPage.TextlineCells.First();
        Assert.False(string.IsNullOrWhiteSpace(firstCell.Text));
        Assert.NotNull(firstCell.Source);
        Assert.NotNull(firstCell.Source.StartTime);
        Assert.NotNull(firstCell.Source.EndTime);
        Assert.True(firstCell.Source.EndTime > firstCell.Source.StartTime.Value);
    }
}

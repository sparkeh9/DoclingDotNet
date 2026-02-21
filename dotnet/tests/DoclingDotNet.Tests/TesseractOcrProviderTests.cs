using DoclingDotNet.Models;
using DoclingDotNet.Ocr;
using Xunit;

namespace DoclingDotNet.Tests;

public sealed class TesseractOcrProviderTests
{
    [Fact]
    public void IsAvailable_WhenLanguageDataMissing_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"doclingdotnet-tess-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var provider = new TesseractOcrProvider(new TesseractOcrProviderOptions
            {
                DataPath = tempDir,
                DefaultLanguage = "eng",
                AutoDownloadMissingLanguages = false
            });

            Assert.False(provider.IsAvailable());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void IsAvailable_WhenLanguageDataPresent_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"doclingdotnet-tess-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "eng.traineddata"), "stub");

            var provider = new TesseractOcrProvider(new TesseractOcrProviderOptions
            {
                DataPath = tempDir,
                DefaultLanguage = "eng",
                AutoDownloadMissingLanguages = false
            });

            Assert.True(provider.IsAvailable());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessAsync_WhenPagesAreEmpty_ReturnsNoChanges()
    {
        var provider = new TesseractOcrProvider(new TesseractOcrProviderOptions
        {
            DataPath = "missing-data-path"
        });

        var result = await provider.ProcessAsync(new OcrProcessRequest
        {
            RunId = "run-ocr-empty",
            DocumentKey = "doc-1",
            FilePath = "input.pdf",
            Language = "eng",
            Pages = []
        });

        Assert.Equal(OcrProcessStatus.NoChanges, result.Status);
    }

    [Fact]
    public async Task ProcessAsync_WhenRequestedLanguageDataMissing_ReturnsRecoverableFailure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"doclingdotnet-tess-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var provider = new TesseractOcrProvider(new TesseractOcrProviderOptions
            {
                DataPath = tempDir,
                DefaultLanguage = "eng",
                AutoDownloadMissingLanguages = false
            });

            var result = await provider.ProcessAsync(new OcrProcessRequest
            {
                RunId = "run-ocr-missing-lang",
                DocumentKey = "doc-2",
                FilePath = "input.pdf",
                Language = "eng",
                Pages =
                [
                    new SegmentedPdfPageDto()
                ]
            });

            Assert.Equal(OcrProcessStatus.RecoverableFailure, result.Status);
            Assert.Equal("MissingLanguageData", result.ErrorType);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using DoclingDotNet.Asr;
using DoclingDotNet.Pipeline;

namespace TranscribeDemo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Initializing DoclingDotNet with Whisper.net...");

        var rootDir = @"d:\code\sparkeh9\doclingdotnet";
        var modelPath = Path.Combine(rootDir, "dotnet", "tests", "DoclingDotNet.Tests", "Assets", "ggml-tiny.en.bin");
        var audioSamplePath = Path.Combine(rootDir, "TranscribeDemo", "blindfury_clip.wav");

        Console.WriteLine($"Model Path: {modelPath}");
        Console.WriteLine($"Audio Path: {audioSamplePath}");
        Console.WriteLine(new string('-', 50));

        if (!File.Exists(modelPath))
        {
            Console.WriteLine("Error: Whisper model not found!");
            return;
        }

        if (!File.Exists(audioSamplePath))
        {
            Console.WriteLine("Error: Audio sample not found!");
            return;
        }

        using var provider = new WhisperNetAsrProvider(modelPath);

        var request = new AudioConversionRequest
        {
            FilePath = audioSamplePath,
            AsrProvider = provider
        };

        var runner = new DoclingAudioConversionRunner();
        
        Console.WriteLine("Transcribing...");
        var result = await runner.ExecuteAsync(request);

        Console.WriteLine($"Status: {result.Pipeline.Status}");
        if (result.Diagnostics.Count > 0)
        {
            foreach (var diag in result.Diagnostics)
            {
                Console.WriteLine($"Diag: {diag.Message}");
            }
        }

        Console.WriteLine("Transcription Complete!\n");
        Console.WriteLine("--- TRANSCRIPTION ---");
        var sb = new System.Text.StringBuilder();
        foreach (var page in result.Pages)
        {
            foreach (var cell in page.TextlineCells)
            {
                var text = cell.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                var start = cell.Source?.StartTime?.ToString("0.00") ?? "0.00";
                var end = cell.Source?.EndTime?.ToString("0.00") ?? "0.00";
                
                var line = $"[{start}s -> {end}s] {text}";
                Console.WriteLine(line);
                sb.AppendLine(line);
            }
        }
        var outputPath = Path.Combine(AppContext.BaseDirectory, "transcription_output.txt");
        await File.WriteAllTextAsync(outputPath, sb.ToString());
        
        Console.WriteLine("---------------------");
        Console.WriteLine($"\nOutput saved to: {outputPath}");
    }
}

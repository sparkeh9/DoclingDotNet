using System.Runtime.InteropServices;
using Tesseract;

Console.WriteLine("Spike.Tesseract: validating native OCR runtime load...");
Console.WriteLine($"Runtime: {Environment.Version} | OS: {Environment.OSVersion} | Arch: {RuntimeInformation.ProcessArchitecture}");

var dataRoot = Path.Combine(AppContext.BaseDirectory, "tessdata");
Directory.CreateDirectory(dataRoot);

var engDataPath = Path.Combine(dataRoot, "eng.traineddata");
var x64NativePath = Path.Combine(AppContext.BaseDirectory, "x64", "tesseract50.dll");
var leptonicaPath = Path.Combine(AppContext.BaseDirectory, "x64", "leptonica-1.82.0.dll");

Console.WriteLine($"Native DLL present: {File.Exists(x64NativePath)} ({x64NativePath})");
Console.WriteLine($"Leptonica DLL present: {File.Exists(leptonicaPath)} ({leptonicaPath})");

if (!File.Exists(engDataPath))
{
    Console.WriteLine("Downloading eng.traineddata...");
    using var http = new HttpClient();
    using var stream = await http.GetStreamAsync("https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata");
    await using var file = File.Create(engDataPath);
    await stream.CopyToAsync(file);
}

try
{
    using var engine = new TesseractEngine(dataRoot, "eng", EngineMode.Default);
    Console.WriteLine("Tesseract native load: OK");
    Console.WriteLine($"Tesseract version: {engine.Version}");
}
catch (Exception ex)
{
    Console.WriteLine("Tesseract native load: FAILED");
    Console.WriteLine(ex.ToString());
    Environment.ExitCode = 1;
}

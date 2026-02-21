using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DoclingDotNet.Pipeline;
using DoclingDotNet.Models;

var request = new PdfConversionRequest
{
    FilePath = args[0]
};

var runner = new DocumentConversionRunner();
var result = await runner.ExecuteAsync(request);

var texts = result.Pages
    .SelectMany(p => p.TextlineCells)
    .Select(c => c.Text)
    .ToList();

var json = JsonSerializer.Serialize(texts, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);

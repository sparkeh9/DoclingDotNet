using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DoclingDotNet.Pipeline;
using DoclingDotNet.Models;

namespace DoclingBenchmark;

class Program
{
    static async Task Main(string[] args)
    {
        var files = new[] {
            "upstream/deps/docling-parse/tests/data/regression/font_01.pdf",
            "upstream/deps/docling-parse/tests/data/regression/complex_tables.pdf",
            "upstream/docling/tests/data/docx/word_sample.docx",
            "upstream/docling/tests/data/html/wiki_duck.html"
        };

        var runner = new DocumentConversionRunner();

        Console.WriteLine("{");
        for (int i = 0; i < files.Length; i++)
        {
            var f = files[i];
            if (!File.Exists(f))
            {
                continue; // Skip if file is missing
            }

            var request = new PdfConversionRequest { FilePath = f };
            
            try 
            {
                // Warmup
                await runner.ExecuteAsync(request);
                
                var sw = Stopwatch.StartNew();
                int iterations = 10;
                for (int j = 0; j < iterations; j++)
                {
                    await runner.ExecuteAsync(request);
                }
                sw.Stop();
                
                var avgTime = sw.Elapsed.TotalSeconds / iterations;
                var fileName = Path.GetFileName(f);
                Console.WriteLine($"  \"{fileName}\": {avgTime.ToString(System.Globalization.CultureInfo.InvariantCulture)}{(i == files.Length - 1 ? "" : ",")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  \"{Path.GetFileName(f)}\": -1 /* Error: {ex.Message} */{(i == files.Length - 1 ? "" : ",")}");
            }
        }
        Console.WriteLine("}");
    }
}
# DoclingDotNet Usage Guide

Welcome to the **DoclingDotNet** quick-start guide! This library is a high-performance, pure-managed .NET port of the popular [Docling](https://github.com/DS4SD/docling) document parsing library. It allows you to extract highly structured semantic data (text, tables, reading order, layouts) from PDFs and native semantic formats (DOCX, PPTX, HTML, XML) seamlessly.

## Installation

Install the core library via the NuGet package manager:

```bash
dotnet add package DoclingDotNet
```

Depending on your target formats and advanced features, you may also want to install the corresponding native dependencies:

```bash
# Required for parsing PDFs natively
dotnet add package bblanchon.PDFium.Win32 # Or .Linux / .macOS

# Required for AI-driven layout inference (RT-DETR)
dotnet add package Microsoft.ML.OnnxRuntime

# Required for OCR fallback on scanned documents
dotnet add package Tesseract
```

---

## 1. Unified Document Conversion

The easiest way to get started is using the unified `DocumentConversionRunner`. This acts as a master dispatcher: it automatically detects the file type and routes it either through the complex PDF pipeline or directly into the fast, deterministic native backends (like DOCX or HTML).

```csharp
using System;
using System.Text.Json;
using System.Threading.Tasks;
using DoclingDotNet.Pipeline;

class Program
{
    static async Task Main(string[] args)
    {
        // Initialize the universal runner
        var runner = new DocumentConversionRunner();

        // Prepare the request
        var request = new PdfConversionRequest
        {
            FilePath = "path/to/your/document.docx" // Works for .pdf, .docx, .pptx, .html, etc.
        };

        // Execute the conversion
        var result = await runner.ExecuteAsync(request);

        Console.WriteLine($"Successfully parsed {result.PageCount} pages.");

        // The extracted structural data is available in result.Pages
        foreach (var page in result.Pages)
        {
            Console.WriteLine($"Page {page.Dimension.Rect.RX2}x{page.Dimension.Rect.RY2}");
            foreach (var cell in page.TextlineCells)
            {
                Console.WriteLine($"[{cell.Rect.RX0}, {cell.Rect.RY0}] -> {cell.Text}");
            }
        }
        
        // Export to JSON if needed
        var json = JsonSerializer.Serialize(result.Pages, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }
}
```

---

## 2. Advanced PDF Parsing (OCR & AI Layouts)

Unlike semantic formats (DOCX/HTML) which have structural tags built-in, PDFs are just a collection of 2D coordinates. To extract meaningful paragraphs, tables, and reading order from complex PDFs, `DoclingDotNet` uses a configurable pipeline.

### Basic PDF Extraction

```csharp
using DoclingDotNet.Pipeline;

var pdfRunner = new DoclingPdfConversionRunner();
var request = new PdfConversionRequest 
{ 
    FilePath = "scanned_invoice.pdf" 
};

var result = await pdfRunner.ExecuteAsync(request);
```

### Enabling OCR Fallback

If a PDF page contains no readable text (e.g., it's a scanned image), you can enable OCR fallback. `DoclingDotNet` natively supports Tesseract.

```csharp
var request = new PdfConversionRequest 
{ 
    FilePath = "scanned_invoice.pdf",
    EnableOcrFallback = true,
    RequireOcrSuccess = false,
    OcrLanguage = "eng" // Ensure you have eng.traineddata in your tessdata/ folder!
};

// The runner will automatically utilize Tesseract if the page lacks text cells.
var result = await pdfRunner.ExecuteAsync(request);

Console.WriteLine($"OCR was applied: {result.OcrApplied} (Provider: {result.OcrProviderName})");
```

### Enabling ONNX AI Layout Inference (RT-DETR)

To identify complex structures like `table`, `picture`, `formula`, or `list_item` accurately, you can feed the pages through the `docling-layout-heron-onnx` model.

```csharp
using DoclingDotNet.Layout;

// 1. Initialize the ONNX provider with the downloaded RT-DETR model path
var onnxLayout = new OnnxLayoutProvider("models/docling-layout-heron-onnx/model.onnx");

// 2. Register it with the runner
var pdfRunner = new DoclingPdfConversionRunner(layoutProviders: new[] { onnxLayout });

// 3. Enable layout inference in the request
var request = new PdfConversionRequest 
{ 
    FilePath = "academic_paper.pdf",
    EnableLayoutInference = true 
};

var result = await pdfRunner.ExecuteAsync(request);

// Now your pages will have highly structured Clusters (Paragraphs, Tables, Figures)
// grouped in logical reading order.
```

---

## 3. Native Audio Transcription (ASR)

DoclingDotNet supports audio transcription natively using `Whisper.net`, completely bypassing Python for extreme inference performance. Just like the PDF conversion pipeline, audio extraction maps directly into the standard `DoclingDocument` JSON structures (with `TrackSource` metadata encoding start/end timestamps).

> [!NOTE]
> Converting from non-WAV formats (MP4, MP3, FLAC, M4A) requires `ffmpeg` to be installed on the host system and available in the `$PATH`. If `ffmpeg` is missing, only standard 16Hz 16-bit Mono PCM `.wav` files will process automatically.

**1. Install `Whisper.net`:**
```xml
<PackageReference Include="Whisper.net" Version="1.9.0" />
<PackageReference Include="Whisper.net.Runtime" Version="1.9.0" />
```

**2. Transcribe Audio:**
```csharp
using DoclingDotNet.Asr;
using DoclingDotNet.Pipeline;

// Load a local GGML Whisper model
using var provider = new WhisperNetAsrProvider("ggml-base.en.bin");

var request = new AudioConversionRequest
{
    FilePath = "podcast.wav",
    AsrProvider = provider
};

var runner = new DoclingAudioConversionRunner();
var result = await runner.ExecuteAsync(request);

// The transcribed chunks are packed identically to PDF text cells!
foreach (var page in result.Pages)
{
    foreach (var cell in page.TextlineCells)
    {
        Console.WriteLine($"[{cell.Source.StartTime} -> {cell.Source.EndTime}] {cell.Text}");
    }
}
```

---

## 4. Native Semantic Formats (Non-PDF Backends)

While the `DocumentConversionRunner` automatically handles routing, `DoclingDotNet` natively supports parsing many structured document formats without incurring the massive overhead of optical layout AI. 

We parse these using the official standard libraries (like `DocumentFormat.OpenXml` and `HtmlAgilityPack`), ensuring high-speed, zero-drift text extraction.

### Supported Formats
*   **Microsoft Word** (`.docx`, `.docm`, `.dotx`, `.dotm`) via `MsWordDocumentBackend`
*   **Microsoft PowerPoint** (`.pptx`, `.pptm`, `.potx`, `.potm`) via `MsPowerPointDocumentBackend`
*   **Microsoft Excel** (`.xlsx`, `.xlsm`, `.xltx`, `.xltm`) via `MsExcelDocumentBackend`
*   **HTML** (`.html`, `.htm`) via `HtmlDocumentBackend`
*   **XML / Academic Metadata** (`.xml`, `.jats`, `.mets`) via `XmlDocumentBackend`
*   **LaTeX** (`.tex`, `.latex`) via `LatexDocumentBackend`
*   **Markdown & AsciiDoc** (`.md`, `.markdown`, `.adoc`, `.asciidoc`) via `MarkdownDocumentBackend`
*   **CSV & TSV** (`.csv`, `.tsv`) via `CsvDocumentBackend`
*   **Native Images** (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.tiff`, `.webp`) via `ImageDocumentBackend`
*   **Plain Text** (`.txt`, `.log`, `.ini`) via `TextDocumentBackend`
*   **EPUB eBooks** (`.epub`) via `EpubDocumentBackend`
*   **Emails** (`.eml`, `.msg`) via `EmlDocumentBackend`

### Explicitly Calling a Backend

If you only want to process a specific format without instantiating the entire PDF pipeline, you can use the backend interfaces directly:

```csharp
using DoclingDotNet.Backends;
using System.Threading;

// 1. Instantiate the specific backend
var backend = new MsWordDocumentBackend();

// 2. Pass in the file path
var pages = await backend.ConvertAsync(stream, CancellationToken.None);

// 3. The backend returns the same exact SegmentedPdfPageDto schema as a PDF!
foreach (var page in pages) 
{
    foreach (var cell in page.TextlineCells) 
    {
        Console.WriteLine(cell.Text); // Extracts raw paragraph texts, resolving tables and lists natively
    }
}
```

By ensuring every format—whether PDF, DOCX, or HTML—outputs the exact same `SegmentedPdfPageDto` data structure, you only have to write your downstream integration logic (e.g., chunking for RAG, dumping to a database) once.

---

## 4. Batch Processing

For processing thousands of documents, use the `ExecuteBatchAsync` API. It handles parallel isolation, aggregates telemetry, and generates structured output artifacts (manifests, summaries, and diagnostic logs).

```csharp
var batchRequest = new PdfBatchConversionRequest
{
    BatchRunId = "nightly_ingestion_001",
    Documents = new[]
    {
        new PdfConversionRequest { FilePath = "doc1.pdf" },
        new PdfConversionRequest { FilePath = "doc2.docx" }
    },
    ContinueOnDocumentFailure = true,
    ArtifactOutputDirectory = "./output/batch_results",
    CleanArtifactOutputDirectory = true
};

var batchResult = await pdfRunner.ExecuteBatchAsync(batchRequest);

Console.WriteLine($"Batch completed. {batchResult.Aggregation.SucceededDocumentCount} succeeded, {batchResult.Aggregation.FailedDocumentCount} failed.");
Console.WriteLine($"Artifact saved to: {batchResult.PersistedArtifactDirectory}");
```

---

## 5. Understanding the Output DTO (`SegmentedPdfPageDto`)

The primary output of the conversion is a list of `SegmentedPdfPageDto` objects. This perfectly mirrors the JSON schema of the upstream Python library.

*   `Dimension`: The geometric size of the page (Width/Height/CropBox).
*   `CharCells` / `WordCells` / `TextlineCells`: Granular access to extracted text at the character, word, or full-line level, including fonts, colors, and bounding boxes.
*   `BitmapResources`: Embedded images extracted from the document.
*   `Shapes` / `Lines`: Vector graphics and their stroke/fill properties.
*   `Tables` & `LayoutClusters`: (Available if Layout Inference was run) Structurally grouped text and grid boundaries.

Every coordinate in `DoclingDotNet` uses the standard PDF `BOTTOMLEFT` origin system.

---

## 6. Exporting and Serialization

While the primary output format supported natively by `DoclingDotNet` is **JSON**, the library also provides several built-in format exporters in the `DoclingDotNet.Export` namespace for your convenience.

### Exporting to JSON (Lossless)

Because `SegmentedPdfPageDto` is fully decorated with `[JsonPropertyName]` attributes matching the Python data model, you can serialize the output directly to a lossless JSON representation:

```csharp
using System.Text.Json;
using DoclingDotNet.Serialization;

// DoclingJson.SerializerOptions is provided to match the exact snake_case expected by upstream python.
var jsonString = JsonSerializer.Serialize(result.Pages, DoclingJson.SerializerOptions);
await File.WriteAllTextAsync("output.json", jsonString);
```

### Exporting to Other Formats (Markdown, HTML, Plain Text, DocTags)

If you need to feed the extracted content directly into an LLM or render it visually, you can pass the `PdfConversionRunResult` object to one of the static exporters:

```csharp
using DoclingDotNet.Export;

// 1. Markdown (Ideal for RAG / LLM Context)
var markdown = MarkdownExporter.Export(result);
await File.WriteAllTextAsync("output.md", markdown);

// 2. HTML (Ideal for rendering structured text in a browser)
var html = HtmlExporter.Export(result);
await File.WriteAllTextAsync("output.html", html);

// 3. DocTags (XML-like format containing raw text and geometric bounding boxes)
var docTags = DocTagsExporter.Export(result);
await File.WriteAllTextAsync("output.doctags.xml", docTags);

// 4. Plain Text (Stripped of all formatting)
var plainText = TextExporter.Export(result);
await File.WriteAllTextAsync("output.txt", plainText);
```

---

## 7. Developer Guide (Building from Source)

If you have cloned this repository and want to build or contribute to the core engine, you must initialize the upstream dependencies (which are not committed to the repo to keep it lightweight).

Run the initialization script from the root of the repository:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\init-workspace.ps1
```

This script will:
1. Clone the specific baseline versions of IBM's `docling` and `docling-parse` into the `upstream/` directory.
2. Apply the .NET C-ABI bridge patches to the native C++ code.
3. Prepare the environment for building the native components.

Once initialized, you can run the standard `dotnet build` and `dotnet test` commands.

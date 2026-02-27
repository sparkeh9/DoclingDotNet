<div align="center">

# DoclingDotNet

[![Slice0 CI](https://github.com/sparkeh9/doclingdotnet/actions/workflows/slice0-ci.yml/badge.svg)](https://github.com/sparkeh9/doclingdotnet/actions/workflows/slice0-ci.yml)
[![NuGet version](https://img.shields.io/nuget/v/DoclingDotNet.svg)](https://www.nuget.org/packages/DoclingDotNet/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**A high-performance, pure-managed .NET port of the Docling document parsing library. Blisteringly fast extraction of structured semantic data from PDFs, DOCX, HTML, EPUB, and more.**

</div>

## Overview
DoclingDotNet is a high-fidelity, cross-platform .NET port of IBM's [Docling](https://github.com/DS4SD/docling). It extracts highly structured semantic data—including paragraphs, tables, reading order, and layouts—from complex documents. 

By replacing heavy Python and C++ spatial algorithms with zero-allocation, pure-managed C# code, DoclingDotNet parses complex PDFs **up to 2x faster** and structured formats like DOCX/HTML **over 10x faster** than the upstream implementation while maintaining **100% semantic behavioral parity**.

It features a completely managed pipeline with native support for structured formats (DOCX, PPTX, HTML, EPUB, EML), advanced AI extraction for PDFs (via ONNX RT-DETR layout inference and Tesseract OCR), and now native Audio Speech Recognition (ASR) via Whisper.net.

## Start Here
- 🚀 **[Quick Start & Usage Guide](docs/USAGE.md)**: Learn how to convert documents, enable AI layouts, and extract Markdown/HTML.
- ⚡ **[Benchmarks (Speed & Accuracy)](BENCHMARK.md)**: See the empirical speedup metrics and semantic parity guarantees.
- 🏗️ **[The Strangler Fig Architecture](docs/02_Architecture/Strangler_Fig_Porting.md)**: Read how we safely ported a massive Python monolith to .NET without breaking behavior.

## The Strangler Fig Porting Method
To safely port the massive Python and C++ ecosystem to .NET without breaking functionality, we utilized the **Strangler Fig Pattern**. 

Instead of a complete rewrite, we established an immutable JSON data contract (`SegmentedPdfPageDto`) as our absolute source of truth. We then "strangled" the upstream Python codebase slice by slice:
1. **The Roots:** We started by wrapping the core C++ `docling-parse` library with a thin P/Invoke C-ABI bridge, mapping its raw output directly into our JSON contract.
2. **The Trunk:** We ripped out the slow, FFI-heavy Python layout and reading-order algorithms, replacing them with pure C# zero-allocation algorithms (custom `SpatialIndex`, `IntervalTree`, and `UnionFind`) that output the exact same JSON.
3. **The Branches:** Finally, we bypassed the PDF pipeline entirely to parse semantic formats (DOCX, HTML, EPUB) using robust .NET native libraries (`DocumentFormat.OpenXml`, `HtmlAgilityPack`), ensuring they also produce the exact same JSON contract.

By validating every slice with an automated **Semantic Parity Harness**, we guaranteed that the .NET port remained 100% behaviorally identical to the upstream Python logic at every step, while allowing us to ruthlessly optimize for C# performance. 

You can read the full case study here: [**Strangling the Python Monolith to High-Performance .NET**](docs/02_Architecture/Strangler_Fig_Porting.md).

## Key Features

*   **Universal Input**: Seamlessly parses `.pdf`, `.docx`, `.pptx`, `.xlsx`, `.html`, `.epub`, `.eml`, `.md`, `.csv`, `.wav`, `.mp3` and raw images.
*   **Universal Output**: Regardless of the input format, DoclingDotNet emits a unified, highly-structured JSON data model (`SegmentedPdfPageDto`).
*   **Built-in Exporters**: Export the unified model directly to Markdown, HTML, Plain Text, or DocTags for immediate ingestion into LLMs or Vector Databases (RAG pipelines).
*   **AI-Powered Layouts**: Configurable pipeline allows routing complex PDFs through local ONNX `RT-DETR` models to accurately group tables, figures, and paragraphs.
*   **Native Audio Transcription (ASR)**: Configurable pipeline runs `Whisper.net` to instantly transcribe `.wav` or transcodes `.mp3`/`.m4a` files locally (requires `ffmpeg` on path) and hydrates them directly into the standard JSON schema with timeline tracking.
*   **Cloud Native & Serverless Ready**: 100% `System.IO.Stream` compatible. Parse documents directly from memory buffers without touching the disk.

## Quick Example
```csharp
using DoclingDotNet.Pipeline;
using DoclingDotNet.Export;

var runner = new DocumentConversionRunner();
var request = new PdfConversionRequest { FilePath = "sample_book.epub" }; // Or .pdf, .docx, etc.

var result = await runner.ExecuteAsync(request);

// Instantly generate RAG-ready Markdown!
var markdown = MarkdownExporter.Export(result);
Console.WriteLine(markdown);
```

## Contributing and Architecture
If you are contributing to the core engine, please review the internal architectural documentation:
- Project context & overview: `docs/01_Context/Project_Overview.md`
- Current implementation state: `docs/01_Context/Current_State.md`
- Parity Mechanism (How CI blocks drift): `docs/03_Execution/Parity_Mechanism.md`
- Main docs index: `docs/00_Index/Map_of_Content.md`

## Core Validation
If you are developing locally, ensure the Semantic Parity Harness passes against the ground truth corpus before opening a Pull Request:
```powershell
dotnet test dotnet/DoclingDotNet.slnx --configuration Release
powershell -ExecutionPolicy Bypass -File .\scripts\test-docling-parse-cabi-smoke.ps1 -SkipConfigure
powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 -SkipConfigure -Output .artifacts/parity/docling-parse-parity-report.json -MaxOcrDrift 0 -TextMismatchSeverity Minor -GeometryMismatchSeverity Minor -ContextMismatchSeverity Minor --max-pages 20
```

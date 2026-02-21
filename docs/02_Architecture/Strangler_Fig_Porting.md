# Strangling the Python Monolith: How We Ported Docling to High-Performance .NET

Porting a complex, AI-driven, multi-language document parsing library from Python and C++ into a pure-managed .NET ecosystem is a daunting task. The risk of introducing subtle behavioral drift—where a bounding box is off by a fraction of a pixel, or a complex reading-order algorithm misinterprets a multi-column layout—is incredibly high.

To successfully port the popular [Docling](https://github.com/DS4SD/docling) library to C# (`DoclingDotNet`), we didn't attempt a risky, all-at-once "big bang" rewrite. Instead, we applied a strict interpretation of the **Strangler Fig Pattern**, heavily relying on a rigid data contract and an automated Semantic Parity Harness.

Here is how we systematically replaced a massive Python ecosystem with high-performance, zero-allocation .NET code.

---

## 1. The Anchor: Defining the Data Contract

The Strangler Fig pattern requires a clear boundary between the legacy system (the "host") and the new system (the "strangler"). For Docling, that boundary wasn't an API endpoint; it was a complex JSON schema representing the physical and semantic structure of a document page.

We defined `SegmentedPdfPageDto` in C#. This Data Transfer Object became our absolute source of truth. It defined exactly what a page looked like: its dimensions, its text cells, its bounding boxes (`r_x0`, `r_y0`...), its images, and its tabular structures.

Whether the upstream Python library was parsing a scanned PDF using PyTorch, or extracting paragraphs from a DOCX file using `python-docx`, the final output was always mapped to this specific JSON structure.

## 2. Slice by Slice: The Strangler in Action

Instead of rewriting everything, we sliced the port into discrete, verifiable vertical increments.

### Slice 1: The Core C++ Engine (The Roots)
Docling's heavy lifting for PDFs is done by a C++ engine (`docling-parse`). 
Instead of rewriting the PDF parser in C#, we built a thin C-ABI bridge (`DoclingParseAbi.cs`) using P/Invoke. We ran the C++ engine, extracted the raw data, and mapped it directly into our `SegmentedPdfPageDto` contract.

*At this stage, .NET was just a wrapper around the legacy native engine.*

### Slice 5: Ripping Out Python Algorithms (The Trunk)
Upstream Docling uses complex Python algorithms, backed by native C++ spatial libraries like `rtree` and `libspatialindex`, to group overlapping text boxes and determine the logical reading order of a page.

Calling Python or C++ spatial libraries from .NET incurs massive Foreign Function Interface (FFI) overhead. So, we "strangled" this part of the Python codebase. We wrote pure-managed, zero-allocation C# equivalents:
*   A custom `SpatialIndex<T>` using flat arrays replaced `rtree`.
*   A custom `IntervalTree` backed by `List<T>.BinarySearch` replaced Python's `bisect`.
*   A custom `UnionFind` struct handled grouping.

We completely replaced the Python layout and reading-order pipelines with our `LayoutPostprocessor` and `ReadingOrderPredictor` in C#. Because our C# algorithms output the exact same `SegmentedPdfPageDto`, the rest of the pipeline didn't know the underlying engine had been completely rewritten.

### Slice 6: Strangling the Semantic Formats (The Branches)
Upstream Docling parses Word documents (DOCX) and HTML using heavy Python libraries (`python-docx`, `BeautifulSoup`). 

We strangled these dependencies entirely. We built `MsWordDocumentBackend` using Microsoft's official `DocumentFormat.OpenXml` package, and `HtmlDocumentBackend` using `HtmlAgilityPack`. We traversed the native `.NET` DOMs and mapped the paragraphs, headings, and tables directly into—you guessed it—our `SegmentedPdfPageDto` contract.

We bypassed the heavy AI PDF pipeline entirely for these formats, resulting in blistering parse speeds.

---

## 3. The Safety Net: The Semantic Parity Harness

How did we know our pure C# `ReadingOrderPredictor` or our OpenXML DOCX parser actually matched the upstream Python library? We built a **Semantic Parity Harness**.

Before writing the C# implementations, we ran the upstream Python `docling` library against a massive corpus of test documents (PDFs, DOCX, HTML) and saved the resulting JSON outputs as "Ground Truth."

Every time we implemented a new slice in .NET, our CI pipeline ran the C# code against the same test documents and compared the resulting JSON against the Python Ground Truth. 

Standard text diffing tools fail on complex AI outputs (e.g., native C++ might output `0.1000001` while .NET outputs `0.1000002`). Our Parity Harness performed deep semantic equivalence checks:
*   **Geometric Drift:** It calculated Intersection-over-Union (IoU) on bounding boxes to ignore microscopic floating-point differences.
*   **Sequence Hashing:** It cryptographically hashed the exact reading order of text cells to ensure our C# graph traversal matched Python's layout flow perfectly.
*   **Distribution Matching:** It verified that the statistical distribution of font names, colors, and line-caps matched exactly.

If a C# change caused a structural divergence from the Python baseline, the CI pipeline failed immediately.

---

## The Result: Significantly Faster

By treating the JSON output as an immutable contract, we successfully "strangled" the massive Python monolith. 

Because we replaced dynamic Python dictionaries and FFI-heavy C++ spatial calls with strictly typed, memory-contiguous C# structs (`Span<T>`), the performance gains were staggering. 

In our benchmarks, the pure-managed `.NET` port parses and extracts semantics from complex PDFs **up to 2x faster**, and from structured formats like DOCX/HTML **over 10x faster** than the upstream Python library, while maintaining 100% structural and semantic parity.

The Strangler Fig pattern didn't just make the porting process safer; it allowed us to aggressively optimize the specific bottlenecks of the legacy architecture while proving, empirically, that the output remained perfectly correct.
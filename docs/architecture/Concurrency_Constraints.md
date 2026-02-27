# `DoclingDotNet` Native Concurrency Findings

## Executive Summary
You asked if we can extend `.NET`'s lead by explicitly parallelizing the C# extraction wrapper to process pages simultaneously.

**Short Answer:** We can, but **NOT** on the same `DoclingParseSession` / `docling_parse_handle` for a single loaded document. The underlying C++ library (`docling-parse`) and its PDF backend (`QPDF`) are **not thread-safe** for concurrent concurrent reads/decodes of the *same* document instance.

## Technical Details

I traced the C ABI down to the core layout logic inside `pdflib::document_decoder` (which wraps `QPDF`):

1. **Shared State:** When you call `LoadDocument` in .NET, the C ABI spins up a single `pdflib::document_decoder` and stores it into a global dictionary (`handle->state.key2doc[key]`).
2. **QPDF is not thread-safe:** The `document_decoder` holds a single `QPDF` instance (`QPDF qpdf_document;`). `QPDF` explicitly states in its documentation that while you can have multiple `QPDF` objects on different threads, a single `QPDF` object (and its spawned `QPDFObjectHandle` references) cannot be accessed concurrently. 
3. **Internal Caching State:** The `decode_segmented_page_json` Native call explicitly mutates the state of the document decoder on every call:
   ```cpp
   // docling_parse_c_api.cpp : 971
   decoder->unload_page(page_number);
   auto page_decoder = decoder->decode_page(page_number, config);
   ```
   If two .NET threads call `DecodeSegmentedPageJson` simultaneously with the same `docKey`, they will hit a race condition mutating the internal caches of that `pdflib::document_decoder` and the `QPDF` parser, inevitably causing a memory violation (`AccessViolationException` or segfault).

## How We *Can* Parallelize .NET

Because `QPDF` and `pdflib` are thread-safe *across different document instances*, we can achieve parallel page extraction without OS scheduling bottlenecks if we change how the .NET API handles the native boundary.

### Option 1: The "Clone" Approach (Document-level scaling)
If you are processing *multiple different PDFs*, you can easily use `.AsParallel()` or `Parallel.ForEach` in C#. Each PDF gets its own unique `docKey` and `DoclingParseSession`, which means it gets a totally isolated `QPDF` object in memory. This is 100% thread-safe and will scale linearly up to your core count.

### Option 2: The "Multi-Instance" Approach (Page-level scaling)
If we want to extract **one massive 1,000-page PDF** much faster, we cannot use a single `DoclingParseSession`. We would need to build a C# dispatcher that:
1. Loads the exact same PDF bytes into *N* different `DoclingParseSession` instances (where *N* is the number of CPU cores).
2. Assigns different page ranges to different sessions (e.g., Session A decodes pages 1-250, Session B decodes pages 251-500).
3. Merges the results back together in C#.

Because `.NET` uses `Span<T>` and fast JSON source-generators, this dispatcher would have virtually zero overhead, allowing us to squeeze maximum multi-threaded performance out of the C++ engine without risking data races.

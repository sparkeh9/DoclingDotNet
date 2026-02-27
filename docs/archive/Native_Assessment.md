# Docling .NET Port Assumption Pressure Test

Date: 2026-02-20

## Verdict Summary
- Confirmed: 5
- Partially confirmed: 1
- Challenged: 0

## Assumptions

### A1: No intrinsic technical blocker to a .NET port
Status: **Confirmed**

Evidence:
- Three independent native/runtime integrations were validated in .NET 10 using prebuilt binaries:
  - ONNX Runtime (`dotnet/examples/Spike.OnnxRuntime/Program.cs`)
  - PDFium (`dotnet/examples/Spike.Pdfium/Program.cs`)
  - Tesseract (`dotnet/examples/Spike.Tesseract/Program.cs`)
- All three compile and execute successfully on Windows x64.

Implication:
- A .NET port is feasible from a runtime interop standpoint.

### A2: Native dependencies can be consumed without local C/C++ builds
Status: **Confirmed (for tested surfaces)**

Evidence:
- NuGet restore provided native binaries directly:
  - ONNX Runtime package includes `runtimes/win-x64/native/onnxruntime.dll`.
  - PDFium package includes `build/pdfium_x64.dll`.
  - Tesseract package includes `x64/tesseract50.dll` and `x64/leptonica-1.82.0.dll`.
- All spikes executed without compiling native code locally.

Implication:
- Binary-first packaging strategy is viable for major runtime pieces.

### A3: `docling-parse` can be wrapped via a thin C ABI over existing core
Status: **Confirmed (implemented and spike-validated)**

Evidence:
- Original state was pybind-first:
  - `deps/docling-parse/CMakeLists.txt:193` (`pybind11_add_module(pdf_parsers ...)`)
  - `deps/docling-parse/app/pybind_parse.cpp:15` (`PYBIND11_MODULE(pdf_parsers, m)`)
- Added a dedicated C ABI layer and build target:
  - `deps/docling-parse/src/c_api/docling_parse_c_api.h`
  - `deps/docling-parse/src/c_api/docling_parse_c_api.cpp`
  - `deps/docling-parse/CMakeLists.txt` (`DOCLING_PARSE_BUILD_C_API`, `docling_parse_c`)
- Hardened contract for long-term interop stability:
  - ABI/version introspection (`docling_parse_get_abi_version`)
  - size-safe config initialization (`docling_parse_get_decode_page_config_size`, `docling_parse_init_decode_page_config`)
  - compatibility/lifetime/threading contract documented in `deps/docling-parse/docs/c_abi.md`
- Added Python-binding opt-out for native-only consumers:
  - `deps/docling-parse/CMakeLists.txt` (`DOCLING_PARSE_BUILD_PYTHON_BINDINGS=OFF`)
- Built `docling_parse_c.dll` and invoked from .NET via P/Invoke:
  - `dotnet/examples/Spike.DoclingParseCAbi/NativeDoclingParse.cs`
  - `dotnet/examples/Spike.DoclingParseCAbi/Program.cs`

Implication:
- Thin C ABI is now a concrete and tested interop path, not just a proposal.
- Remaining work is mostly packaging, CI publication, and API expansion based on parity targets.

### A4: OCR/model stack should be pluggable with fallback chain in .NET
Status: **Partially confirmed**

Evidence:
- Source has explicit plugin/fallback controls:
  - Entry point registration: `repo/pyproject.toml:83`
  - Plugin loading via `pluggy`: `repo/docling/models/factories/base_factory.py:95`
  - Security gate for third-party plugins: `repo/docling/models/factories/base_factory.py:101`
  - `allow_external_plugins` option: `repo/docling/datamodel/pipeline_options.py:973`
- Tesseract runtime spike confirms at least one OCR backend can be independently loaded in .NET.

Implication:
- A .NET plugin architecture is appropriate.
- Exact feature parity with Python plugin ecosystem still requires explicit contract/version design and compatibility tests.

### A5: Pipeline concurrency semantics are a substantial effort area
Status: **Confirmed**

Evidence:
- `repo/docling/pipeline/standard_pdf_pipeline.py` contains custom bounded queue/stage orchestration, timeout, and run-id isolation behavior:
  - `ThreadedQueue` abstraction: `repo/docling/pipeline/standard_pdf_pipeline.py:118`
  - Batched stage processing grouped by run id: `repo/docling/pipeline/standard_pdf_pipeline.py:257`
  - Timeout propagation and failure marking: `repo/docling/pipeline/standard_pdf_pipeline.py:630`
  - Cleanup caveat for blocked worker calls: `repo/docling/pipeline/standard_pdf_pipeline.py:602`

Implication:
- This is not simple task parallelism; it needs deliberate mapping to Channels/Dataflow with behavior-level regression tests.

### A6: Backends/schema semantics are large enough to dominate port effort
Status: **Confirmed**

Evidence:
- High-complexity modules:
  - `repo/docling/backend/msword_backend.py` (1639 lines)
  - `repo/docling/backend/html_backend.py` (1348 lines)
  - `repo/docling/backend/latex_backend.py` (1228 lines)
  - `repo/docling/datamodel/pipeline_options.py` (1285 lines)
  - `repo/docling/datamodel/document.py` (607 lines)

Implication:
- Most risk is semantic fidelity and output parity, not mechanical API translation.

## Recommendations (Updated)
1. Keep binary-first strategy for ONNX/PDFium/Tesseract in early .NET milestones.
2. Adopt the new C ABI as the primary .NET bridge for parser parity; avoid re-implementing parser internals in C#.
3. Define a behavior-parity harness early (golden docs + expected structured outputs) before broad backend porting.
4. Implement plugin contracts in .NET with explicit security toggles mirroring `allow_external_plugins`.
5. Prioritize pipeline semantics tests (timeouts, partial failure, run isolation) before optimization.

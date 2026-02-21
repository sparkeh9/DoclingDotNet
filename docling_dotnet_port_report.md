# Docling -> .NET/C# 10 Port Report

Date: 2026-02-20  
Scope: `docling` (`repo/`) plus first-order internal dependencies (`deps/docling-core`, `deps/docling-parse`, `deps/docling-ibm-models`)

## 1) Bottom Line

There is no intrinsic technical reason this cannot be ported to .NET/C# 10.  
The codebase is portable in principle, but it is deeply integrated with native/ML runtimes that must be re-bound or replaced.

## 2) Native Libraries / Runtimes It Wraps

This section lists native surfaces, where they appear in source, and official pages.

| Surface in Docling | Native library/runtime | Evidence in source | GitHub | Docs |
|---|---|---|---|---|
| PDF backend (`pypdfium2`) | PDFium (via `pypdfium2`) | `repo/docling/backend/pypdfium2_backend.py:9`, `repo/pyproject.toml:51` | https://github.com/pypdfium2-team/pypdfium2 , https://github.com/chromium/pdfium | https://pypdfium2.readthedocs.io/en/stable/ |
| Alternate PDF parser (`docling-parse`) | C++ parser exposed via `pybind11` | `repo/docling/backend/docling_parse_backend.py:10`, `deps/docling-parse/pyproject.toml:14`, `deps/docling-parse/CMakeLists.txt:193` | https://github.com/docling-project/docling-parse , https://github.com/pybind/pybind11 | https://pybind11.readthedocs.io/en/stable/ |
| `docling-parse` transitive native deps | `qpdf`, `libjpeg-turbo`, plus compiled ext deps | `deps/docling-parse/cmake/extlib_qpdf_v12.cmake:20`, `deps/docling-parse/cmake/extlib_jpeg.cmake:19`, `deps/docling-parse/CMakeLists.txt:114` | https://github.com/qpdf/qpdf , https://github.com/libjpeg-turbo/libjpeg-turbo | https://qpdf.readthedocs.io/en/stable/ , https://libjpeg-turbo.org/Documentation/Documentation |
| Layout/Table models package | PyTorch/torchvision/Transformers runtime (`docling-ibm-models`) | `repo/pyproject.toml:49`, `deps/docling-ibm-models/pyproject.toml:33`, `deps/docling-ibm-models/pyproject.toml:42` | https://github.com/docling-project/docling-ibm-models , https://github.com/pytorch/pytorch , https://github.com/pytorch/vision , https://github.com/huggingface/transformers | https://pytorch.org/docs/stable/ , https://docs.pytorch.org/vision/stable/ , https://huggingface.co/docs/transformers/index |
| ONNX inference engines | ONNX Runtime / ONNX Runtime GPU | `repo/pyproject.toml:103`, `repo/pyproject.toml:107`, `repo/docling/models/inference_engines/object_detection/onnxruntime_engine.py:12` | https://github.com/microsoft/onnxruntime | https://onnxruntime.ai/docs/ |
| RapidOCR path | RapidOCR + backend-specific native runtimes (ONNX/OpenVINO/Paddle/Torch) | `repo/docling/models/stages/ocr/rapid_ocr_model.py:109`, `repo/docling/models/stages/ocr/auto_ocr_model.py:62`, `repo/docs/getting_started/installation.md:58` | https://github.com/RapidAI/RapidOCR , https://github.com/openvinotoolkit/openvino , https://github.com/PaddlePaddle/Paddle | https://rapidai.github.io/RapidOCRDocs/ , https://docs.openvino.ai/ , https://www.paddlepaddle.org.cn/documentation/docs/en/ |
| EasyOCR path | EasyOCR (PyTorch-backed) | `repo/docling/models/stages/ocr/easyocr_model.py:50`, `repo/docs/getting_started/installation.md:55` | https://github.com/JaidedAI/EasyOCR | https://www.jaided.ai/easyocr/ |
| Tesseract linked binding | Tesseract OCR engine via `tesserocr` (native binding) | `repo/docling/models/stages/ocr/tesseract_ocr_model.py:67`, `repo/pyproject.toml:92`, `repo/docs/getting_started/installation.md:126` | https://github.com/sirfz/tesserocr , https://github.com/tesseract-ocr/tesseract | https://tesseract-ocr.github.io/tessdoc/ |
| Tesseract CLI fallback | External `tesseract` binary invocation | `repo/docling/models/stages/ocr/tesseract_ocr_cli_model.py:76`, `repo/docling/models/stages/ocr/tesseract_ocr_cli_model.py:127` | https://github.com/tesseract-ocr/tesseract | https://tesseract-ocr.github.io/tessdoc/ |
| macOS OCR path | Apple Vision framework via `ocrmac` wrapper | `repo/docling/models/stages/ocr/ocr_mac_model.py:53`, `repo/docling/models/stages/ocr/auto_ocr_model.py:45`, `repo/pyproject.toml:55` | https://github.com/straussmaximilian/OCRMac | https://developer.apple.com/documentation/vision |
| Spatial indexing | `rtree` wrapper over `libspatialindex` | `repo/pyproject.toml:58`, `deps/docling-ibm-models/docling_ibm_models/reading_order/reading_order_rb.py:14`, `repo/docling/utils/layout_postprocessor.py:53` | https://github.com/Toblerity/rtree , https://github.com/libspatialindex/libspatialindex | https://rtree.readthedocs.io/en/stable/ |
| Optional OpenCV in model package | OpenCV native binaries (optional extras) | `deps/docling-ibm-models/pyproject.toml:49` | https://github.com/opencv/opencv | https://docs.opencv.org/ |

### Notes on native scope

- `docling-parse` is a true C++ component, not just Python glue (`deps/docling-parse/pyproject.toml:14` and `deps/docling-parse/CMakeLists.txt:193`).
- OCR and model inference are intentionally pluggable, but each plugin currently expects Python-native packages.
- Some document conversion paths also rely on external desktop tooling:
  - LibreOffice-based DOCX->PDF conversion for DrawingML fallback (`repo/docling/backend/docx/drawingml/utils.py:44`, `repo/docling/backend/msword_backend.py:339`).

## 3) Non-Native Python-Specific Code To Port (Beyond Plumbing)

These are substantive behaviors you must reimplement, not just wire-up code.

### A. Data model and validation semantics (Pydantic-heavy)

- Strict runtime validation, discriminated unions, field-level constraints, custom serializers:
  - `repo/docling/document_converter.py:294`
  - `repo/docling/datamodel/pipeline_options.py:121`
  - `repo/docling/datamodel/base_models.py:196`
  - `repo/docling/datamodel/document.py:361`
- `docling-core` schema and document semantics are central and should be mirrored in C# types:
  - `deps/docling-core/pyproject.toml:2`

Why this is non-trivial:
- Behavior compatibility depends on validation order, defaults, coercion, and JSON serialization parity.

### B. Multi-stage PDF orchestration and concurrency semantics

- Custom bounded queues, backpressure, run-id isolation, timeout/failure propagation:
  - `repo/docling/pipeline/standard_pdf_pipeline.py:118`
  - `repo/docling/pipeline/standard_pdf_pipeline.py:257`
  - `repo/docling/pipeline/standard_pdf_pipeline.py:632`

Why this is non-trivial:
- Matching throughput and failure behavior requires equivalent concurrency design, not simple task wrappers.

### C. Layout and reading-order postprocessing algorithms

- Spatial-index-driven overlap resolution, orphan cell logic, bbox refinement:
  - `repo/docling/utils/layout_postprocessor.py:157`
  - `repo/docling/utils/layout_postprocessor.py:493`
- Reading-order merge/relationship logic:
  - `repo/docling/models/stages/reading_order/readingorder_model.py:386`
  - `repo/docling/models/stages/reading_order/readingorder_model.py:419`

Why this is non-trivial:
- These are core extraction-quality algorithms, not infrastructure.

### D. Format-specific parsers with rich document semantics

- DOCX parser with tables, images, DrawingML handling, headers/footers/comments:
  - `repo/docling/backend/msword_backend.py:50`
  - `repo/docling/backend/msword_backend.py:1349`
  - `repo/docling/backend/msword_backend.py:1636`
  - `repo/docling/backend/msword_backend.py:1690`
- PPTX parser with placeholder/list-style inheritance and shape recursion:
  - `repo/docling/backend/mspowerpoint_backend.py:37`
  - `repo/docling/backend/mspowerpoint_backend.py:274`
  - `repo/docling/backend/mspowerpoint_backend.py:370`
- HTML parser with rich table-cell extraction/list semantics:
  - `repo/docling/backend/html_backend.py:230`
  - `repo/docling/backend/html_backend.py:519`
  - `repo/docling/backend/html_backend.py:1028`
- LaTeX parser with macro expansion/math/table/figure extraction:
  - `repo/docling/backend/latex_backend.py:39`
  - `repo/docling/backend/latex_backend.py:977`
  - `repo/docling/backend/latex_backend.py:1347`
- XML security + domain parsing (JATS/USPTO):
  - `repo/docling/backend/xml/jats_backend.py:120`
  - `repo/docling/backend/xml/uspto_backend.py:8`

Why this is non-trivial:
- These backends encode document semantics and edge-case logic; ports must preserve output shape and provenance.

### E. Model/OCR selection logic and prompt/inference behavior

- Auto OCR engine selection and fallback rules:
  - `repo/docling/models/stages/ocr/auto_ocr_model.py:25`
  - `repo/docling/models/stages/ocr/auto_ocr_model.py:62`
  - `repo/docling/models/stages/ocr/auto_ocr_model.py:83`
- Tesseract integration strategy (linked API + CLI fallback):
  - `repo/docling/models/stages/ocr/tesseract_ocr_model.py:29`
  - `repo/docling/models/stages/ocr/tesseract_ocr_cli_model.py:35`
- VLM prompt formatting + stop-criteria behavior:
  - `repo/docling/models/inference_engines/vlm/_utils.py:93`
  - `repo/docling/models/inference_engines/vlm/transformers_engine.py:288`
  - `repo/docling/models/inference_engines/vlm/transformers_engine.py:343`

Why this is non-trivial:
- Output consistency depends on prompt formatting, stopping criteria, and fallback heuristics.

### F. Plugin/extension discovery model

- Dynamic plugin loading via entry points (`pluggy`) and kind-based factories:
  - `repo/docling/models/factories/base_factory.py:6`
  - `repo/docling/models/factories/base_factory.py:95`
  - `repo/docling/models/plugins/defaults.py:1`

Why this is non-trivial:
- You need a .NET plugin discovery model compatible with same extension boundaries.

## 4) Porting Implication Summary

- **Intrinsic blocker:** none found.
- **Primary risk:** feature parity with current extraction quality and model behavior.
- **Primary effort drivers:**
  - Rebinding native runtimes in .NET (PDF, OCR, inference).
  - Recreating Docling document schema/validation semantics.
  - Porting algorithmic backend logic (layout/read-order/parser semantics), not just APIs.

## 5) Recommended .NET/C# 10 Mapping (with Native Equivalents)

This is a practical target stack to replace or re-bind Docling's native surfaces in C#.

| Docling subsystem / native surface | .NET/C# 10 candidate(s) | GitHub / docs | Parity and risk notes |
|---|---|---|---|
| PDF rendering/text extraction (`pypdfium2`, `docling-parse`) | `PDFiumSharp` and/or `PdfPig` | https://github.com/ArgusMagnus/PDFiumSharp , https://github.com/UglyToad/PdfPig | `PdfPig` is managed and good for text/layout extraction. PDFium binding gives renderer parity. For `docling-parse` quality parity, best path is a thin C ABI around existing C++ and call from C# via P/Invoke. |
| C++ native interop layer (`docling-parse` + transitive C libs) | P/Invoke (`LibraryImport`/`DllImport`) over custom C ABI shim | https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke | No intrinsic blocker; this is standard interop work. Keep ABI narrow and versioned to reduce binding churn. |
| ONNX inference (`onnxruntime`) | `Microsoft.ML.OnnxRuntime` C# API | https://onnxruntime.ai/docs/get-started/with-csharp.html , https://github.com/microsoft/onnxruntime/tree/main/csharp | Strong parity path for ONNX models, including CPU/GPU provider selection patterns. |
| PyTorch-backed models (`torch`, EasyOCR paths) | `TorchSharp` (where models can be represented) or convert/export to ONNX | https://github.com/dotnet/TorchSharp | Some Python model ecosystems assume HuggingFace/Python runtime behavior; ONNX export is often lower-risk for production parity. |
| Tesseract linked binding (`tesserocr`) + CLI fallback | `charlesw/tesseract` + external CLI fallback strategy | https://github.com/charlesw/tesseract , https://github.com/tesseract-ocr/tesseract , https://tesseract-ocr.github.io/tessdoc/ | Direct .NET wrapper exists. Keep CLI fallback for deployment environments where native loading is constrained. |
| macOS OCR (`ocrmac` / Apple Vision) | .NET Apple bindings (`macios`) to Vision APIs | https://github.com/dotnet/macios , https://developer.apple.com/documentation/vision | Platform-specific but feasible. Gate behind runtime OS capability checks, matching Docling's auto-selection behavior. |
| Spatial indexing (`rtree`/`libspatialindex`) | `NetTopologySuite` (`STRtree`, geometric ops) | https://github.com/NetTopologySuite/NetTopologySuite , https://nettopologysuite.github.io/NetTopologySuite/ | Good replacement for bbox overlap/intersection logic used in postprocessing. |
| Optional OpenCV runtime | `OpenCvSharp` | https://github.com/OpenCvSharp/OpenCvSharp , https://docs.opencv.org/ | Straightforward for image pre/post-processing when needed by OCR/model paths. |
| DOCX/PPTX semantics | Open XML SDK (+ optional helpers like `ShapeCrawler`) | https://github.com/dotnet/Open-XML-SDK , https://learn.microsoft.com/en-us/office/open-xml/open-xml-sdk , https://github.com/ShapeCrawler/ShapeCrawler | Core OOXML access is strong in .NET. High effort remains in reproducing Docling semantic extraction rules. |
| HTML semantics | `AngleSharp` | https://github.com/AngleSharp/AngleSharp , https://anglesharp.github.io/ | Suitable for robust DOM parsing and normalization comparable to backend HTML logic. |
| Validation/model semantics (Pydantic analog) | C# records + `FluentValidation` (+ JSON converters) | https://github.com/FluentValidation/FluentValidation , https://docs.fluentvalidation.net/en/latest/ | Requires careful reproduction of defaults/coercion/discriminated-union behavior used by Docling schemas. |
| Pipeline concurrency/backpressure | `System.Threading.Channels` and/or TPL Dataflow | https://learn.microsoft.com/en-us/dotnet/core/extensions/channels , https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library | Adequate primitives to match bounded queues, backpressure, and timeout semantics in PDF pipeline stages. |
| Plugin discovery (`pluggy` entry points) | DI + assembly scanning (`Scrutor`) | https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection , https://github.com/khellang/Scrutor | Equivalent extension model is feasible; enforce explicit plugin contracts and versioning. |

### Implementation note on `docling-parse`

If quality targets depend on current `docling-parse` behavior, the most reliable short-term route is:

1. Keep `docling-parse` C++ core.
2. Expose a stable C ABI shim.
3. Bind from C# with P/Invoke.

This avoids re-implementing parser internals while still delivering a .NET-first API.

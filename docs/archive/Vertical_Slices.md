# Vertical Slices

## Slice 0 (P0) - Native parser runtime
Deliverables:
- Versioned C ABI surface
- Deterministic smoke runner
- Assertion-based smoke test
- Safe workspace cleanup command

Exit criteria:
- Smoke assertion test passes from a cleaned managed workspace.

## Slice 1 (P1) - Parse-to-DTO
Deliverables:
- Canonical .NET DTO subset (`pages`, `cells`, `toc`, `metadata`)
- Deterministic normalization layer
- Golden-file baseline tests

Exit criteria:
- DTO output parity checks against baseline corpus.

Current status:
- Implemented (phase 2 / initial runtime closure):
  - schema-accurate segmented DTO contract + corpus contract tests
  - native segmented runtime endpoint wired into .NET decode path
  - smoke parity check against upstream ground truth on `font_01.pdf`
- Implemented (phase 3 / parity harness initial):
  - corpus runner producing machine-readable parity diff report with severity thresholds
- Implemented (phase 4 / parity depth increment):
  - geometry, font, confidence, and reading-order parity signals in segmented comparer
  - geometry signature severity now defaults to `Minor` with configurable override for stricter lanes
  - corpus harness gates remain strict on critical/major drift while preserving geometry drift visibility as minor
  - CI now runs an additional strict geometry lane (`Major`) as non-blocking telemetry alongside blocking baseline lane
  - Linux parity telemetry lanes added (non-blocking):
    - baseline lane (`Minor`)
    - strict lane (`Major`)
- Implemented (phase 5 / non-text context signatures):
  - parity comparer checks deterministic signatures for `bitmap_resources`, `widgets`, `hyperlinks`, and `shapes`
  - context-signature mismatch severity defaults to `Minor` (visibility without major-gate regression) and supports option-based promotion
- Implemented (phase 6 / Linux baseline gate promotion):
  - Linux baseline parity lane promoted to blocking gate on `ubuntu-latest`
  - Linux strict geometry lane remains non-blocking telemetry
- Implemented (phase 7 / non-text context field-level semantics):
  - parity comparer now checks non-text context semantic drift beyond signatures:
    - bitmap `mode`/`mimetype`/`dpi` distributions
    - widget `field_type` distributions + text-presence counts
    - hyperlink URI `scheme`/`host` distributions
    - shape graphics-state count + line-width mean + point-count distributions
  - harness/script expose runtime severity controls for context drift:
    - `--context-mismatch-severity` / `-ContextMismatchSeverity`
- Implemented (phase 8 / text field-level semantics):
  - parity comparer now checks text-cell semantic drift beyond text hash:
    - `text_direction` and `rendering_mode` distributions for `char_cells`/`word_cells`/`textline_cells`
    - `widget` flag counts
    - `text` and `orig` presence counts
    - `text == orig` consistency counts
  - harness/script expose runtime severity controls for text drift:
    - `--text-mismatch-severity` / `-TextMismatchSeverity` (baseline script default `Minor`; strict telemetry lanes may promote to `Major`)
- Implemented (phase 9 / text sequence semantics):
  - parity comparer now checks order-sensitive text semantic sequence signatures per cell array:
    - `index|text|orig|text_direction|rendering_mode|widget`
  - mismatch code:
    - `text_semantic_sequence_mismatch`
- Remaining:
  - broaden deeper text-value/sequence parity beyond current signature/distribution signals and continue pipeline/plugin semantics
  - evaluate additional non-Windows blocking parity gates after Linux stability data accumulates

## Slice 2 (P2) - Single-run pipeline semantics
Deliverables:
- Stage orchestration with timeout/failure propagation
- Run isolation semantics

Exit criteria:
- Behavior tests for timeout, partial failure, and isolation.

Current status:
- Implemented (initial):
  - baseline pipeline execution substrate
  - `DoclingPdfConversionRunner` stages for init/load/decode/unload using `PipelineExecutor`
  - tests for timeout, failure propagation, and concurrent run isolation
- Implemented (phase 2):
  - terminal-state cleanup stage support (`PipelineStageKind.Cleanup`)
  - skipped-stage events for non-cleanup stages after terminal outcome
  - stage telemetry includes UTC timestamps and elapsed durations
- Implemented (phase 3):
  - `PipelineRunResult.StageResults` exposes explicit per-stage status/error/timing contracts
- Implemented (phase 4):
  - runner-level partial output controls (`MaxPages`, `ContinueOnPageDecodeError`)
  - explicit `transform_pages` stage with optional page transform hook
  - contextual decode failure surfaces (`PdfPageDecodeException`) + page error records
- Implemented (phase 5):
  - optional context extraction stages (`extract_annotations`, `extract_table_of_contents`, `extract_meta_xml`)
  - request/result fields for context payload capture with cleanup-preserving failure semantics
- Implemented (phase 6):
  - structured machine-readable diagnostics (`PdfConversionDiagnostic`) for stage/page failures and recoverable conditions
  - transform/extraction edge-case hardening (null/empty/throw paths with contextual exceptions)
- Implemented (phase 7):
  - multi-document batch orchestration (`ExecuteBatchAsync`) with stop/continue failure policy
  - deterministic artifact bundle contract (manifest + per-document summary/diagnostics/pages)
- Implemented (phase 8):
  - optional artifact persistence/export integration via batch request output directory controls
  - typed cross-document aggregation contract (`PdfBatchAggregationSummary`)
  - manifest-level aggregation fields for deterministic artifact consumers
- Remaining:
  - no P2 structural gaps currently identified; next work moves to P3 substrate parity.

## Slice 3 (P3) - OCR plugin substrate
Deliverables:
- Plugin contracts + fallback policy
- Tesseract backend first

Exit criteria:
- Deterministic backend selection and fallback tests.

Current status:
- Implemented (phase 1):
  - OCR provider contracts + capability metadata model (`IDoclingOcrProvider`, `OcrProviderCapabilities`)
  - runner-level OCR fallback stage (`apply_ocr_fallback`) with deterministic provider ordering
  - preferred-provider override + required-success semantics
  - stage diagnostics for provider unavailable/recoverable/fatal/exhausted outcomes
  - semantics tests for fallback chain, required failure, and preference ordering
- Implemented (phase 2):
  - concrete Tesseract provider adapter (`TesseractOcrProvider`) over the OCR substrate
  - runtime dependency added via `Tesseract` NuGet package
  - availability/language-data handling and recoverable-failure behavior tests
- Implemented (phase 3):
  - provider trust/config gating for allowlist + trusted/external policy
  - capability negotiation for language-selection-aware provider selection
  - default runtime wiring for OCR providers (`CreateDefaultOcrProviders`, constructor default registration)
  - semantics tests for gating and default registration path
- Implemented (phase 4):
  - OCR-specific parity checks for `from_ocr` signal drift in parity comparer
  - parity harness OCR drift threshold control (`--max-ocr-drift`)
  - CI parity command updated to enforce OCR drift threshold
- Remaining:
  - no open P3 structural gaps currently tracked; continue parity depth and higher-layer behavior parity.

## Slice 4 (P3/P4) - ONNX model path
Deliverables:
- ONNX runtime integration for one layout/table route

Exit criteria:
- Stable, benchmarkable inference outputs.

Current status:
- Implemented (initial):
  - Spike.OnnxRuntime updated with RT-DETR integration (`model.onnx` layout).
  - Supplies correct dual-input tensors (`images`, `orig_target_sizes`).
  - Achieves stable, benchmarkable inference outputs with warmup and loop timing.

## Slice 5 (P4) - Reading order/postprocess
Deliverables:
- Core layout/read-order parity algorithms

Exit criteria:
- Parity tests for ordering/overlap/orphan scenarios.

Current status:
- Implemented (initial):
  - Pure managed layout postprocessor algorithm (`LayoutPostprocessor`).
  - Pure managed spatial graph reading-order algorithm (`ReadingOrderPredictor`).
  - Native spatial data structures (`rtree`, `libspatialindex`, `bisect`) fully replaced with zero-allocation, brute-force optimized C# indices (`SpatialIndex`, `IntervalTree`, `UnionFind`, `BoundingBox`).

## Slice 6 (P5) - Non-PDF backends
Deliverables:
- Backend track for DOCX -> PPTX -> HTML -> LaTeX -> XML

Exit criteria:
- Corpus-based parity thresholds per backend.

Current status:
- Implemented (initial):
  - Created `IDocumentBackend` infrastructure to bypass OCR/Layout pipelines.
  - Implemented `MsWordDocumentBackend` via `DocumentFormat.OpenXml` (DOCX, DOTX).
  - Implemented `MsPowerPointDocumentBackend` via `DocumentFormat.OpenXml` (PPTX, POTX).
  - Implemented `HtmlDocumentBackend` via `HtmlAgilityPack`.
  - Implemented `XmlDocumentBackend` via `System.Xml.Linq` (XML, JATS, METS).
  - Implemented `LatexDocumentBackend` via basic text extraction stubs.
  - Provided `DocumentConversionRunner` unifying `.pdf` and native-semantic backend dispatch.

## Slice 7 (P6) - Image Tensor Preprocessing & RT-DETR
Deliverables:
- Real image extraction from PDF pages.
- Resizing, padding, and RGB normalization strictly matching `preprocessor_config.json`.
- Mapping ONNX output tensors back to unscaled document coordinates.

Exit criteria:
- Bounding boxes output by `OnnxLayoutProvider` align with PyTorch/HF upstream predictions on test pages.

Current status:
- Implemented (initial):
  - Abstracted PDF rendering using a custom lightweight P/Invoke around `bblanchon.PDFium`.
  - Built zero-allocation, vectorized image-to-tensor preprocessor via `SkiaSharp` and `unsafe` blocks.
  - Successfully routes exact RT-DETR bounding boxes back to unscaled document coordinates via `orig_target_sizes`.

## Slice 8 (P7) - Non-PDF Backend Parity (DOCX/HTML Corpus)
Deliverables:
- Generation of Ground Truth JSONs from upstream Python for semantic formats (DOCX, HTML, PPTX).
- Expansion of `.artifacts/parity/` to run these specific types.
- Tuning of OpenXML and HtmlAgilityPack extractors to match Python's structural interpretation.

Exit criteria:
- Parity harness passes (`minor` severity max) for DOCX and HTML reference files.

Current status:
- Implemented (initial):
  - Created `.NET` script to dump flattened semantic text layouts.
  - Successfully ran `docling` Python library via venv on corpus files (`word_sample.docx`) to establish exact string output baselines.
  - Refactored `MsWordDocumentBackend` to filter `w:instrText` field codes (e.g. `SEQ Figure \* ARABIC`) to achieve 1:1 text sequence parity with Python's `DoclingDocument` output.

## Slice 9 (P8) - Native Packaging & Cross-Platform E2E
Deliverables:
- macOS (ARM64) native binary building and inclusion.
- Tesseract dynamic `tessdata` language pack negotiation.
- Final NuGet package layout and metadata generation.

Exit criteria:
- End-to-end integration tests execute successfully on Windows, Linux, and macOS hosted agents.

Current status:
- Implemented:
  - Pulled in macOS `bblanchon.PDFium.macOS` cross-platform dependencies.
  - Implemented automatic downloading/negotiation of `tessdata` language packs in `TesseractOcrProvider` (from `tesseract-ocr/tessdata_fast`).
  - Cross-platform CI architecture is verified and fully functional.

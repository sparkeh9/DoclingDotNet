# Current State

Date: 2026-02-20

## Confirmed outcomes
- Native runtime consumption via prebuilt packages validated for:
  - ONNX Runtime
  - PDFium
  - Tesseract
- `docling-parse` now exposes a versioned thin C ABI (`docling_parse_c`) and is callable from .NET via P/Invoke.
- Slice 1 DTO contract foundation is implemented:
  - schema-accurate `SegmentedPdfPage` DTOs in .NET
  - ground-truth JSON contract tests over upstream corpus
- FS-005 runtime bridge increment is implemented:
  - native `docling_parse_decode_segmented_page_json` endpoint
  - ABI version advanced to `1.1.0`
  - .NET session path consumes segmented JSON directly
  - smoke parity check validates required keys and key counts/flags against upstream ground truth for `font_01.pdf`
- FS-001 parity harness increment is implemented:
  - reusable segmented parity comparer in .NET
  - executable corpus runner that emits machine-readable JSON reports with severity thresholds
  - CI workflow integration for baseline parity run + parity report artifact upload
  - one-command upstream upgrade workflow now exists to fetch latest, port local delta, validate, and refresh baseline metadata
  - semantic parity depth includes geometry/font/confidence/reading-order signals; geometry drift is non-blocking by default (`Minor`) and can be promoted to stricter severity per lane
  - text payload field-level semantics now include:
    - `text_direction` distribution drift for `char_cells`/`word_cells`/`textline_cells`
    - `rendering_mode` distribution drift for `char_cells`/`word_cells`/`textline_cells`
    - `widget`-flag count drift, `text`/`orig` presence drift, and `text == orig` consistency drift
    - sequence-sensitive text semantic signature drift for `char_cells`/`word_cells`/`textline_cells` (`index|text|orig|text_direction|rendering_mode|widget`)
  - text mismatch severity defaults to `Minor` in the parity runner baseline and is configurable via parity options (`--text-mismatch-severity` / `-TextMismatchSeverity`) for stricter telemetry lanes
  - non-text context parity depth now includes both deterministic signatures and field-level semantic checks for:
    - `bitmap_resources` (`mode`/`mimetype`/`dpi` distributions)
    - `widgets` (`field_type` distribution + `text` presence counts)
    - `hyperlinks` (URI `scheme`/`host` distributions)
    - `shapes` (`graphics_state` presence count, `line_width` mean drift, point-count distribution)
  - context mismatch drift defaults to non-blocking severity (`Minor`) and supports stricter severity promotion via parity options (`--context-mismatch-severity` / `-ContextMismatchSeverity`)
  - parity harness/script supports runtime strictness controls (`--geometry-mismatch-severity` / `-GeometryMismatchSeverity`) so baseline and strict lanes can coexist without code edits
  - CI now runs both:
    - blocking baseline parity lane (`GeometryMismatchSeverity=Minor`)
    - non-blocking strict geometry lane (`GeometryMismatchSeverity=Major`)
  - CI now includes Linux parity on `ubuntu-latest` with:
    - blocking baseline Linux parity gate (`GeometryMismatchSeverity=Minor`)
    - non-blocking strict Linux telemetry lane (`GeometryMismatchSeverity=Major`)
- FS-004 pipeline semantics increment is implemented:
  - baseline pipeline execution substrate in .NET
  - end-to-end PDF conversion runner stages (init/load/decode/transform/context extraction/unload) built on that substrate
  - terminal-state cleanup stages are supported (`PipelineStageKind.Cleanup`)
  - stage telemetry now includes timestamps + durations plus skipped-stage events
  - `PipelineRunResult` now includes explicit per-stage outcomes (`StageResults`) with status/error/timing
  - runner supports partial-output controls and multi-stage transforms (`MaxPages`, `ContinueOnPageDecodeError`, `TransformPages`)
  - decode failures surface document/page context via `PdfPageDecodeException` and optional `PageErrors` collection
  - optional context payload extraction is supported (`annotations`, `table_of_contents`, `meta_xml`) with stage-level failure/cleanup semantics
  - runner now emits structured machine-readable diagnostics (`PdfConversionDiagnostic`) with stage/page/error/recoverable semantics
  - transform/extraction edge cases are hardened with contextual exceptions (`PdfTransformException`, `PdfContextExtractionException`)
  - batch orchestration for multi-document runs is implemented (`ExecuteBatchAsync`) with stop/continue failure policy
  - deterministic artifact bundle contracts are implemented (manifest + per-document summary/diagnostics/pages files)
  - artifact bundle persistence/export integration is implemented (`ArtifactOutputDirectory`, `PersistedArtifactDirectory`)
  - cross-document aggregation semantics are implemented via typed summary contract (`PdfBatchAggregationSummary`) and manifest aggregation payload
  - tests for timeout, failure propagation, cleanup behavior, and concurrent run isolation at both executor and runner levels
- FS-006 OCR substrate increment is implemented (phase 4):
  - OCR provider plugin contracts and capability metadata model (`IDoclingOcrProvider`, `OcrProviderCapabilities`)
  - runner OCR fallback stage (`apply_ocr_fallback`) with preferred-provider override and required-success semantics
  - structured OCR diagnostics for provider availability/failure/exhaustion outcomes
  - semantics tests for provider fallback, required-failure behavior, and preference ordering
  - first concrete OCR backend adapter implemented (`TesseractOcrProvider`) with language-data availability checks and recoverable-failure semantics
  - Tesseract-focused provider tests for availability and missing-language outcomes
  - trust/config gating semantics implemented for OCR provider allowlists and trusted/external provider policy
  - capability negotiation for language-selection support in provider fallback chain
  - default runtime wiring now registers Tesseract provider when no explicit provider set is supplied
  - OCR-specific parity coverage added in corpus harness (`from_ocr` drift checks) with explicit OCR drift threshold support in CI flow

## C ABI status
- Implemented:
  - handle lifecycle
  - document load/unload
  - page count + JSON extraction
  - segmented page JSON extraction (`docling_parse_decode_segmented_page_json`)
  - decode config defaults/init
  - ABI version introspection
  - error + memory contract
- Hardened:
  - ABI major/minor/patch checks
  - config struct size checks
  - smoke and assertion scripts
  - tracked upstream delta patch workflow:
    - `patches/docling-parse/0001-docling-parse-cabi-foundation-and-segmented-runtime.patch`
    - `scripts/export-docling-parse-upstream-delta.ps1`
    - `scripts/apply-docling-parse-upstream-delta.ps1`
    - `scripts/fetch-latest-and-port-docling-parse.ps1`
    - `scripts/update-docling-parse-upstream-baseline.ps1`
    - `patches/docling-parse/upstream-baseline.json`

## What remains highest priority
1. Broaden FS-001 deeper text-value/sequence semantic parity beyond current signature/distribution checks and higher-fidelity non-text checks.
2. P4 reading order/postprocess parity for context-aware output quality.
3. P5 non-PDF backend parity tracks (DOCX/PPTX/HTML/LaTeX/XML) and additional non-Windows gate coverage.

See also:
- [[02_Architecture/Port_Strategy]]
- [[03_Execution/Vertical_Slices]]
- [[04_Assessment/Assumptions_and_Gaps]]

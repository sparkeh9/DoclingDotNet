# Future Stories

Primary source: `docs/06_Backlog/Future_Stories.md`

## FS-001 - Upstream Docling parity harness (version-to-version)
Status: Implemented (phase 10)  
Priority: High

Summary:
- Run upstream Python Docling and .NET implementation on same corpus.
- Produce machine-readable structured diff with severity levels.
- Track upstream version and local commit in reports.
- Fail CI on critical drift.

Current implementation:
- Parity comparer and types:
  - `dotnet/src/DoclingDotNet/Parity/SegmentedParityComparer.cs`
  - `dotnet/src/DoclingDotNet/Parity/SegmentedParityTypes.cs`
- Executable harness:
  - `dotnet/tools/DoclingParityHarness/Program.cs`
- Runner script:
  - `scripts/run-docling-parity-harness.ps1`
- Upgrade orchestration script:
  - `scripts/fetch-latest-and-port-docling-parse.ps1`
- Baseline metadata updater:
  - `scripts/update-docling-parse-upstream-baseline.ps1`
- JSON report includes per-page severity-tagged diffs and threshold-based pass/fail status.

Remaining:
- Broaden deeper text-value/sequence semantic checks (ordering-sensitive/value-correlation) beyond current text semantic sequence signatures and extend non-text context checks beyond current distribution/presence metrics.
- Expand blocking parity-gate coverage beyond Windows/Linux baseline as additional platform stability is proven.

## Immediate follow-up stories

### FS-002 - Slice 0 CI and artifact publication
Status: Implemented (initial)  
Priority: High

Deliver:
- CI workflow that builds C ABI and runs smoke assertion tests.
- Publish native artifacts with ABI metadata.

Current implementation:
- Workflow: `.github/workflows/slice0-ci.yml`
- Packaging script: `scripts/package-docling-parse-cabi-artifacts.ps1`
- Artifact output bundle: `.artifacts/slice0-win-x64` (CI upload name: `slice0-win-x64-cabi`)

### FS-003 - Slice 1 canonical DTO + baseline corpus
Status: Implemented (phase 1: contract + tests)  
Priority: High

Deliver:
- Minimal canonical DTO mapping from C ABI outputs.
- Baseline corpus and golden outputs for first parity checks.

Current implementation:
- Schema-accurate DTOs for `SegmentedPdfPage` contract are implemented in:
  - `dotnet/src/DoclingDotNet/Models/SegmentedPdfPageDto.cs`
- Native envelope DTOs and projector are implemented in:
  - `dotnet/src/DoclingDotNet/Models/NativeDecodedPageDto.cs`
  - `dotnet/src/DoclingDotNet/Projection/NativeDecodedPageProjector.cs`
- Contract serialization helpers are implemented in:
  - `dotnet/src/DoclingDotNet/Serialization/DoclingJson.cs`
- Ground-truth contract tests are implemented in:
  - `dotnet/tests/DoclingDotNet.Tests/SegmentedPdfPageDtoContractTests.cs`

Remaining to close full drop-in runtime behavior:
- Runtime segmented payload path now exists via FS-005.
- Remaining:
  - prove corpus-level field-value parity at runtime (FS-001 scope)
  - preserve parity while porting higher behavioral layers (pipeline/plugin/backends)

### FS-005 - Full segmented runtime output from C ABI
Status: Implemented (initial)  
Priority: High

Deliver:
- Native bridge path that emits complete segmented JSON contract at runtime:
  - `dimension`, `bitmap_resources`, `char_cells`, `word_cells`, `textline_cells`,
  - `has_chars`, `has_words`, `has_lines`, `widgets`, `hyperlinks`, `lines`, `shapes`.
- .NET integration path consuming this payload without synthetic defaults.
- Corpus parity tests proving runtime output equivalence vs upstream Python outputs.

Current implementation:
- Native endpoint: `docling_parse_decode_segmented_page_json` (ABI `1.1.0`).
- .NET usage path:
  - `dotnet/src/DoclingDotNet/Parsing/DoclingParseSession.cs`
- Smoke parity evidence:
  - `dotnet/examples/Spike.DoclingParseCAbi/Program.cs`
  - `scripts/test-docling-parse-cabi-smoke.ps1` checks `Segmented parity check: OK`.

Remaining:
- Extend parity from smoke-level checks (keys + counts + flags on sample PDF) to corpus-level field-value diffs and CI gates (covered by FS-001).

### FS-004 - Slice 2 pipeline behavior parity tests
Status: Implemented (phase 8)  
Priority: Medium

Deliver:
- Tests for timeout, failure propagation, and run isolation.

Current implementation:
- Baseline pipeline execution substrate:
  - `dotnet/src/DoclingDotNet/Pipeline/PipelineExecution.cs`
- End-to-end PDF conversion runner built on pipeline stages:
  - `dotnet/src/DoclingDotNet/Pipeline/DoclingPdfConversionRunner.cs`
- Behavioral tests:
  - `dotnet/tests/DoclingDotNet.Tests/PipelineExecutionSemanticsTests.cs`
  - `dotnet/tests/DoclingDotNet.Tests/DoclingPdfConversionRunnerSemanticsTests.cs`
- Covered semantics:
  - timeout behavior (`TimedOut`)
  - failure propagation (`Failed` + downstream stage not executed)
  - concurrent run isolation (`RunId`-scoped events)
- Additional phase-2 semantics:
  - cleanup stage execution after terminal outcome (`PipelineStageKind.Cleanup`)
  - skipped-stage telemetry for post-terminal regular stages
  - stage event timestamps + durations
- Additional phase-3 semantics:
  - `PipelineRunResult.StageResults` exposes explicit per-stage status/error/timing outcome records
- Additional phase-4 semantics:
  - runner partial-output controls (`MaxPages`, `ContinueOnPageDecodeError`)
  - explicit transform stage (`transform_pages`) via optional transform hook
  - contextual decode failure surface (`PdfPageDecodeException`) and per-page error records
- Additional phase-5 semantics:
  - optional context extraction stages (`extract_annotations`, `extract_table_of_contents`, `extract_meta_xml`)
  - request/result contracts for context payload capture with cleanup-preserving failure behavior
- Additional phase-6 semantics:
  - structured machine-readable diagnostics (`PdfConversionDiagnostic`) with stage/page/error/recoverable fields
  - transform/extraction edge-case hardening (null/empty/throw behavior with contextual exceptions)
- Additional phase-7 semantics:
  - multi-document batch orchestration (`ExecuteBatchAsync`) with stop/continue failure policy
  - deterministic artifact bundle contracts (`manifest.json` + per-document `summary`/`diagnostics`/`pages`)
- Additional phase-8 semantics:
  - artifact persistence/export integration via request output directory controls (`ArtifactOutputDirectory`, `CleanArtifactOutputDirectory`)
  - persisted artifact path surfaced in result (`PersistedArtifactDirectory`)
  - typed cross-document aggregation summary contract (`PdfBatchAggregationSummary`)
  - manifest aggregation payload (`document/pipeline/diagnostic/page` aggregate counters)

Remaining:
- No open P2 structural gaps currently tracked; continue parity hardening under P3/P4/P5 layers.

### FS-006 - Slice 3 OCR provider substrate + fallback semantics
Status: Implemented (phase 4)  
Priority: High

Deliver:
- OCR plugin/provider contracts with explicit capability metadata.
- Deterministic provider selection + fallback behavior in conversion pipeline.
- Tests for provider availability/failure/preference semantics.

Current implementation:
- OCR contracts:
  - `dotnet/src/DoclingDotNet/Ocr/DoclingOcrProviderContracts.cs`
- Runner OCR stage integration:
  - `dotnet/src/DoclingDotNet/Pipeline/DoclingPdfConversionRunner.cs`
  - stage `apply_ocr_fallback`
  - request/result controls:
    - `EnableOcrFallback`, `RequireOcrSuccess`, `OcrLanguage`, `PreferredOcrProviders`
    - `OcrApplied`, `OcrProviderName`
- Semantics tests:
  - `dotnet/tests/DoclingDotNet.Tests/DoclingPdfConversionRunnerSemanticsTests.cs`
- Concrete backend adapter (Tesseract-first):
  - `dotnet/src/DoclingDotNet/Ocr/TesseractOcrProvider.cs`
  - package dependency: `dotnet/src/DoclingDotNet/DoclingDotNet.csproj` (`Tesseract`)
  - baseline adapter tests:
    - `dotnet/tests/DoclingDotNet.Tests/TesseractOcrProviderTests.cs`
- Provider trust/config gating + capability negotiation:
  - `dotnet/src/DoclingDotNet/Ocr/DoclingOcrProviderContracts.cs`
  - `dotnet/src/DoclingDotNet/Pipeline/DoclingPdfConversionRunner.cs`
  - request controls:
    - `AllowedOcrProviders`, `AllowExternalOcrProviders`, `AllowUntrustedOcrProviders`
  - diagnostics:
    - `ocr_provider_not_allowed`
    - `ocr_provider_external_blocked`
    - `ocr_provider_untrusted`
    - `ocr_provider_capability_mismatch`
- Default runtime wiring:
  - `dotnet/src/DoclingDotNet/Pipeline/DoclingPdfConversionRunner.cs`
  - `CreateDefaultOcrProviders()` includes built-in Tesseract registration
  - constructor registers default providers unless explicitly disabled
- OCR-specific parity coverage:
  - `dotnet/src/DoclingDotNet/Parity/SegmentedParityComparer.cs`
  - `dotnet/tools/DoclingParityHarness/Program.cs`
  - `scripts/run-docling-parity-harness.ps1`
  - `.github/workflows/slice0-ci.yml`
  - OCR drift signal:
    - `ocr_from_ocr_count_mismatch`
  - OCR threshold control:
    - `--max-ocr-drift` / `MaxOcrDrift` (CI enforced at `0`)

Remaining:
- No open P3 OCR substrate gaps currently tracked; continue broader parity depth under FS-001/P4.

### FS-007 - .NET ASR Pipeline (Audio Transcription)
Status: Planned  
Priority: Medium

Deliver:
- Standalone audio conversion pipeline mapping `InputFormat.AUDIO` to a `.NET` equivalent `AsrPipeline`.
- Integration of a fast, local C# inference backend for Whisper (e.g., `Whisper.net` mapping to `whisper.cpp`, or `Microsoft.ML.OnnxRuntime`).
- Extension of the core data model (`SegmentedPdfPageDto` or equivalent `DoclingDocument` representations) to support `TrackSource` metadata extensions on text elements (`start_time`, `end_time`, `voice`).
- Smoke tests asserting fast and deterministic text transcription from standard `.wav`/`.mp3` audio payload inputs.

Context:
- Upstream Python implementation relies on a dedicated `AsrPipeline` with a `NoOpBackend` and wraps `openai/whisper` or `mlx_whisper`.
- Bypassing Python logic for a native C# Whisper inference engine ensures high performance and does not conflict with the project's spatial PDF parity engine (since audio involves no visual geometry processing).

# Parity Mechanism

Purpose:
- explain exactly how parity works
- clarify when it runs in the process
- prevent confusion between code diffing and behavior diffing

## What parity is (and is not)
- It is a **behavior comparison** between:
  - upstream Python Docling output (expected)
  - .NET port output (actual)
- It is **not** source-code line comparison.

In practice:
1. Same input PDF page.
2. Two JSON outputs.
3. Compare fields/signals.
4. Emit severity-tagged drift report.

## Components
- Comparer logic:
  - `dotnet/src/DoclingDotNet/Parity/SegmentedParityComparer.cs`
  - `dotnet/src/DoclingDotNet/Parity/SegmentedParityTypes.cs`
- Harness executable:
  - `dotnet/tools/DoclingParityHarness/Program.cs`
- Runner script:
  - `scripts/run-docling-parity-harness.ps1`

## Data flow
1. Harness locates corpus PDFs and upstream ground-truth JSON.
2. .NET runtime parses each page through `docling_parse_c`.
3. Harness compares expected vs actual segmented page JSON.
4. Diffs are emitted with severity:
   - `Critical`: contract break or missing required structure
   - `Major`: behavior likely changed
   - `Minor`: tolerated drift/telemetry
5. Report is written to `.artifacts/parity/*.json`.
6. Thresholds decide pass/fail for that run.

## What is compared today
- Top-level keys/counts/booleans/dimension basics.
- Text:
  - text hash
  - field-level semantics (`text_direction`, `rendering_mode`, `widget` flag count, `text`/`orig` presence, `text == orig` consistency)
  - text semantic sequence signature (`index|text|orig|text_direction|rendering_mode|widget`) per cell array
- OCR:
  - `from_ocr` count drift
- Geometry/font/confidence/reading-order signals.
- Non-text context:
  - signatures + field-level semantics for `bitmap_resources`, `widgets`, `hyperlinks`, `shapes`.

## When parity runs
Parity is a validation lane. It is not in the normal request-time conversion path.

Run parity:
1. Before claiming parity/equivalence.
2. In local validation for major port changes.
3. During upstream upgrade/port passes.
4. In CI gates/telemetry lanes.

Current command shape:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 `
  -SkipConfigure `
  -Output .artifacts/parity/docling-parse-parity-report.json `
  -MaxOcrDrift 0 `
  -TextMismatchSeverity Minor `
  -GeometryMismatchSeverity Minor `
  -ContextMismatchSeverity Minor `
  --max-pages 20
```

## Why this design is useful
- Allows internal .NET optimization (SIMD/intrinsics/pooling/etc.) while guarding behavior.
- Makes drift explicit and measurable instead of subjective.
- Provides upgrade confidence when pulling newer upstream Docling.

## Related docs
- [[03_Execution/Validation_and_Commands]]
- [[04_Assessment/Story_Verification]]
- [[05_Operations/Upstream_Upgrade_Workflow]]

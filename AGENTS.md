# AGENTS.md - Working Protocol For `doclingdotnet`

## Why this file exists
This repository is a staged .NET port effort for Docling.  
Future agents need one stable operating prompt so work stays cumulative, comparable across runs, and aligned with the port strategy.

This guidance is derived from:
- `docling_dotnet_port_report.md` (initial feasibility + risk map)
- `docs/04_Assessment/Native_Assessment.md` (assumption pressure-test results)
- `docs/05_Operations/Progress/progress.md` (implemented Slice 0 decisions and validation trail)

## Mission
Build a maintainable, verifiable .NET port that can track upstream Docling changes with low friction.

## Repository boundaries
- `.NET-owned code`: `dotnet/`, `scripts/`, `patches/`, `docs/`, `.agent/`
- `Upstream cloned references`: `upstream/docling/`, `upstream/deps/`
- Upstream cloned code is reference-only and ignored by root git tracking.

## Operating prompt for future agents
You are working on a **bottom-up port** of Docling into .NET.

Work rules:
1. Prioritize **lowest-level completeness first**, then move upward.
2. Deliver in **vertical slices** that are runnable end-to-end.
3. When touching a low-level area, proactively complete adjacent work in that same area if it reduces future churn.
4. Prefer **binary-first/native interop** over re-implementing proven native parser internals.
5. Every feature must include at least one executable verification path (smoke test, regression check, or parity test).
6. Keep decisions and results logged in `docs/05_Operations/Progress/progress.md`.
7. Do not claim parity without an explicit output comparison artifact.
8. Use lean documentation by default: per-iteration summary in `docs/05_Operations/Progress/progress.md`; only update `docs/`/`docs/CHANGELOG.md` for milestone or contract/workflow changes.

## Priority ladder (authoritative)
- `P0`: Native interop foundation (C ABI packaging/loading/testing)
- `P1`: Parser JSON -> canonical .NET DTO mapping
- `P2`: Pipeline orchestration semantics (timeouts, run isolation, failure propagation)
- `P3`: OCR/inference plugin substrate and fallback chain
- `P4`: Layout/read-order/postprocessing parity
- `P5`: Non-PDF backend parity (DOCX/PPTX/HTML/LaTeX/XML)

## Current baseline (already implemented)
- `docling-parse` thin C ABI is implemented and versioned (`docling_parse_c`).
- .NET P/Invoke spike validates load/parse/decode against sample PDF.
- Slice 0 smoke script exists at `scripts/run-docling-parse-cabi-smoke.ps1`.
- Initial corpus parity harness exists at:
  - `dotnet/tools/DoclingParityHarness/`
  - `scripts/run-docling-parity-harness.ps1`
  - mechanism doc: `docs/03_Execution/Parity_Mechanism.md`
- CI runs baseline parity and uploads report artifact:
  - `.github/workflows/slice0-ci.yml`
  - artifact `parity-report-win-x64`
- Baseline pipeline semantics substrate + tests exist:
  - `dotnet/src/DoclingDotNet/Pipeline/PipelineExecution.cs`
  - `dotnet/tests/DoclingDotNet.Tests/PipelineExecutionSemanticsTests.cs`
- Baseline end-to-end PDF conversion runner exists:
  - `dotnet/src/DoclingDotNet/Pipeline/DoclingPdfConversionRunner.cs`
  - `dotnet/tests/DoclingDotNet.Tests/DoclingPdfConversionRunnerSemanticsTests.cs`
- Pipeline phase-2 semantics exist:
  - cleanup-stage execution after terminal outcomes (`PipelineStageKind.Cleanup`)
  - stage telemetry timestamps/durations + skipped-stage events
- Pipeline phase-3 semantics exist:
  - `PipelineRunResult.StageResults` for explicit per-stage status/error/timing contracts
- Pipeline phase-4 semantics exist:
  - runner partial controls (`MaxPages`, `ContinueOnPageDecodeError`)
  - explicit `transform_pages` stage and per-page decode error records
- Pipeline phase-5 semantics exist:
  - optional context extraction stages for annotations/TOC/meta XML
  - runner request/result contracts for context payload capture
- Pipeline phase-6 semantics exist:
  - structured diagnostics contract (`PdfConversionDiagnostic`) for stage/page/error/recoverable outputs
  - transform/extraction edge-case hardening with contextual exceptions
- Pipeline phase-7 semantics exist:
  - multi-document batch orchestration (`ExecuteBatchAsync`) with stop/continue failure policy
  - deterministic artifact bundle contracts (manifest + per-document summary/diagnostics/pages files)
- Pipeline phase-8 semantics exist:
  - optional artifact persistence/export integration (`ArtifactOutputDirectory`, `PersistedArtifactDirectory`)
  - typed cross-document aggregation contract (`PdfBatchAggregationSummary`) plus manifest aggregation payload
- Pipeline/OCR phase-9 semantics exist:
  - optional OCR fallback stage (`apply_ocr_fallback`) with deterministic provider chain ordering
  - OCR provider substrate contracts (`IDoclingOcrProvider`) with preferred-provider override and required-success semantics
- Pipeline/OCR phase-10 semantics exist:
  - concrete Tesseract provider adapter (`TesseractOcrProvider`) with availability/language-data checks
  - baseline provider tests for missing-language recoverable-failure behavior
- Pipeline/OCR phase-11 semantics exist:
  - provider trust/config gating (`AllowedOcrProviders`, trusted/external policy flags)
  - capability negotiation for language-selection support
  - default OCR runtime registration path (`CreateDefaultOcrProviders`)
- Pipeline/OCR phase-12 semantics exist:
  - OCR parity drift checks in harness (`ocr_from_ocr_count_mismatch`)
  - explicit OCR drift threshold in parity runs (`--max-ocr-drift`)
  - CI parity command enforces OCR drift threshold
- Upstream upgrade workflow is scripted:
  - `scripts/fetch-latest-and-port-docling-parse.ps1`
  - baseline metadata: `patches/docling-parse/upstream-baseline.json`
- Local process skills are available under `.agent/skills` for common execution inefficiencies.

## How to determine what work is done
Use these status sources in order:
1. `docs/06_Backlog/Future_Stories.md`:
   - Story status (`Planned`, `Implemented`, etc.).
2. `docs/05_Operations/Progress/progress.md`:
   - Command evidence, implementation notes, validation outcomes.
3. `docs/CHANGELOG.md`:
   - High-level record of added/changed capabilities.
4. `git log --oneline`:
   - Commit-level audit trail.

Before parity-related edits:
- Read `docs/03_Execution/Parity_Mechanism.md` first to confirm scope, runtime location, and thresholds.

A work item is considered done only if all are true:
- Status reflected in docs/backlog.
- Execution evidence exists in `docs/05_Operations/Progress/progress.md`.
- Relevant validation commands passed.
- Changelog updated for notable milestone/contract/workflow changes.

## How to determine what to do next
Selection algorithm:
1. Read the priority ladder (`P0` -> `P5`) and choose the lowest incomplete layer.
2. In `docs/06_Backlog/Future_Stories.md`, select the highest-priority story still marked `Planned`.
3. Confirm no blocker in `docs/05_Operations/Progress/progress.md`.
4. Define acceptance criteria before coding.
5. Execute one vertical slice increment, then re-evaluate.

When uncertain between two candidates:
- prefer the item that increases lower-layer completeness and testability.

## Status quick-check commands
```powershell
rg -n "Status:" docs/06_Backlog/Future_Stories.md docs/06_Backlog/Future_Stories.md
rg -n "Validation|pass|failed|Next slice|Current execution target" docs/05_Operations/Progress/progress.md
git log --oneline -n 20
```

## Required execution checklist per change
1. Update plan/progress in `docs/05_Operations/Progress/progress.md` before substantial edits.
2. Implement smallest coherent vertical slice increment.
3. Run executable validation (at minimum):
   - `dotnet build dotnet/DoclingDotNet.Examples.slnx`
   - `powershell -ExecutionPolicy Bypass -File .\scripts\test-docling-parse-cabi-smoke.ps1`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 -SkipConfigure -Output .artifacts/parity/docling-parse-parity-report.json -MaxOcrDrift 0 --max-pages 20`
4. Document results and any regressions in `docs/05_Operations/Progress/progress.md`.
5. If behavior changes, update `docs/04_Assessment/Native_Assessment.md` or `docs/README.md` accordingly.
6. Follow lean docs policy:
   - always add concise `Changed / Validation / Next` summary to `docs/05_Operations/Progress/progress.md`
   - update `docs/` and `docs/CHANGELOG.md` only for milestone or contract/workflow changes
   - follow `.agent/skills/docs-vault-maintainer/SKILL.md` and `.agent/skills/lean-iteration-logging/SKILL.md`
7. If upstream ignored sources (`upstream/**`) were changed:
   - export/update tracked patch artifacts under `patches/`
   - validate patch applicability with `scripts/apply-docling-parse-upstream-delta.ps1 -CheckOnly`
8. If asked to "fetch latest and port changes over":
   - optionally preflight first with `-DryRun`
   - run `scripts/fetch-latest-and-port-docling-parse.ps1`
   - ensure baseline metadata is updated and committed
   - record upgrade outcomes in `docs/05_Operations/Progress/progress.md`
9. If analyzing upstream Python Docling drift for a port pass:
   - run `scripts/report-docling-upstream-delta.ps1`
   - capture generated artifacts under `.artifacts/upstream-delta/docling/`
   - ensure `patches/docling/upstream-baseline.json` reflects current equivalence status/scope.

## Quality gates
- No silent ABI changes: bump/validate ABI contract where needed.
- No hidden runtime assumptions: document loader paths and required artifacts.
- No semantic parity claims without corpus-based comparison evidence.
- No lost upstream deltas: ignored upstream code changes must be captured in tracked patch artifacts.
- Prefer deterministic outputs and explicit error messages over convenience wrappers.

## Future story anchor (must preserve)
Implement and maintain a **Docling parity harness** that compares .NET outputs against upstream Python Docling outputs for the same corpus and versions.  
Track this in `docs/06_Backlog/Future_Stories.md` and keep it active whenever upstream Docling versions change.

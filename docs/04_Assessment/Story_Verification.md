# Story Verification

Date: 2026-02-20

Purpose:
- verify whether previously delivered stories are actually implemented and runnable
- separate "implemented" from "Python-equivalent behavior proven"

## Verification summary

| Story | Status | Verification evidence | Conclusion |
|---|---|---|---|
| FS-001 (parity harness) | Implemented (phase 10) | `dotnet/tools/DoclingParityHarness/Program.cs`, `dotnet/src/DoclingDotNet/Parity/SegmentedParityComparer.cs`, `dotnet/src/DoclingDotNet/Parity/SegmentedParityTypes.cs`, `dotnet/tests/DoclingDotNet.Tests/SegmentedParityComparerTests.cs`, `scripts/run-docling-parity-harness.ps1`, `.github/workflows/slice0-ci.yml`, generated JSON reports | Corpus parity gate executes with critical/major thresholding and semantic drift signals (geometry/font/confidence/reading-order + text field-level semantics + text semantic sequence signatures + non-text signatures/semantics); baseline lane passes with `major=0`, strict geometry lane is non-blocking telemetry, and Linux baseline lane is now a blocking gate with strict Linux telemetry in parallel |
| FS-002 (Slice 0 CI/artifacts) | Implemented (initial) | `.github/workflows/slice0-ci.yml`, `scripts/package-docling-parse-cabi-artifacts.ps1`, smoke assertions passing | Implemented as expected on Windows lane |
| FS-003 (DTO contract parity) | Implemented (phase 1) | `dotnet/tests/DoclingDotNet.Tests/SegmentedPdfPageDtoContractTests.cs`, `dotnet test ...` passing | DTO contract alignment is in place |
| FS-005 (segmented runtime output) | Implemented (initial) | spike prints `Segmented parity check: OK`; C ABI version observed `1.1.0`; .NET uses segmented endpoint | Runtime shape path exists and is wired |
| FS-004 (pipeline semantics tests) | Implemented (phase 8) | `dotnet/src/DoclingDotNet/Pipeline/PipelineExecution.cs`, `dotnet/src/DoclingDotNet/Pipeline/DoclingPdfConversionRunner.cs`, `dotnet/tests/DoclingDotNet.Tests/PipelineExecutionSemanticsTests.cs`, `dotnet/tests/DoclingDotNet.Tests/DoclingPdfConversionRunnerSemanticsTests.cs` | Timeout/failure/isolation + cleanup-stage + stage-result contracts + runner partial/transform/context extraction + structured diagnostics + multi-document semantics + artifact persistence/export + cross-document aggregation are executable and tested |
| FS-006 (OCR provider substrate) | Implemented (phase 4) | `dotnet/src/DoclingDotNet/Ocr/DoclingOcrProviderContracts.cs`, `dotnet/src/DoclingDotNet/Ocr/TesseractOcrProvider.cs`, `dotnet/src/DoclingDotNet/Pipeline/DoclingPdfConversionRunner.cs`, `dotnet/src/DoclingDotNet/Parity/SegmentedParityComparer.cs`, `dotnet/tools/DoclingParityHarness/Program.cs`, `dotnet/tests/DoclingDotNet.Tests/DoclingPdfConversionRunnerSemanticsTests.cs`, `dotnet/tests/DoclingDotNet.Tests/TesseractOcrProviderTests.cs`, `dotnet/tests/DoclingDotNet.Tests/SegmentedParityComparerTests.cs` | Provider contracts, runtime wiring, policy gating, capability negotiation, and OCR-specific parity drift checks with threshold enforcement are executable and tested |

## Verification commands (latest run)

```powershell
dotnet build dotnet/DoclingDotNet.slnx --configuration Release
dotnet test dotnet/DoclingDotNet.slnx --configuration Release
dotnet build dotnet/DoclingDotNet.Examples.slnx --configuration Release
powershell -ExecutionPolicy Bypass -File .\scripts\test-docling-parse-cabi-smoke.ps1 -SkipConfigure
powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 -SkipConfigure -Output .artifacts/parity/docling-parse-parity-report.json -MaxOcrDrift 0 -TextMismatchSeverity Minor -GeometryMismatchSeverity Minor -ContextMismatchSeverity Minor --max-pages 20
powershell -ExecutionPolicy Bypass -File .\scripts\apply-docling-parse-upstream-delta.ps1 -CheckOnly
powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1 -TargetRef HEAD -SkipFetch -SkipValidation
```

## Important boundary

Current evidence does **not** prove full "works as Python library would work" across all behavior layers.

What is proven:
- schema contract alignment for segmented JSON DTOs
- native segmented runtime endpoint availability
- smoke-level parity check on one sample document
- corpus-run parity report generation with severity and threshold gates
- semantic parity depth signals for geometry/font/confidence/reading-order plus text field-level semantics + text semantic sequence signatures and non-text context signatures/field-level distributions (`bitmap_resources`, `widgets`, `hyperlinks`, `shapes`) with configurable strictness
- one-command upstream upgrade orchestration with baseline commit/hash tracking

What is still unproven:
- full-corpus, deep field-by-field parity coverage across all document categories
- full upstream-equivalent pipeline integration breadth and telemetry richness
- plugin/fallback behavior parity and non-PDF backend parity
- multi-platform parity harness CI *blocking* gate coverage beyond Windows/Linux baseline

Next:
- expand deeper text-value/sequence parity beyond current signature/distribution checks and continue pipeline/plugin parity layers.

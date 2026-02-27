# Validation and Commands

See also:
- [Parity_Mechanism.md](Parity_Mechanism.md)

## Core validation commands
```powershell
dotnet build dotnet/DoclingDotNet.Examples.slnx
powershell -ExecutionPolicy Bypass -File .\scripts\test-docling-parse-cabi-smoke.ps1 -SkipConfigure
powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 -SkipConfigure -Output .artifacts/parity/docling-parse-parity-report.json -MaxOcrDrift 0 -TextMismatchSeverity Minor -GeometryMismatchSeverity Minor -ContextMismatchSeverity Minor --max-pages 20
```

## .NET workspace validation
```powershell
dotnet build dotnet/DoclingDotNet.slnx
dotnet test dotnet/DoclingDotNet.slnx --configuration Release
```

## Smoke run (without assertions)
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parse-cabi-smoke.ps1
```

## Parity harness run
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 -SkipConfigure -Output .artifacts/parity/docling-parse-parity-report.json -GeometryMismatchSeverity Minor

# enforce OCR parity drift gate
powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 -SkipConfigure -Output .artifacts/parity/docling-parse-parity-report.json -MaxOcrDrift 0 -TextMismatchSeverity Minor -GeometryMismatchSeverity Minor -ContextMismatchSeverity Minor

# strict telemetry lane example (promote text/geometry/context drift to major)
powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 -SkipConfigure -Output .artifacts/parity/docling-parse-parity-report.json -MaxOcrDrift 0 -TextMismatchSeverity Major -GeometryMismatchSeverity Major -ContextMismatchSeverity Major

# strict non-text context lane example (promote context drift to major)
powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 -SkipConfigure -Output .artifacts/parity/docling-parse-parity-report.json -MaxOcrDrift 0 -TextMismatchSeverity Major -GeometryMismatchSeverity Minor -ContextMismatchSeverity Major
```

Current expected behavior:
- `-GeometryMismatchSeverity Major` currently reports geometry drift and fails threshold (`major > 0`), which is intentional for strict-lane visibility.
- `-TextMismatchSeverity` defaults to `Minor` in the runner script so baseline parity gates focus on core ported behavior while richer text semantic checks remain available for strict telemetry lanes.
- Non-text context checks (`bitmap_resources`, `widgets`, `hyperlinks`, `shapes`) include both signature and field-level semantic distribution drift and run in baseline mode with default `Minor` severity to preserve blocking gate stability while retaining drift visibility.

## Cleanup
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\clean-workspace.ps1
```

## Artifact packaging
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-docling-parse-cabi-artifacts.ps1 -Configuration Release -OutputDir .artifacts/slice0-win-x64
```

## Upstream delta patch workflow
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\export-docling-parse-upstream-delta.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\apply-docling-parse-upstream-delta.ps1 -CheckOnly
powershell -ExecutionPolicy Bypass -File .\scripts\update-docling-parse-upstream-baseline.ps1
```

## Docling upstream delta analysis workflow
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\update-docling-upstream-baseline.ps1 -PortedRef HEAD -TrackedRef origin/main
powershell -ExecutionPolicy Bypass -File .\scripts\report-docling-upstream-delta.ps1
```
- Produces LLM-ready artifacts under `.artifacts/upstream-delta/docling/`:
  - commit/file summary JSON
  - markdown report
  - patch for baseline..target range
  - prompt template for porting work

## One-command upstream upgrade
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1
```
- Safe preflight only:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1 -DryRun`
- Optional local/offline preflight:
  - `-SkipFetch`
- Optional fast path:
  - `-SkipValidation`

## CI workflow
- GitHub Actions workflow:
  - `.github/workflows/slice0-ci.yml`
- It performs:
  - .NET build (`dotnet/`)
  - Slice 0 smoke assertions
  - FS-001 baseline parity harness run (`--max-pages 20`)
  - OCR parity drift threshold gate (`-MaxOcrDrift 0`)
  - baseline parity gate configured for ported-code stability (`-TextMismatchSeverity Minor`, `-GeometryMismatchSeverity Minor`, `-ContextMismatchSeverity Minor`)
  - geometry mismatch severity explicitly configured (`-GeometryMismatchSeverity Minor`)
  - strict parity telemetry lane in non-blocking mode (`-TextMismatchSeverity Major`, `-GeometryMismatchSeverity Major`, `-ContextMismatchSeverity Major`, `continue-on-error: true`)
  - Linux baseline parity lane (`ubuntu-latest`, blocking gate with `Text/Geometry/Context=Minor`)
  - Linux strict parity lane (`ubuntu-latest`, non-blocking telemetry with `Text/Geometry/Context=Major`)
  - C ABI artifact packaging and upload (`slice0-win-x64-cabi`)
  - parity report uploads:
    - `parity-report-win-x64` (baseline + strict-lane reports)
    - `parity-report-linux` (baseline gate + strict telemetry lane)

## Notes
- The smoke scripts handle native stderr logs robustly and fail by non-zero exit code.
- Parser JSON payload sizes can vary slightly; tests assert success markers, not exact payload length.
- Root git intentionally ignores `upstream/**`; use the upstream delta patch workflow to keep native changes reproducible.
- Run smoke and parity scripts sequentially (both use `upstream/deps/docling-parse/build-cabi` and can file-lock if run concurrently).

## Evidence location
- Detailed execution history is in `docs/operations/progress.md`.

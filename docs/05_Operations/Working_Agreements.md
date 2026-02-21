# Working Agreements

## Core operating mode
- Execute bottom-up (P0 -> P5).
- Deliver vertical slices with runnable validation.
- Avoid parity claims without output comparison evidence.
- Treat `docs/03_Execution/Parity_Mechanism.md` as the parity source-of-truth before modifying parity lanes.

## Process controls
- Keep progress log current: `docs/05_Operations/Progress/progress.md`.
- Keep docs lean by default:
  - every iteration: concise summary in `docs/05_Operations/Progress/progress.md`
  - update `docs/` + `docs/CHANGELOG.md` only for milestone or contract/workflow changes
- When changing ignored upstream code, export/update tracked patch artifacts under `patches/`.
- For upstream upgrades, use one-command workflow:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1`
- For upstream Docling change detection since last port baseline:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\report-docling-upstream-delta.ps1`

## Status and planning sources
- Done-state source:
  - `docs/06_Backlog/Future_Stories.md` + validation evidence in `docs/05_Operations/Progress/progress.md`.
- Next-work source:
  - priority ladder + lowest incomplete story in `docs/06_Backlog/Future_Stories.md`.
- Audit source:
  - `docs/CHANGELOG.md` and git history.

## Local skills
- `.agent/skills/debug-loop-breaker`
- `.agent/skills/native-process-wrapper`
- `.agent/skills/hung-run-recovery`
- `.agent/skills/vertical-slice-gate`
- `.agent/skills/docs-vault-maintainer`
- `.agent/skills/lean-iteration-logging`

## Required minimum checks
```powershell
dotnet build dotnet/DoclingDotNet.slnx --configuration Release
dotnet test dotnet/DoclingDotNet.slnx --configuration Release --no-build
dotnet build dotnet/DoclingDotNet.Examples.slnx
powershell -ExecutionPolicy Bypass -File .\scripts\test-docling-parse-cabi-smoke.ps1 -SkipConfigure
powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 -SkipConfigure -Output .artifacts/parity/docling-parse-parity-report.json -MaxOcrDrift 0 --max-pages 20
powershell -ExecutionPolicy Bypass -File .\scripts\apply-docling-parse-upstream-delta.ps1 -CheckOnly
```

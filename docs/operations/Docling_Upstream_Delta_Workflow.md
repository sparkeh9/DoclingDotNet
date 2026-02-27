# Docling Upstream Delta Workflow

Goal: identify exactly what changed in upstream Python Docling since the last ported baseline, and generate an LLM-ready input package.

## Baseline model
- Baseline metadata file:
  - `patches/docling/upstream-baseline.json`
- Baseline captures:
  - last ported upstream commit (`baseline_ported_commit`)
  - tracked ref/commit (usually `origin/main`)
  - behavioral equivalence status + scope + evidence artifact paths

Important:
- Commit hash alone does not prove equivalence.
- Equivalence is explicit and must be backed by artifacts.

## Commands
1. Update/record the last ported baseline:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\update-docling-upstream-baseline.ps1 -PortedRef HEAD -TrackedRef origin/main
```

2. Generate upstream delta report + patch + LLM prompt:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\report-docling-upstream-delta.ps1
```

## Output artifacts
- Output directory:
  - `.artifacts/upstream-delta/docling/`
- Generated files:
  - `docling-upstream-delta-<from>-to-<to>.patch`
  - `docling-upstream-delta-<from>-to-<to>.json`
  - `docling-upstream-delta-<from>-to-<to>.md`
  - `docling-upstream-delta-<from>-to-<to>-llm-prompt.md`

## Recommended usage
1. Run report generation.
2. Provide generated JSON + patch + prompt markdown to LLM.
3. Implement and validate ported changes.
4. Update baseline only after passing validations and parity evidence collection.

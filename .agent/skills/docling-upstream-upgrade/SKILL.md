---
name: docling-upstream-upgrade
description: "Run the one-command Docling upstream upgrade workflow: fetch latest upstream, port local delta, validate, and record baseline metadata."
---

# Docling Upstream Upgrade

## Trigger phrases
- "fetch latest and port changes over"
- "upgrade docling upstream"
- "port latest docling-parse changes"

## Purpose
Make upstream upgrades reproducible and auditable for ignored upstream sources.

## Primary command
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1
```

## What this skill enforces
1. Refresh the tracked upstream delta patch.
2. Fetch latest upstream refs (unless intentionally skipped).
3. Reapply local port delta onto target upstream commit.
4. Run validation gates (tests, smoke, parity, patch check).
5. Update baseline metadata (`patches/docling-parse/upstream-baseline.json`).
6. Log outcomes in `docs/05_Operations/Progress/progress.md`.
7. Update docs/changelog when workflow/script behavior changes.

## Safe debug modes
- Preflight only, no workspace mutation:
  - `-DryRun`
- Offline/local preflight:
  - `-SkipFetch`
- Faster command-path verification:
  - `-SkipValidation`
- Keep temp workspace on failure:
  - `-KeepTempOnFailure`

## Required evidence after run
- Validation command outcomes recorded in `docs/05_Operations/Progress/progress.md`.
- Baseline metadata file updated.
- `docs/CHANGELOG.md` updated for workflow/script changes.

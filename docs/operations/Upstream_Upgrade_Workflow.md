# Upstream Upgrade Workflow

Goal: allow one command to execute a reproducible Docling upstream upgrade + port carry-over flow.

Primary command:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1
```

This is the operational meaning of: **"fetch latest and port changes over"**.

## What the command does
1. Exports current local upstream delta patch:
   - `patches/docling-parse/0001-docling-parse-cabi-foundation-and-segmented-runtime.patch`
2. Fetches latest upstream refs (`origin`) unless `-SkipFetch` is used.
3. Resolves target ref (default `origin/main`).
4. Preflights patch apply on an isolated temp clone.
5. Resets local `upstream/deps/docling-parse` to target commit and reapplies port patch.
6. Updates baseline metadata:
   - `patches/docling-parse/upstream-baseline.json`
7. Runs validation gates (unless `-SkipValidation`):
   - `dotnet test dotnet/DoclingDotNet.slnx --configuration Release`
   - `dotnet build dotnet/DoclingDotNet.Examples.slnx --configuration Release`
   - smoke + parity + patch check scripts

## Baseline and divergence tracking
Baseline metadata is stored in:
- `patches/docling-parse/upstream-baseline.json`

Key fields:
- `upstream_head_commit`
- `tracked_ref` / `tracked_ref_commit`
- `patch_sha256`
- `root_repo_commit`

To inspect current divergence from baseline manually:

```powershell
$meta = Get-Content .\patches\docling-parse\upstream-baseline.json | ConvertFrom-Json
git -C .\upstream\deps\docling-parse fetch origin
git -C .\upstream\deps\docling-parse rev-list --left-right --count "$($meta.tracked_ref_commit)...origin/main"
```

## Common options
- Use a specific target ref:
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1 -TargetRef origin/main
  ```
- Skip fetch (offline/local test):
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1 -SkipFetch
  ```
- Dry-run preflight only (no in-place update):
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1 -DryRun
  ```
- Skip validation (faster, not recommended for production upgrades):
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1 -SkipValidation
  ```
- Continue even if validation fails:
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1 -ContinueOnValidationFailure
  ```
- Allow empty exported patch (normally blocked as safety guard):
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\fetch-latest-and-port-docling-parse.ps1 -AllowEmptyPatch
  ```

## Failure behavior and safety
- Patch apply is validated in a temp clone before in-place workspace update.
- Empty patch exports fail fast by default to avoid accidental wipe of local upstream delta (`-AllowEmptyPatch` overrides this).
- `-DryRun` performs fetch/ref resolution + patch preflight only and does not modify workspace upstream state.
- If in-place apply fails, script attempts rollback to original commit and patch.
- Use `-KeepTempOnFailure` to retain temp workspace for debugging.
- Use `-KeepBackup` to keep a backup patch snapshot before update.

## Required follow-up after successful upgrade
1. Review output of validation commands.
2. Confirm baseline metadata changed as expected.
3. Commit tracked repository changes (scripts/docs/metadata/changelog/progress).
4. Do not claim parity without parity report evidence.

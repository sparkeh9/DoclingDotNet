---
name: native-process-wrapper
description: "Prevent PowerShell/native command false failures by capturing stdout/stderr explicitly and gating on exit code."
---

# Native Process Wrapper

## When to use
- Running `cmake`, `dotnet`, or other native tools from PowerShell.
- Native stderr logs are being treated as hard failures.
- Script reliability differs across shells/hosts.

## Protocol
1. Avoid direct `& tool ...` when reliability is critical.
2. Use `Start-Process` with:
   - redirected stdout file
   - redirected stderr file
   - explicit exit code check
3. Print both streams for diagnostics.
4. Fail only on non-zero exit code (unless policy says otherwise).

## Minimum wrapper requirements
- Command name and args logged.
- Output stream capture.
- Deterministic exit code handling.
- Cleanup of temp files.

## Repository reference
- Canonical implementation:
  - `scripts/run-docling-parse-cabi-smoke.ps1`

## Exit criteria
- Script behavior is consistent across PowerShell hosts.
- No false-negative failures from expected stderr logging.

---
name: hung-run-recovery
description: "Recover from hung command sessions using targeted process discovery/termination and add anti-hang guards."
---

# Hung Run Recovery

## When to use
- Command shows no progress for an abnormal duration.
- Session polling continues without output.
- Orphaned child processes remain after script exit.

## Protocol
1. Confirm hang signal:
   - no useful output over a defined window, and
   - no expected CPU/disk progress.
2. Locate specific process by command line (not broad kill).
3. Terminate only matching processes.
4. Re-run with safeguards:
   - timeout budget,
   - output heartbeat,
   - smaller scope where possible.

## Safety rules
- Never blanket-kill all `dotnet`/`pwsh` processes.
- Always match by command line intent.
- Log what was terminated and why.

## Repository reference
- Used during post-clean smoke test recovery in `docs/operations/progress.md`.

## Exit criteria
- Hung process is cleared.
- Re-run succeeds or fails fast with actionable error.

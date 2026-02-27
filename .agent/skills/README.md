# Local Skills (`.agent/skills`)

Purpose: repository-specific process skills for reducing execution inefficiencies.

## Skills
- `debug-loop-breaker`: Stop repeated trial/error loops and force hypothesis-driven debugging.
- `native-process-wrapper`: Run native commands safely in PowerShell without stderr-induced false failures.
- `hung-run-recovery`: Recover quickly from stuck command sessions and prevent repeat hangs.
- `vertical-slice-gate`: Enforce bottom-up slice completion checks before moving upward.
- `docs-vault-maintainer`: Keep docs lean; update root `docs/`/`CHANGELOG` only for milestone or contract/workflow changes.
- `lean-iteration-logging`: Enforce concise per-iteration `Changed / Validation / Next` summaries in `docs/operations/progress.md`.
- `docling-upstream-upgrade`: Execute one-command upstream upgrade workflow ("fetch latest and port changes over") with baseline metadata and validations.

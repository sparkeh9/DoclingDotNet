# Documentation Workflow

## Default mode (lean)
For normal iterations, update only `docs/05_Operations/Progress/progress.md` with one concise entry:
- `Changed`
- `Validation`
- `Next`

## Escalation triggers
Update `docs/` pages and `docs/CHANGELOG.md` only when the iteration changes:
- public API/contract behavior
- CI/gating/workflow behavior
- upstream upgrade process behavior
- operating policy/process rules

## Required actions by level
1. Every iteration:
   - append concise summary to `docs/05_Operations/Progress/progress.md`.
2. Escalated changes only:
   - update targeted doc page(s) in `docs/`
   - add/update `docs/CHANGELOG.md`
   - update `AGENTS.md` if policy changed.

## Ownership
- Agents making code/process changes are responsible for logging in the same turn.
- Lean behavior is enforced by:
  - `.agent/skills/docs-vault-maintainer/SKILL.md`
  - `.agent/skills/lean-iteration-logging/SKILL.md`

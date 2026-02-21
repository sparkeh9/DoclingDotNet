---
name: lean-iteration-logging
description: "Enforce concise iteration logging: summary entry in docs/05_Operations/Progress/progress.md, minimal docs/changelog churn."
---

# Lean Iteration Logging

## Trigger
- Any request indicating docs/logging is too verbose.
- Normal iteration closeout for this repository.

## Rule
For each iteration, update only `docs/05_Operations/Progress/progress.md` with:
1. `Changed`
2. `Validation`
3. `Next`

## Escalate docs only when needed
Update `docs/` and `docs/CHANGELOG.md` only if the iteration changes:
- public contracts/API behavior
- CI/gating/workflow behavior
- upstream-upgrade process behavior

## Anti-patterns to avoid
- Multi-file docs sweep on every small code slice.
- Rewriting context/backlog pages for routine internal refactors.
- Duplicating detailed implementation notes across multiple docs.

## Completion gate
- Iteration has one concise entry in `docs/05_Operations/Progress/progress.md`.
- Additional docs/changelog updates were made only if escalation criteria were met.

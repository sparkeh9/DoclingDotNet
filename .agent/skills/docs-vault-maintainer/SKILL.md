---
name: docs-vault-maintainer
description: "Keep docs lean: append concise iteration summaries, and only update docs/changelog for milestone or contract/workflow changes."
---

# Docs Vault Maintainer

## Purpose
Preserve useful documentation without high-churn doc overhead.

## When to use
- Every implementation iteration (for concise progress entry).
- Milestone-level behavior/process/contract changes.
- CI/workflow or upgrade-process changes.

## Required actions
1. Always append one concise entry to `docs/05_Operations/Progress/progress.md`:
   - `Changed`
   - `Validation`
   - `Next`
2. Update `docs/CHANGELOG.md` only for milestone/significant changes.
3. Update `docs/` pages only when one of these changes:
   - public API/contract behavior
   - CI/gating/workflow behavior
   - upstream-upgrade process
4. If process policy changes, update `AGENTS.md` and this skill.

## Default behavior
- Prefer **no broad docs sweep** per small slice.
- Avoid touching multiple docs files for routine internal implementation increments.
- Keep iteration-level detail in `docs/05_Operations/Progress/progress.md` only.

## Completion gate
- Iteration is documented when concise `docs/05_Operations/Progress/progress.md` entry exists.
- Milestone/contract/workflow changes also require:
  - `docs/CHANGELOG.md` update
  - targeted doc page update(s) only where needed.

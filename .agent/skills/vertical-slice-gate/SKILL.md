---
name: vertical-slice-gate
description: "Enforce bottom-up vertical-slice completion checks before advancing to higher-layer features."
---

# Vertical Slice Gate

## When to use
- Starting or closing any slice (`P0`, `P1`, ...).
- Temptation to move upward before lower-level reliability is proven.

## Protocol
1. Declare slice boundary and acceptance criteria.
2. Implement only minimal cross-layer scope for that slice.
3. Run mandatory checks:
   - build/regression command(s)
   - slice smoke test(s)
4. Log evidence in `docs/05_Operations/Progress/progress.md`.
5. Only then move to next layer.

## Required checks in this repo (minimum)
- `dotnet build dotnet/DoclingDotNet.Examples.slnx`
- `powershell -ExecutionPolicy Bypass -File .\scripts\test-docling-parse-cabi-smoke.ps1 -SkipConfigure`

## Neighbor-feature rule
When touching low-level code, also complete nearby low-level improvements that reduce future churn:
- script robustness
- deterministic error handling
- reproducible clean/test workflow

## Exit criteria
- Slice checks pass.
- Progress log contains concise `Changed / Validation / Next` outcomes.
- Additional docs updates are required only for milestone or contract/workflow changes.

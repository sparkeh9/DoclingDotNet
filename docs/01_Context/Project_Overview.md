# Project Overview

## Goal
Port Docling capabilities to a .NET-first implementation without losing extraction quality or operational reliability.

## Repository layout
- `upstream/docling/`: upstream Docling Python codebase snapshot (reference implementation)
- `upstream/deps/`: upstream dependency snapshots, including `docling-parse` native parser
- `dotnet/`: .NET library workspace for port implementation code and examples
- `scripts/`: automation scripts for validation and porting
- `patches/`: upstream drift validation metadata and patches
- `docs/`: this vault

## Baseline conclusion
- No intrinsic blocker to .NET porting.
- Main challenge is semantic parity, not raw interop plumbing.

## Primary repository scenarios
1. Build and harden a .NET replacement runtime for Docling parse/pipeline behavior.
2. Validate behavior equivalence against upstream outputs using parity reports.
3. Port upstream changes safely using baseline/hash + delta scripts and parity gates.
4. Optimize .NET implementation performance while preserving output behavior.
5. Produce deterministic artifacts/evidence for CI and release hardening.

## Key references
- `docling_dotnet_port_report.md`
- `docs/04_Assessment/Native_Assessment.md`
- `docs/05_Operations/Progress/progress.md`
- `docs/03_Execution/Parity_Mechanism.md`

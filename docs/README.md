# DoclingDotNet Docs

Technical reference for DoclingDotNet — a .NET port of [Docling](https://github.com/DS4SD/docling).

## Start here

- [USAGE.md](USAGE.md) — integration guide for library consumers
- [CHANGELOG.md](CHANGELOG.md) — release history

## Repository layout

| Path | Purpose |
|------|---------|
| `upstream/docling/` | Upstream Python Docling snapshot (reference, git-ignored) |
| `upstream/deps/` | Upstream dependency snapshots incl. `docling-parse` native parser |
| `dotnet/` | .NET library, tests, tools, and examples |
| `scripts/` | Validation, upgrade, and packaging automation |
| `patches/` | Upstream delta patches and baseline metadata |
| `docs/` | This reference vault |

## Architecture

- [architecture/Strangler_Fig_Porting.md](architecture/Strangler_Fig_Porting.md) — design philosophy and port approach
- [architecture/C_ABI_Bridge.md](architecture/C_ABI_Bridge.md) — `docling_parse_c` C ABI contract, build, and validation
- [architecture/Concurrency_Constraints.md](architecture/Concurrency_Constraints.md) — thread-safety boundaries for `DoclingParseSession`

## Operations

- [operations/Parity_Mechanism.md](operations/Parity_Mechanism.md) — what parity is, what is compared, when to run it
- [operations/Validation_and_Commands.md](operations/Validation_and_Commands.md) — all validation commands
- [operations/Upstream_Upgrade_Workflow.md](operations/Upstream_Upgrade_Workflow.md) — one-command upstream upgrade
- [operations/Docling_Upstream_Delta_Workflow.md](operations/Docling_Upstream_Delta_Workflow.md) — Python Docling drift analysis
- [operations/progress.md](operations/progress.md) — living iteration log

## Backlog and deviations

- [Future_Stories.md](Future_Stories.md) — authoritative story backlog and status
- [Upstream_Deviations_Registry.md](Upstream_Deviations_Registry.md) — documented behavioral differences vs upstream Python

## Archived

Historical porting-era snapshots: [archive/](archive/)

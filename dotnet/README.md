# .NET Workspace

This directory contains .NET-owned implementation code for the Docling port.

## Layout
- `DoclingDotNet.slnx`: solution root for .NET code.
- `src/DoclingDotNet`: initial library project.
- `tests/DoclingDotNet.Tests`: contract and parity unit tests.
- `tools/DoclingParityHarness`: executable corpus parity harness.

## Scope boundary
- All upstream Python/native cloned source lives under `../upstream/`.
- .NET code in this folder should not depend on editing upstream repositories directly for day-to-day development.

## Current status
- Initial library scaffold created.
- ABI helper surface included for `docling_parse_c` compatibility checks.
- Segmented parity comparer and harness are implemented for machine-readable drift reports.

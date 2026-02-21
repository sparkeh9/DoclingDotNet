# docling upstream baseline metadata

This folder tracks baseline metadata for the ignored upstream Python Docling clone (`upstream/docling`).

Primary file:
- `upstream-baseline.json`
  - `baseline_ported_commit`: last upstream commit considered ported
  - `tracked_ref` / `tracked_ref_commit`: reference target used for drift checks
  - `behavioral_equivalence_status`: `not_verified`, `partial`, or `verified`
  - `behavioral_equivalence_scope`: textual scope boundary for claimed equivalence
  - `behavioral_evidence_artifacts`: paths to supporting parity/verification artifacts

Populate/update with:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\update-docling-upstream-baseline.ps1
```

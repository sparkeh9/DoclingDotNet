# Parity Deviation (Deviance) Remediation Plan

Date: 2026-02-21  
Branch: `chore/parity-baseline-gate-and-deviation-plan`  
Evidence reports:
- Baseline pass: `.artifacts/parity/verify-postfix-baseline.json`
- Strict fail: `.artifacts/parity/verify-postfix-strict.json`

## Intent
- Keep the blocking parity gate focused on port-core correctness.
- Drive strict parity drift (`Major`) to zero in prioritized slices.
- Avoid masking real port regressions while we iterate on higher-layer behavior.

## Current Strict Drift Inventory

Total strict drift on 20-page slice:
- `Major`: 88
- `Critical`: 0
- `OCR drift`: 0

Itemized mismatch codes (from strict report):

| Priority | Mismatch code | Count | Primary documents impacted |
|---|---|---:|---|
| P1 | `text_color_distribution_mismatch` | 21 | `ligatures_01.pdf`, `cropbox_versus_mediabox_01.pdf`, `cropbox_versus_mediabox_02.pdf`, `broken_media_box_v01.pdf`, `font_02.pdf`, `font_03.pdf` |
| P1 | `text_semantic_sequence_mismatch` | 21 | `ligatures_01.pdf`, `cropbox_versus_mediabox_01.pdf`, `cropbox_versus_mediabox_02.pdf`, `broken_media_box_v01.pdf`, `font_02.pdf`, `font_03.pdf` |
| P1 | `text_index_value_map_mismatch` | 21 | `ligatures_01.pdf`, `cropbox_versus_mediabox_01.pdf`, `cropbox_versus_mediabox_02.pdf`, `broken_media_box_v01.pdf`, `font_02.pdf`, `font_03.pdf` |
| P2 | `bitmap_mimetype_distribution_mismatch` | 8 | `broken_media_box_v01.pdf`, `cropbox_versus_mediabox_02.pdf`, `font_02.pdf`, `font_03.pdf`, `form_fields.pdf`, `ligatures_01.pdf` |
| P2 | `bitmap_resource_signature_mismatch` | 8 | `broken_media_box_v01.pdf`, `cropbox_versus_mediabox_02.pdf`, `font_02.pdf`, `font_03.pdf`, `form_fields.pdf`, `ligatures_01.pdf` |
| P3 | `geometry_signature_mismatch` | 6 | `cropbox_versus_mediabox_01.pdf`, `cropbox_versus_mediabox_02.pdf`, `deep-mediabox-inheritance.pdf` |
| P3 | `shape_signature_mismatch` | 1 | `cropbox_versus_mediabox_01.pdf` |
| P3 | `bitmap_dpi_distribution_mismatch` | 1 | `ligatures_01.pdf` |
| P3 | `bitmap_mode_distribution_mismatch` | 1 | `ligatures_01.pdf` |

Most impacted PDFs:
- `ligatures_01.pdf` (22)
- `cropbox_versus_mediabox_02.pdf` (18)
- `cropbox_versus_mediabox_01.pdf` (12)
- `broken_media_box_v01.pdf` (11)
- `font_02.pdf` (11)
- `font_03.pdf` (11)

## Remediation Work Packages

## WP1 - Text semantic parity hardening (P1)
Scope:
- `char_cells`, `word_cells`, `textline_cells`
- Sequence hash, index-value map, and color distribution drift

Actions:
1. Audit canonicalization path for text ordering/index assignment consistency.
2. Normalize text color extraction representation to match upstream conventions.
3. Add focused parity regression tests for each text mismatch code with fixture-level assertions.
4. Re-run strict parity on high-impact set first (`ligatures_*`, `cropbox_*`, `font_*`).

Acceptance:
- `text_*` strict `Major` count reduced from 63 to 0 on `--max-pages 20`.

## WP2 - Bitmap/context parity normalization (P2)
Scope:
- Bitmap mimetype, signature, DPI/mode metadata handling

Actions:
1. Compare upstream-vs-.NET resource metadata derivation and serialization.
2. Resolve mimetype normalization discrepancy (`application/octet-stream` vs expected image type).
3. Stabilize bitmap signature input fields and ordering.
4. Add regression tests for representative docs (`form_fields.pdf`, `broken_media_box_v01.pdf`, `ligatures_01.pdf`).

Acceptance:
- `bitmap_*` strict `Major` count reduced from 18 to 0 on `--max-pages 20`.

## WP3 - Geometry/cropbox/mediabox and shape parity (P3)
Scope:
- Geometry signatures for char/word/textline cells
- Shape signature parity

Actions:
1. Review coordinate normalization/rounding path where cropbox/mediabox inheritance applies.
2. Align geometry signature input precision and transform ordering.
3. Add fixture-specific tests for cropbox/media edge cases.

Acceptance:
- `geometry_signature_mismatch` and `shape_signature_mismatch` strict `Major` count reduced from 7 to 0 on `--max-pages 20`.

## Execution order
1. WP1
2. WP2
3. WP3

Rationale:
- Text drift accounts for the majority of strict failures and has the highest fidelity impact.
- Bitmap/context drift is the next largest contributor and likely normalization-driven.
- Geometry/shape edge cases should be resolved after text/bitmap behavior is stabilized.

## Verification Protocol Per Work Package
1. `dotnet test dotnet/DoclingDotNet.slnx --configuration Release`
2. `dotnet build dotnet/DoclingDotNet.Examples.slnx --configuration Release`
3. `powershell -ExecutionPolicy Bypass -File .\scripts\test-docling-parse-cabi-smoke.ps1 -SkipConfigure`
4. Baseline parity gate:
   - `powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 -SkipConfigure -Output .artifacts/parity/wp-baseline.json -MaxOcrDrift 0 --max-pages 20`
5. Strict parity telemetry:
   - `powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 -SkipConfigure -Output .artifacts/parity/wp-strict.json -MaxOcrDrift 0 -TextMismatchSeverity Major -GeometryMismatchSeverity Major -ContextMismatchSeverity Major --max-pages 20`

## Done Criteria
- Baseline parity remains passing (`critical=0`, `major=0`, `ocr_drift=0`).
- Strict parity reaches `critical=0`, `major=0` on the same corpus slice.
- Progress evidence for each slice is logged in `docs/05_Operations/Progress/progress.md`.

# Complexity Hotspots

These areas are expected to dominate parity effort.

## Pipeline semantics
- `upstream/docling/docling/pipeline/standard_pdf_pipeline.py`
- Concerns:
  - queueing/backpressure
  - run-isolation semantics
  - timeout and failure propagation

## Backend semantic breadth
- Large modules observed:
  - `upstream/docling/docling/backend/msword_backend.py`
  - `upstream/docling/docling/backend/html_backend.py`
  - `upstream/docling/docling/backend/latex_backend.py`
- Implication:
  - high edge-case density
  - broad output-shape responsibility

## Schema/validation semantics
- Pydantic-style defaults/coercion/serialization behavior in upstream models.
- Requires explicit C# parity contracts and regression tests.

## Reading order/layout logic
- Spatial/postprocessing algorithms influence output quality heavily.
- Needs focused parity harness coverage, not just API-level tests.

References:
- `docling_dotnet_port_report.md`
- `docs/04_Assessment/Native_Assessment.md`

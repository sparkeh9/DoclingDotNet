# Port Strategy

## Strategy principle
Build bottom-up, verify each layer, then move up.

## Priority ladder
- `P0`: native interop foundation
- `P1`: parser output -> canonical .NET DTO
- `P2`: pipeline orchestration semantics
- `P3`: OCR/model plugin substrate and fallback chain
- `P4`: reading-order/layout postprocessing parity
- `P5`: non-PDF backend semantic parity

## Vertical slices
- Slice 0: native parser runtime and smoke/test reliability
- Slice 1: parse-to-DTO deterministic output path
- Slice 2: pipeline run semantics (timeout/failure/isolation)
- Slice 3: OCR plugin (Tesseract-first)
- Slice 4: ONNX layout/model integration
- Slice 5: reading order + postprocessing
- Slice 6: format backends (DOCX/PPTX/HTML/LaTeX/XML)

## Architectural position
- Keep native parser internals in C++ (`docling-parse`) and bind through stable C ABI.
- Avoid reimplementing parser internals in C# unless unavoidable.
- Focus engineering effort on parity-critical semantics and orchestration.

See:
- [[02_Architecture/C_ABI_Bridge]]
- [[03_Execution/Vertical_Slices]]

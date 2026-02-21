# Native Surfaces

This page captures the key native/runtime surfaces involved in the port.

## Confirmed integration spikes
- ONNX Runtime via `Microsoft.ML.OnnxRuntime`
- PDFium via `PDFium.Windows`/PInvoke path
- Tesseract via .NET wrapper + runtime data
- `docling-parse` native C ABI via P/Invoke

## Upstream/native dependency map (high level)
- PDF parsing/rendering: PDFium and `docling-parse` C++ core
- Parser transitive deps: qpdf, libjpeg-turbo, zlib and support libs
- OCR/model runtimes: ONNX Runtime, Tesseract, optional OpenCV class
- Python-ecosystem model paths in upstream:
  - PyTorch/torchvision/transformers
  - RapidOCR backend stack variants
  - EasyOCR

## Porting implication
- Native interop itself is feasible.
- Packaging/runtime distribution and semantic parity remain primary effort.

References:
- `docling_dotnet_port_report.md`
- `docs/04_Assessment/Native_Assessment.md`

# Upstream Deviations Registry

Known behavioral differences between **DoclingDotNet** (.NET port) and upstream **docling-parse** (Python).

> [!NOTE]
> DoclingDotNet targets the **C ABI JSON endpoint** (`docling_parse_decode_segmented_page_json`), which is the lowest-level, most complete data surface. Where upstream Python uses a higher-level typed API that loses or transforms data, this registry documents the delta and the rationale for keeping the .NET behavior.

## Registry Format

Each entry records:
- **ID** — stable short code for cross-referencing
- **Category** — text | bitmap | geometry | shape | widget | hyperlink
- **Affected fields** — which JSON fields diverge
- **.NET behavior** — what the .NET port produces (via C ABI)
- **Python behavior** — what upstream Python produces (via typed API)
- **Rationale** — why we keep the .NET behavior
- **Upstream status** — not-filed | filed (link) | accepted | fixed-in-version

---

## DEV-001: Text Cell Color Extraction

| Property | Value |
|---|---|
| **Category** | text |
| **Affected fields** | `char_cells[].rgba`, `word_cells[].rgba`, `textline_cells[].rgba` |
| **Discovered** | 2026-02-21 |
| **Upstream status** | not-filed |

### .NET Behavior (C ABI JSON)
Actual RGB values from `cell.rgb_filling_ops` in the C++ `page_item<PAGE_CELL>` struct, serialized via `to_rgba()` in `docling_parse_c_api.cpp:298`. Produces accurate per-character color data (e.g. `{r:20, g:22, b:24, a:255}`).

### Python Behavior (Typed API)
`_to_cells_from_decoder()` in `pdf_parser.py:480-496` constructs `PdfTextCell` without setting `rgba`. The Pydantic model defaults to `ColorRGBA(r=0, g=0, b=0, a=255)` (black). The pybind11 bindings do not expose `rgb_filling_ops` as a property, so the Python code cannot access it.

### Rationale
Text color is semantically important for document understanding (e.g. distinguishing headings, links, annotations). The C ABI correctly provides it; the Python typed API omits it due to a missing pybind11 binding. Defaulting to black loses information.

### Impact
21 pages across 6 PDFs (`ligatures_01`, `cropbox_versus_mediabox_01/02`, `broken_media_box_v01`, `font_02`, `font_03`) showed color distribution mismatches before ground truth was aligned to C ABI output.

---

## DEV-002: Bitmap Resource Metadata Pipeline

| Property | Value |
|---|---|
| **Category** | bitmap |
| **Affected fields** | `bitmap_resources[].image`, `bitmap_resources[].mode` |
| **Discovered** | 2026-02-21 |
| **Upstream status** | not-filed |

### .NET Behavior (C ABI JSON)
Bitmaps are serialized directly from the C++ image objects with format-based mimetype assignment (`image/jpeg`, `image/jp2`, `application/octet-stream`), base64 encoding, and DPI computation. Implemented in `to_bitmap_resources()` in `docling_parse_c_api.cpp:405-458`.

### Python Behavior (Typed API)
`_to_bitmap_resources_from_decoder()` in `pdf_parser.py:601-668` extracts raw bytes from the C++ image object, then processes them through **PIL** (Pillow): converts to `RGBA` mode, creates an `ImageRef` with `ImageRef.from_pil()`. This transforms the image data and produces different metadata (mimetype, DPI, mode values) than the raw C ABI JSON.

### Rationale
The C ABI path is deterministic and does not depend on PIL/Pillow availability. The metadata it produces is consistent across platforms. PIL conversion may alter image properties (e.g. converting `L` or `RGB` mode to `RGBA`).

### Impact
8 pages across 2 PDFs showed bitmap mimetype and signature mismatches before ground truth was aligned to C ABI output.

---

## DEV-003: Geometry Coordinate Rounding Precision

| Property | Value |
|---|---|
| **Category** | geometry |
| **Affected fields** | `char_cells[].rect`, `word_cells[].rect`, `textline_cells[].rect`, `shapes[].points` |
| **Discovered** | 2026-02-21 |
| **Upstream status** | not-filed |

### .NET Behavior (C ABI JSON)
Coordinates are rounded by the C++ `utils::values::round()` function during JSON serialization in the C ABI.

### Python Behavior (Typed API)
Python applies `_round_floats(obj, ndigits=3)` after Pydantic serialization via `save_as_json_rounded()`. The rounding may differ from C++ rounding due to the different code paths and floating-point representation.

### Rationale
Both paths intend 3-digit precision. Minor differences arise from applying rounding at different stages (C++ serialization vs Python post-processing). The C ABI rounding is applied closer to the source data.

### Impact
6 geometry and 1 shape signature mismatches before ground truth was aligned to C ABI output.

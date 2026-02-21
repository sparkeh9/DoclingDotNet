#!/usr/bin/env python3
"""Extract PDF text using upstream Python docling-parse.

Usage:
    python extract-pdf-python.py <pdf-path> <output-dir>

Writes one text file: <output-dir>/<basename>.python.md
Outputs JSON stats to stdout (one line) for the comparison script.
"""
import json
import os
import sys
import time


def main():
    if len(sys.argv) < 3:
        print("Usage: python extract-pdf-python.py <pdf-path> <output-dir>")
        sys.exit(1)

    pdf_path = sys.argv[1]
    output_dir = sys.argv[2]

    if not os.path.exists(pdf_path):
        print(f"[python] ERROR: PDF not found: {pdf_path}", file=sys.stderr)
        sys.exit(1)

    os.makedirs(output_dir, exist_ok=True)
    basename = os.path.splitext(os.path.basename(pdf_path))[0]

    print("[python] Loading docling-parse...", file=sys.stderr)
    from docling_parse.pdf_parser import DoclingPdfParser

    parser = DoclingPdfParser(loglevel="warning")

    print(f"[python] Loading PDF: {pdf_path}", file=sys.stderr)
    doc = parser.load(path_or_stream=pdf_path, lazy=False)

    page_stats = []
    color_counts = {}
    text_lines = []

    t0 = time.perf_counter()
    for page_no, page in doc.iterate_pages():
        page_data = page.export_to_dict()

        nc = len(page_data.get("char_cells", []))
        nw = len(page_data.get("word_cells", []))
        nl = len(page_data.get("textline_cells", []))

        # Extract text from textline_cells
        text_lines.append(f"--- Page {page_no} ---")
        for cell in page_data.get("textline_cells", []):
            text_lines.append(cell.get("text", "").rstrip())
        text_lines.append("")

        # Accumulate colors from char_cells
        for cell in page_data.get("char_cells", []):
            rgba = cell.get("rgba", {})
            key = f"rgba({rgba.get('r', 0)},{rgba.get('g', 0)},{rgba.get('b', 0)},{rgba.get('a', 255)})"
            color_counts[key] = color_counts.get(key, 0) + 1

        page_stats.append({"page": page_no, "chars": nc, "words": nw, "lines": nl})

    extract_time = time.perf_counter() - t0
    doc.unload()

    # Write text file
    text_file = os.path.join(output_dir, f"{basename}.python.md")
    with open(text_file, "w", encoding="utf-8") as f:
        f.write("\n".join(text_lines))

    # Output JSON stats to stdout (single line for PS to capture)
    stats = {
        "pages": len(page_stats),
        "timeMs": int(extract_time * 1000),
        "perPage": page_stats,
        "colors": color_counts,
        "textFile": text_file,
    }
    # Write stats JSON file
    stats_file = os.path.join(output_dir, f"{basename}.python.stats.json")
    with open(stats_file, "w", encoding="utf-8") as f:
        json.dump(stats, f)


if __name__ == "__main__":
    main()

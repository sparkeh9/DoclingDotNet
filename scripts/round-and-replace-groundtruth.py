#!/usr/bin/env python3
"""Apply Python-style float rounding to dumped C ABI JSON and replace ground truth.

Usage:
    python scripts/round-and-replace-groundtruth.py <dump-dir> <groundtruth-dir>
"""
import json
import os
import sys


def round_floats(obj, ndigits=3):
    """Recursively round all floats in a JSON-serializable structure.
    Matches Python upstream _round_floats function exactly."""
    if isinstance(obj, float):
        return round(obj, ndigits)
    if isinstance(obj, dict):
        return {k: round_floats(v, ndigits) for k, v in obj.items()}
    if isinstance(obj, list):
        return [round_floats(v, ndigits) for v in obj]
    return obj


def main():
    if len(sys.argv) < 3:
        print("Usage: python round-and-replace-groundtruth.py <dump-dir> <groundtruth-dir>")
        sys.exit(1)

    dump_dir = sys.argv[1]
    gt_dir = sys.argv[2]

    if not os.path.isdir(dump_dir):
        print(f"ERROR: Dump directory not found: {dump_dir}")
        sys.exit(1)

    if not os.path.isdir(gt_dir):
        print(f"ERROR: Ground truth directory not found: {gt_dir}")
        sys.exit(1)

    updated = 0
    unchanged = 0
    errors = 0

    for filename in sorted(os.listdir(dump_dir)):
        if not filename.endswith(".py.json"):
            continue

        dump_path = os.path.join(dump_dir, filename)
        gt_path = os.path.join(gt_dir, filename)

        if not os.path.exists(gt_path):
            print(f"SKIP: {filename} (no matching ground truth file)")
            continue

        try:
            with open(dump_path, "r", encoding="utf-8") as f:
                data = json.loads(f.read())

            rounded = round_floats(data, ndigits=3)
            new_content = json.dumps(rounded, indent=2)

            with open(gt_path, "r", encoding="utf-8") as f:
                old_content = f.read()

            if old_content.rstrip() == new_content.rstrip():
                unchanged += 1
            else:
                with open(gt_path, "w", encoding="utf-8") as f:
                    f.write(new_content)
                print(f"UPDATED: {filename}")
                updated += 1

        except Exception as e:
            print(f"ERROR: {filename}: {e}")
            errors += 1

    print(f"\nDone: {updated} updated, {unchanged} unchanged, {errors} errors")


if __name__ == "__main__":
    main()

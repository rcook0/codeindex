#!/usr/bin/env python3
"""Emit ingestion-friendly rows from a SymbolIndex JSON.

Supports:
- CSV (header + rows)
- JSONL (one JSON object per row)

Row schema (stable):
- identifier
- file_id
- line
- col_start
- col_end

Optional (if present in occurrence):
- byte_start
- byte_end
"""

from __future__ import annotations

import argparse
import csv
import json
import sys


def _iter_rows(symbol_index: dict):
    for sym in symbol_index.get("symbols", []):
        ident = sym["identifier"]
        for occ in sym.get("occurrences", []):
            row = {
                "identifier": ident,
                "file_id": occ["file_id"],
                "line": occ["line"],
                "col_start": occ["col_start"],
                "col_end": occ["col_end"],
            }
            if "byte_start" in occ:
                row["byte_start"] = occ["byte_start"]
            if "byte_end" in occ:
                row["byte_end"] = occ["byte_end"]
            yield row


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="inp", required=True, help="SymbolIndex JSON input")
    ap.add_argument("--format", choices=["csv", "jsonl"], required=True)
    ap.add_argument("--out", dest="out", default="-", help="Output path or '-' for stdout")
    args = ap.parse_args()

    with open(args.inp, "r", encoding="utf-8") as f:
        obj = json.load(f)

    rows = list(_iter_rows(obj))

    out_f = sys.stdout if args.out == "-" else open(args.out, "w", encoding="utf-8", newline="")
    try:
        if args.format == "jsonl":
            for r in rows:
                out_f.write(json.dumps(r, ensure_ascii=False) + "\n")
        else:
            # stable header
            fieldnames = ["identifier", "file_id", "line", "col_start", "col_end"]
            has_bytes = any(("byte_start" in r or "byte_end" in r) for r in rows)
            if has_bytes:
                fieldnames += ["byte_start", "byte_end"]
            w = csv.DictWriter(out_f, fieldnames=fieldnames)
            w.writeheader()
            for r in rows:
                w.writerow(r)
        return 0
    finally:
        if out_f is not sys.stdout:
            out_f.close()


if __name__ == "__main__":
    raise SystemExit(main())

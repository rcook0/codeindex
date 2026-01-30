#!/usr/bin/env python3
"""Patch corpus/expected/*.expected.json with bytes/lines/sha256 from corpus/inputs/*.

No external dependencies. Intended as a reproducibility tool across ecosystems.

Usage:
  python3 tools/regen_meta.py

Behavior:
- Reads each expected JSON.
- For each entry in files[], loads the corresponding inputs/<file_id>.
- Computes: bytes, lines (1-based line counting), sha256.
- Writes file in-place with updated fields.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "corpus"
INPUTS = CORPUS / "inputs"
EXPECTED = CORPUS / "expected"


def count_lines(text: str) -> int:
    if text == "":
        return 0
    # Count \n, plus one if file does not end with \n
    n = text.count("\n")
    if not text.endswith("\n"):
        n += 1
    return n


def sha256_hex(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def patch_expected(path: Path) -> None:
    obj = json.loads(path.read_text(encoding="utf-8"))

    files = obj.get("files", [])
    if not isinstance(files, list):
        raise ValueError(f"{path}: files must be a list")

    for f in files:
        file_id = f.get("file_id")
        if not file_id:
            raise ValueError(f"{path}: file entry missing file_id")

        src = (INPUTS / file_id)
        if not src.exists():
            raise FileNotFoundError(f"Missing input file: {src}")

        data = src.read_bytes()
        text = data.decode("utf-8", errors="replace")

        f["bytes"] = len(data)
        f["lines"] = count_lines(text)
        f["sha256"] = sha256_hex(data)

    path.write_text(json.dumps(obj, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def main() -> None:
    for p in sorted(EXPECTED.glob("*.expected.json")):
        patch_expected(p)
    print("regen_meta: updated expected JSON metadata")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Contract checks for the golden corpus.

This goes beyond JSON Schema validation:
  - discovers expected outputs under corpus/**/expected/*.expected.json
  - verifies identifiers are sorted (lex ordering)
  - verifies occurrences are sorted (file_id, line, col_start, col_end)
  - verifies no duplicate occurrences per symbol (exact duplicate)
  - verifies stats (occurrence_count, unique_line_count) if present
  - verifies per-file metadata (bytes, lines, sha256) when present

Exit status:
  0 on success, 1 on failure.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any, Iterable, Tuple


def _is_sorted(seq: Iterable[Any]) -> bool:
    it = iter(seq)
    try:
        prev = next(it)
    except StopIteration:
        return True
    for x in it:
        if x < prev:
            return False
        prev = x
    return True


def _file_meta(path: Path) -> tuple[int, int, str]:
    data = path.read_bytes()
    sha = hashlib.sha256(data).hexdigest()
    text = data.decode('utf-8', errors='replace')
    lines = text.count('\n')
    if len(text) > 0 and not text.endswith('\n'):
        lines += 1
    return (len(data), lines, sha)


def _find_case_root(expected_json: Path) -> Path:
    """Given .../corpus/<case>/expected/<file>.expected.json return .../corpus/<case>."""
    cur = expected_json.parent
    while cur.name != 'corpus' and cur.name != 'expected' and cur.parent != cur:
        cur = cur.parent
    # expected_json.parent is expected; its parent is case root
    if expected_json.parent.name == 'expected':
        return expected_json.parent.parent
    # Fallback: if structure differs, assume standard corpus root
    return expected_json.parents[2]


def check_symbol_index(path: Path) -> list[str]:
    errs: list[str] = []
    doc = json.loads(path.read_text(encoding='utf-8'))

    # ---- Check file metadata (if present) ----
    case_root = path.parent.parent if path.parent.name == 'expected' else _find_case_root(path)
    inputs_dir = case_root / 'inputs'

    files = doc.get('files', [])
    if isinstance(files, list) and inputs_dir.exists():
        for f in files:
            try:
                file_id = str(f['file_id'])
            except Exception:
                errs.append(f"{path}: malformed files[] entry {f!r}")
                continue
            in_path = inputs_dir / file_id
            if not in_path.exists():
                errs.append(f"{path}: input file missing: {in_path}")
                continue

            bytes_actual, lines_actual, sha_actual = _file_meta(in_path)

            # Only enforce fields that exist in expected JSON.
            if 'bytes' in f and int(f['bytes']) != bytes_actual:
                errs.append(f"{path}: {file_id}: bytes={f['bytes']} != {bytes_actual}")
            if 'lines' in f and int(f['lines']) != lines_actual:
                errs.append(f"{path}: {file_id}: lines={f['lines']} != {lines_actual}")
            if 'sha256' in f and str(f['sha256']) != sha_actual:
                errs.append(f"{path}: {file_id}: sha256 mismatch")

    # ---- Symbol ordering + invariants ----
    symbols = doc.get('symbols', [])
    idents = [s.get('identifier', '') for s in symbols]
    if not _is_sorted(idents):
        errs.append(f"{path}: symbols not sorted by identifier")

    for s in symbols:
        ident = s.get('identifier', '<missing>')
        occs = s.get('occurrences', [])

        keys: list[Tuple[str, int, int, int]] = []
        for o in occs:
            try:
                keys.append((
                    str(o['file_id']),
                    int(o['line']),
                    int(o['col_start']),
                    int(o['col_end']),
                ))
            except Exception:
                errs.append(f"{path}: {ident}: malformed occurrence {o!r}")
                continue

        if not _is_sorted(keys):
            errs.append(f"{path}: {ident}: occurrences not sorted")

        if len(keys) != len(set(keys)):
            errs.append(f"{path}: {ident}: duplicate occurrences found")

        stats = s.get('stats')
        if isinstance(stats, dict):
            oc = stats.get('occurrence_count')
            ul = stats.get('unique_line_count')

            if oc is not None and int(oc) != len(occs):
                errs.append(f"{path}: {ident}: stats.occurrence_count={oc} != {len(occs)}")

            if ul is not None:
                # Multi-file safe: unique (file_id, line) pairs.
                unique_lines = len({(str(o.get('file_id')), int(o.get('line'))) for o in occs if 'file_id' in o and 'line' in o})
                if int(ul) != unique_lines:
                    errs.append(f"{path}: {ident}: stats.unique_line_count={ul} != {unique_lines}")

    return errs


def main() -> int:
    ap = argparse.ArgumentParser(description='Run contract checks on corpus expected outputs')
    ap.add_argument('--repo-root', default=None, help='Repo root (defaults to parent of tools/)')
    args = ap.parse_args()

    repo_root = Path(args.repo_root) if args.repo_root else Path(__file__).resolve().parents[1]
    corpus_root = repo_root / 'corpus'

    if not corpus_root.exists():
        print(f"Corpus directory not found: {corpus_root}")
        return 1

    expected_files = sorted(corpus_root.glob('**/expected/*.expected.json'))
    if not expected_files:
        print(f"No expected outputs found under: {corpus_root}")
        return 1

    all_errs: list[str] = []
    for p in expected_files:
        all_errs.extend(check_symbol_index(p))

    if all_errs:
        print('Corpus contract check: FAILED')
        for e in all_errs:
            print(' -', e)
        return 1

    print(f'Corpus contract check: OK ({len(expected_files)} files)')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())

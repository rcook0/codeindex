#!/usr/bin/env python3
"""Contract checks for the golden corpus.

This goes beyond JSON Schema validation:
  - verifies identifiers are sorted (lex ordering)
  - verifies occurrences are sorted (file_id, line, col_start)
  - verifies no duplicate occurrences per symbol (exact duplicate)
  - verifies stats (occurrence_count, unique_line_count) if present

Exit status:
  0 on success, 1 on failure.
"""

from __future__ import annotations

import argparse
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


def check_symbol_index(path: Path) -> list[str]:
    errs: list[str] = []
    doc = json.loads(path.read_text(encoding='utf-8'))

    symbols = doc.get('symbols', [])
    idents = [s.get('identifier', '') for s in symbols]
    if not _is_sorted(idents):
        errs.append(f"{path}: symbols not sorted by identifier")

    for s in symbols:
        ident = s.get('identifier', '<missing>')
        occs = s.get('occurrences', [])
        # occurrences sorted by (file_id, line, col_start)
        keys: list[Tuple[str, int, int]] = []
        for o in occs:
            try:
                keys.append((str(o['file_id']), int(o['line']), int(o['col_start'])))
            except Exception:
                errs.append(f"{path}: {ident}: malformed occurrence {o!r}")
                continue

        if not _is_sorted(keys):
            errs.append(f"{path}: {ident}: occurrences not sorted")

        # no exact duplicates
        if len(keys) != len(set(keys)):
            errs.append(f"{path}: {ident}: duplicate occurrences found")

        stats = s.get('stats')
        if isinstance(stats, dict):
            oc = stats.get('occurrence_count')
            ul = stats.get('unique_line_count')
            if oc is not None and int(oc) != len(occs):
                errs.append(f"{path}: {ident}: stats.occurrence_count={oc} != {len(occs)}")
            if ul is not None:
                unique_lines = len({int(o['line']) for o in occs if 'line' in o})
                if int(ul) != unique_lines:
                    errs.append(f"{path}: {ident}: stats.unique_line_count={ul} != {unique_lines}")

    return errs


def main() -> int:
    ap = argparse.ArgumentParser(description='Run contract checks on corpus expected outputs')
    ap.add_argument('--repo-root', default=None, help='Repo root (defaults to parent of tools/)')
    args = ap.parse_args()

    repo_root = Path(args.repo_root) if args.repo_root else Path(__file__).resolve().parents[1]
    expected_dir = repo_root / 'corpus' / 'expected'

    if not expected_dir.exists():
        print(f"Expected directory not found: {expected_dir}")
        return 1

    all_errs: list[str] = []
    for p in sorted(expected_dir.glob('*.expected.json')):
        all_errs.extend(check_symbol_index(p))

    if all_errs:
        print('Corpus contract check: FAILED')
        for e in all_errs:
            print(' -', e)
        return 1

    print('Corpus contract check: OK')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())

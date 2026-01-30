#!/usr/bin/env python3
"""Contract checks for the golden corpus.

This goes beyond JSON Schema validation:
  - discovers expected outputs under corpus/**/expected/*.expected.json
  - validates ordering and invariants for SymbolIndex outputs
  - validates ordering and invariants for ProjectIndex outputs (v2.3)
  - verifies per-file metadata (bytes, lines, sha256) when present

Exit status:
  0 on success, 1 on failure.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any, Iterable, Tuple, List, Dict


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


def _case_root_from_expected(expected_json: Path) -> Path:
    """Given .../corpus/<case>/expected/<file>.expected.json return .../corpus/<case>."""
    if expected_json.parent.name == 'expected':
        return expected_json.parent.parent
    # fallback
    return expected_json.parents[2]


def _check_file_metadata(expected_path: Path, doc: Dict[str, Any], inputs_dir: Path) -> List[str]:
    errs: List[str] = []
    files = doc.get('files', [])
    if not isinstance(files, list):
        return errs

    for f in files:
        try:
            file_id = str(f['file_id'])
        except Exception:
            errs.append(f"{expected_path}: malformed files[] entry {f!r}")
            continue

        in_path = inputs_dir / file_id
        if not in_path.exists():
            errs.append(f"{expected_path}: input file missing: {in_path}")
            continue

        bytes_actual, lines_actual, sha_actual = _file_meta(in_path)

        # Only enforce fields that exist in expected JSON.
        if 'bytes' in f and int(f['bytes']) != bytes_actual:
            errs.append(f"{expected_path}: {file_id}: bytes={f['bytes']} != {bytes_actual}")
        if 'lines' in f and int(f['lines']) != lines_actual:
            errs.append(f"{expected_path}: {file_id}: lines={f['lines']} != {lines_actual}")
        if 'sha256' in f and str(f['sha256']) != sha_actual:
            errs.append(f"{expected_path}: {file_id}: sha256 mismatch")

    return errs


def check_symbol_index_doc(expected_path: Path, doc: Dict[str, Any], inputs_dir: Path) -> List[str]:
    errs: List[str] = []

    # ---- Check file metadata (if present) ----
    if inputs_dir.exists():
        errs.extend(_check_file_metadata(expected_path, doc, inputs_dir))

    # ---- Symbol ordering + invariants ----
    symbols = doc.get('symbols', [])
    if not isinstance(symbols, list):
        errs.append(f"{expected_path}: symbols is not an array")
        return errs

    idents = [s.get('identifier', '') for s in symbols]
    if not _is_sorted(idents):
        errs.append(f"{expected_path}: symbols not sorted by identifier")

    for s in symbols:
        ident = s.get('identifier', '<missing>')
        occs = s.get('occurrences', [])
        if not isinstance(occs, list):
            errs.append(f"{expected_path}: {ident}: occurrences is not an array")
            continue

        keys: List[Tuple[str, int, int, int]] = []
        for o in occs:
            try:
                keys.append((
                    str(o['file_id']),
                    int(o['line']),
                    int(o['col_start']),
                    int(o['col_end']),
                ))
            except Exception:
                errs.append(f"{expected_path}: {ident}: malformed occurrence {o!r}")
                continue

        if not _is_sorted(keys):
            errs.append(f"{expected_path}: {ident}: occurrences not sorted")

        if len(keys) != len(set(keys)):
            errs.append(f"{expected_path}: {ident}: duplicate occurrences found")

        stats = s.get('stats')
        if isinstance(stats, dict):
            oc = stats.get('occurrence_count')
            ul = stats.get('unique_line_count')

            if oc is not None and int(oc) != len(occs):
                errs.append(f"{expected_path}: {ident}: stats.occurrence_count={oc} != {len(occs)}")

            if ul is not None:
                # Multi-file safe: unique (file_id, line) pairs.
                unique_lines = len({(str(o.get('file_id')), int(o.get('line'))) for o in occs if 'file_id' in o and 'line' in o})
                if int(ul) != unique_lines:
                    errs.append(f"{expected_path}: {ident}: stats.unique_line_count={ul} != {unique_lines}")

    return errs


def check_project_index(expected_path: Path, doc: Dict[str, Any], case_root: Path) -> List[str]:
    errs: List[str] = []
    inputs_dir = case_root / 'inputs'

    indexes = doc.get('indexes', [])
    if not isinstance(indexes, list):
        return [f"{expected_path}: ProjectIndex.indexes is not an array"]

    profile_ids = [idx.get('profile_id', '') for idx in indexes]
    if not _is_sorted(profile_ids):
        errs.append(f"{expected_path}: ProjectIndex.indexes not sorted by profile_id")

    for i, idx in enumerate(indexes):
        if not isinstance(idx, dict):
            errs.append(f"{expected_path}: ProjectIndex.indexes[{i}] is not an object")
            continue
        # Reuse the SymbolIndex checks, but tag errors with embedded index number.
        sub_errs = check_symbol_index_doc(expected_path, idx, inputs_dir)
        errs.extend([e.replace(str(expected_path), f"{expected_path} (indexes[{i}])") for e in sub_errs])

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

    all_errs: List[str] = []
    for p in expected_files:
        doc = json.loads(p.read_text(encoding='utf-8'))
        case_root = _case_root_from_expected(p)
        inputs_dir = case_root / 'inputs'

        if isinstance(doc, dict) and doc.get('schema_version') == '2.3':
            all_errs.extend(check_project_index(p, doc, case_root))
        else:
            all_errs.extend(check_symbol_index_doc(p, doc, inputs_dir))

    if all_errs:
        print('Corpus contract check: FAILED')
        for e in all_errs:
            print(' -', e)
        return 1

    print(f'Corpus contract check: OK ({len(expected_files)} files)')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())

#!/usr/bin/env python3
"""Validate CodeIndex JSON artifacts against JSON Schemas.

This tool validates:
  - LanguageProfile JSON files (corpus/profiles.*.json)
  - SymbolIndex JSON files (corpus/expected/*.expected.json)

It is intended for both local development and CI.

Exit status:
  0 on success, 1 on any validation failure.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Iterable, Tuple

import jsonschema


def _load_json(path: Path):
    with path.open('r', encoding='utf-8') as f:
        return json.load(f)


def _iter_files(glob_patterns: Iterable[str], repo_root: Path) -> Iterable[Path]:
    for pat in glob_patterns:
        yield from sorted((repo_root / pat).parent.glob((repo_root / pat).name))


def _validate_one(validator: jsonschema.validators.Draft202012Validator, instance, path: Path) -> Tuple[bool, str]:
    errors = sorted(validator.iter_errors(instance), key=lambda e: list(e.path))
    if not errors:
        return True, ''
    lines = [f'Validation failed: {path}']
    for e in errors:
        loc = '/' + '/'.join(str(p) for p in e.path) if e.path else '/'
        lines.append(f'  - {loc}: {e.message}')
    return False, '\n'.join(lines)

def main() -> int:
    ap = argparse.ArgumentParser(description='Validate CodeIndex JSON artifacts against JSON Schemas.')
    ap.add_argument('--repo', type=str, default=None, help='Repository root (default: auto-detect).')
    ap.add_argument('--profiles', nargs='*', default=['corpus/profiles.*.json'], help='Glob(s) for LanguageProfile JSON files.')
    ap.add_argument('--expected', nargs='*', default=['corpus/expected/*.expected.json'], help='Glob(s) for expected SymbolIndex JSON files.')
    ap.add_argument('--schema-profile', type=str, default='schemas/language_profile.schema.json', help='Schema path for LanguageProfile.')
    ap.add_argument('--schema-index', type=str, default='schemas/symbol_index.schema.json', help='Schema path for SymbolIndex.')
    args = ap.parse_args()

    tools_dir = Path(__file__).resolve().parent
    repo_root = Path(args.repo).resolve() if args.repo else tools_dir.parent

    schema_profile = _load_json(repo_root / args.schema_profile)
    schema_index = _load_json(repo_root / args.schema_index)

    v_profile = jsonschema.Draft202012Validator(schema_profile)
    v_index = jsonschema.Draft202012Validator(schema_index)

    ok = True

    for p in _iter_files(args.profiles, repo_root):
        inst = _load_json(p)
        passed, msg = _validate_one(v_profile, inst, p)
        if not passed:
            ok = False
            print(msg)

    for p in _iter_files(args.expected, repo_root):
        inst = _load_json(p)
        passed, msg = _validate_one(v_index, inst, p)
        if not passed:
            ok = False
            print(msg)

    if ok:
        print('Schema validation: OK')
        return 0
    return 1


if __name__ == '__main__':
    raise SystemExit(main())

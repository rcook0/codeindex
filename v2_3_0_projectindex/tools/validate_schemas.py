#!/usr/bin/env python3
"""Validate CodeIndex JSON artifacts against JSON Schemas.

Validates:
  - LanguageProfile JSON files (corpus/profiles.*.json)
  - Expected SymbolIndex JSON files (corpus/**/expected/*.expected.json)
  - Expected ProjectIndex JSON files (corpus/project/expected/*.expected.json)
  - (optional) Profile Registry (profiles/registry.json)

Exit status:
  0 on success, 1 on any validation failure.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Iterable, Tuple

import jsonschema
from referencing import Registry, Resource


def _load_json(path: Path):
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


def _iter_globs(glob_patterns: Iterable[str], repo_root: Path) -> Iterable[Path]:
    out: list[Path] = []
    for pat in glob_patterns:
        out.extend(sorted(repo_root.glob(pat)))
    seen = set()
    for p in sorted(out):
        if p in seen:
            continue
        seen.add(p)
        if p.is_file():
            yield p


def _validate_one(validator: jsonschema.validators.Draft202012Validator, instance, path: Path) -> Tuple[bool, str]:
    errors = sorted(validator.iter_errors(instance), key=lambda e: list(e.path))
    if not errors:
        return True, ""
    lines = [f"Validation failed: {path}"]
    for e in errors:
        loc = "/" + "/".join(str(p) for p in e.path) if e.path else "/"
        lines.append(f"  - {loc}: {e.message}")
    return False, "\n".join(lines)


def main() -> int:
    ap = argparse.ArgumentParser(description="Validate CodeIndex JSON artifacts against JSON Schemas.")
    ap.add_argument("--repo", type=str, default=None, help="Repository root (default: auto-detect).")
    ap.add_argument("--profiles", nargs="*", default=["corpus/profiles.*.json"], help="Glob(s) for LanguageProfile JSON files.")
    ap.add_argument(
        "--expected",
        nargs="*",
        default=["corpus/**/expected/*.expected.json"],
        help="Glob(s) for expected JSON files (SymbolIndex or ProjectIndex).",
    )
    ap.add_argument("--schema-profile", type=str, default="schemas/language_profile.schema.json", help="Schema path for LanguageProfile.")
    ap.add_argument("--schema-index", type=str, default="schemas/symbol_index.schema.json", help="Schema path for SymbolIndex.")
    ap.add_argument("--schema-project", type=str, default="schemas/project_index.schema.json", help="Schema path for ProjectIndex (optional).")
    ap.add_argument("--registry", type=str, default="profiles/registry.json", help="Registry JSON path (optional).")
    ap.add_argument("--registry-schema", type=str, default="profiles/registry.schema.json", help="Registry schema path (optional).")
    args = ap.parse_args()

    tools_dir = Path(__file__).resolve().parent
    repo_root = Path(args.repo).resolve() if args.repo else tools_dir.parent

    schema_profile = _load_json(repo_root / args.schema_profile)
    schema_index = _load_json(repo_root / args.schema_index)

    # Register local schemas so Draft202012 refs resolve without network access.
    registry = Registry()
    for sch in (schema_profile, schema_index):
        sid = sch.get("$id")
        if sid:
            registry = registry.with_resource(sid, Resource.from_contents(sch))

    # Also register common relative ref keys used in this repo.
    registry = registry.with_resource("language_profile.schema.json", Resource.from_contents(schema_profile))
    registry = registry.with_resource("symbol_index.schema.json", Resource.from_contents(schema_index))

    v_profile = jsonschema.Draft202012Validator(schema_profile, registry=registry)
    v_index = jsonschema.Draft202012Validator(schema_index, registry=registry)

    schema_project_path = repo_root / args.schema_project
    v_project = None
    if schema_project_path.exists():
        schema_project = _load_json(schema_project_path)
        sid = schema_project.get("$id")
        if sid:
            registry = registry.with_resource(sid, Resource.from_contents(schema_project))
        registry = registry.with_resource("project_index.schema.json", Resource.from_contents(schema_project))
        v_project = jsonschema.Draft202012Validator(schema_project, registry=registry)

    ok = True

    # Validate profiles
    for p in _iter_globs(args.profiles, repo_root):
        inst = _load_json(p)
        passed, msg = _validate_one(v_profile, inst, p)
        if not passed:
            ok = False
            print(msg)

    # Validate expected artifacts
    for p in _iter_globs(args.expected, repo_root):
        inst = _load_json(p)
        if isinstance(inst, dict) and inst.get("schema_version") == "2.3":
            if v_project is None:
                ok = False
                print(f"ProjectIndex schema missing, cannot validate: {p}")
                continue
            passed, msg = _validate_one(v_project, inst, p)
            if not passed:
                ok = False
                print(msg)
            # Also validate embedded SymbolIndex objects
            for i, idx in enumerate(inst.get("indexes", [])):
                passed2, msg2 = _validate_one(v_index, idx, p)
                if not passed2:
                    ok = False
                    print(f"Embedded SymbolIndex[{i}] invalid in {p}\n{msg2}")
        else:
            passed, msg = _validate_one(v_index, inst, p)
            if not passed:
                ok = False
                print(msg)

    # Validate registry (top-level)
    reg_path = repo_root / args.registry
    reg_schema_path = repo_root / args.registry_schema
    if reg_path.exists() and reg_schema_path.exists():
        schema_reg = _load_json(reg_schema_path)
        v_reg = jsonschema.Draft202012Validator(schema_reg)
        inst = _load_json(reg_path)
        passed, msg = _validate_one(v_reg, inst, reg_path)
        if not passed:
            ok = False
            print(msg)

    if ok:
        print("Schema validation: OK")
        return 0
    return 1


if __name__ == "__main__":
    raise SystemExit(main())

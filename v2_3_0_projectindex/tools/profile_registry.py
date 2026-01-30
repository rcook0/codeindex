#!/usr/bin/env python3
"""CodeIndex Profile Registry

Resolves a file path to a profile alias and the profile JSON path using a
registry.json file.

Registry format is described by profiles/registry.schema.json.
"""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path

try:
    from pathspec import PathSpec
    from pathspec.patterns.gitwildmatch import GitWildMatchPattern
except Exception:
    PathSpec = None


def _load_registry(path: str) -> dict:
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def _match_glob(pattern: str, rel_posix: str) -> bool:
    """Match git-wildmatch style globs if pathspec is installed, otherwise
    fallback to Path.match semantics (less powerful for ** patterns).
    """
    if PathSpec is not None:
        spec = PathSpec.from_lines(GitWildMatchPattern, [pattern])
        return spec.match_file(rel_posix)
    # fallback
    return Path(rel_posix).match(pattern)


def resolve_profile(registry: dict, file_path: str, root: str | None = None) -> tuple[str, str]:
    profiles = registry["profiles"]
    rules = registry["rules"]

    p = Path(file_path)
    if root is not None:
        rel = Path(os.path.relpath(p, root)).as_posix()
    else:
        rel = p.as_posix()

    for rule in rules:
        pat = rule["match"]["glob"]
        if _match_glob(pat, rel):
            alias = rule["profile"]
            if alias not in profiles:
                raise ValueError(f"Registry rule refers to unknown profile alias: {alias}")
            return alias, profiles[alias]

    raise ValueError(f"No matching profile rule for file: {rel}")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--registry", default="profiles/registry.json")
    ap.add_argument("--root", default=None)
    ap.add_argument("file", help="File path to resolve")
    args = ap.parse_args()

    reg = _load_registry(args.registry)
    alias, profile_path = resolve_profile(reg, args.file, args.root)

    out = {"profile_alias": alias, "profile_path": profile_path}
    print(json.dumps(out, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

# CodeIndex Roadmap — v2.3

v2.3 introduces a project-level container so mixed-language repositories can be represented as a single canonical artifact.

## v2.3.0 — ProjectIndex container

- Add `schemas/project_index.schema.json`.
- Add `ProjectIndex` model in Core.
- In registry mode (`--registry`), the CLI writes a single `ProjectIndex` JSON to `--out`.
- `ProjectIndex.indexes[]` is sorted by `profile_id` and each embedded `SymbolIndex` keeps existing ordering guarantees.

## v2.3.1 — Canonical JSON and stable hashing

- Add canonical JSON writing for reproducible diffs.
- Add optional `project_sha256` computed from sorted `(file_id, sha256)` pairs plus engine and registry identifiers.

## v2.3.2 — Incremental indexing cache

- Optional cache keyed by `(profile_id, file_id, file_sha256)`.
- Reuse unchanged per-file fragments and merge to match full rebuild output.

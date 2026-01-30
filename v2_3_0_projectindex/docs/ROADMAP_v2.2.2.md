# CodeIndex v2.2.2 — Emitters and Profile Registry

## Goals

1. Add ingestion-friendly emitters (CSV and JSONL rows) without changing the SymbolIndex contract.
2. Add a profile registry so repo-level discovery can select profiles per file based on glob rules.

## Deliverables

### A) Emitters

- `tools/emit_rows.py` converts a schema-valid `SymbolIndex` JSON into:
  - `csv` with a stable header
  - `jsonl` (one object per occurrence)

Row fields:
- identifier
- file_id
- line
- col_start
- col_end
- byte_start/byte_end (if present)

### B) Profile registry

- `profiles/registry.json` defines:
  - profile aliases -> profile JSON paths
  - ordered glob rules mapping files to aliases

- `tools/profile_registry.py` resolves a file path to a profile alias/path.

## Future (v2.3)

- Integrate the registry into the engine/CLI discovery pipeline so a single run can index mixed-language repos.
- Add per-file profile selection in `inputs-file` lists.

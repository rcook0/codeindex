# CodeIndex — Schemas + Golden Corpus (v2.1)

This bundle contains:
- JSON Schemas for `LanguageProfile` and `SymbolIndex`
- A golden corpus (`corpus/`) with inputs and expected outputs
- A metadata regeneration tool (`tools/regen_meta.py`) to compute bytes/lines/sha256

## Quick start

1) Regenerate stable metadata in expected outputs:

```bash
python3 tools/regen_meta.py
```

2) Validate JSON (optional):
Use any JSON Schema validator against `schemas/*.schema.json`.

## Notes
- Expected outputs include line/column spans that a compliant implementation should match for this corpus.
- The corpus is intentionally small and sharp-edged: it targets comments, literals, stop words, and ordering.

## .NET Implementation (C#)

See [src/README-DOTNET.md](src/README-DOTNET.md).

---

## .NET reference implementation (2.1.1)

This repo includes a Microsoft-friendly reference implementation in **C# (.NET 8)** under `src/`.

### Build

- Windows (PowerShell):
  - `pwsh tools/run_subset.ps1`

- Linux/macOS (bash):
  - `bash tools/run_subset.sh`

### CLI usage

```bash
# Declared-only mode (default; matches 2.1.1 corpus subset)
dotnet run --project src/CodeIndex.Cli -c Release -- \
  --profile corpus/profiles.java.json \
  --input corpus/inputs/java_basic.java \
  --out out.json

# Index all identifiers (ignores declared-only filter)
dotnet run --project src/CodeIndex.Cli -c Release -- \
  --profile corpus/profiles.java.json \
  --input corpus/inputs/java_basic.java \
  --out out.json \
  --all-identifiers
```

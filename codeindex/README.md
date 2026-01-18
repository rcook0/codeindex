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

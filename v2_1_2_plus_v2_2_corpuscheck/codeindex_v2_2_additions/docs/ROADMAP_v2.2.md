# CodeIndex Roadmap — v2.2 (Multi-file + Project Conveniences)

This roadmap extends the v2.1 contract-driven baseline (schemas + golden corpus) to **project-scale** indexing.

## v2.2.0 — Multi-file indexing (contracted)

### Goal
Produce a single `SymbolIndex` that aggregates identifiers and occurrences across **multiple files** deterministically.

### Core behaviors
1. **Multi-input API**
   - Add `index_many(inputs, options) -> SymbolIndex`.
   - Preserve `index(input, options)`.

2. **CLI multi-file support**
   Support at least one contract-friendly mechanism:
   - `--inputs-file <path>` (one file path per line; recommended)
   - `--input <path>` repeatable (optional)
   - `--input-glob <pattern>` (optional)

3. **Deterministic output**
   - `files[]` sorted lexicographically by `file_id`.
   - `symbols[]` sorted lexicographically by `identifier`.
   - For each symbol, `occurrences[]` sorted by `(file_id, line, col_start)`.

4. **Symbol merge semantics**
   - A symbol key is unique across the whole index.
   - Occurrences are the union of occurrences across all files.
   - If deduplication is enabled, duplicates are removed using the tuple `(file_id, line, col_start, col_end)`.

5. **Stats semantics for multi-file**
   - `occurrence_count = len(occurrences)`.
   - `unique_line_count = count(distinct (file_id, line))`.

### Contract additions
Add a new corpus slice under `corpus/multifile/`:
- `inputs/` contains a small multi-file code sample.
- `expected/` contains the expected aggregated `SymbolIndex` JSON.

**Exit criteria**
- Running the engine on `corpus/multifile/inputs/*` produces an output that diffs cleanly vs. `corpus/multifile/expected/*.json`.
- Output is identical even if input file ordering is permuted.

---

## v2.2.1 — Project-level conveniences (still deterministic)

### Goal
Make CodeIndex usable on real repositories without changing the core lexical model.

### Conveniences
1. **Directory traversal**
   - `--root <dir>` recursively collects files.
   - `--include-glob <pattern>` (default patterns may be profile-informed, e.g. `**/*.java`).

2. **Exclusions**
   - `--exclude-glob <pattern>` repeatable.
   - `--exclude-file <path>` (gitignore-like list; one pattern per line).

3. **Stable path normalization**
   - `--path-base <dir>` makes `file_id` relative to a stable base.
   - `file_id` MUST remain deterministic across runs for the same project layout.

4. **Batch runner behavior**
   - `--follow-symlinks` optional, default off.
   - `--max-file-size` guardrail.
   - Clear non-zero exit codes on read errors (configurable tolerant/strict).

5. **Output ergonomics**
   - `--format json|csv|text` (JSON remains canonical).
   - `--out <path>` or write to stdout.
   - Optional `--emit-manifest` listing included/excluded files (debugging determinism).

**Exit criteria**
- Convenience flags do not change indexing semantics other than selecting which files are included.
- Full output determinism under identical inputs and options.

---

## Notes
- v2.2 preserves the principle: **profiles are data, policy is data**.
- The engine remains grammar-light: no AST, no semantic resolution.

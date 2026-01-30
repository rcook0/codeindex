# Multi-file corpus slice (v2.2)

This slice validates **multi-file aggregation** with deterministic ordering.

## Inputs
- `A.java`
- `B.java`

## Expected
- `expected/java_two_files.expected.json`

## Requirements validated
- `files[]` contains both files with correct `bytes/lines/sha256`.
- `symbols[]` merges occurrences across files.
- Sorting invariants:
  - files sorted by `file_id`
  - symbols sorted by `identifier`
  - occurrences sorted by `(file_id, line, col_start)`

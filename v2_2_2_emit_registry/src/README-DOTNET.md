# CodeIndex .NET (C#) Implementation

This folder contains a Microsoft-friendly implementation path for CodeIndex.

## Requirements
- .NET SDK 8.0+

## Build

From repository root:

```bash
cd src
# Windows PowerShell
#   dotnet build CodeIndex.sln -c Release
# Linux/macOS
#   dotnet build CodeIndex.sln -c Release
```

## Run (CLI)

```bash
dotnet run --project src/CodeIndex.Cli/CodeIndex.Cli.csproj -- \
  --profile corpus/profiles.java.json \
  --input corpus/inputs/java_basic.java \
  --out /tmp/out.json
```

## Project discovery (v2.2.1)

You can index a directory tree without pre-enumerating files.

### Index all Java files under a root

```bash
dotnet run --project src/CodeIndex.Cli/CodeIndex.Cli.csproj -- \
  --profile corpus/profiles.java.json \
  --root ./src \
  --recursive \
  --include-glob "**/*.java" \
  --exclude-glob "**/bin/**" \
  --exclude-glob "**/obj/**" \
  --out /tmp/out.json
```

### Discovery flags

- `--root <dir>`: root directory for discovery
- `--recursive`: recurse into subdirectories (default: off)
- `--include-glob <pattern>`: include filter (repeatable). If omitted, all files are included.
- `--exclude-glob <pattern>`: exclude filter (repeatable)
- `--follow-symlinks true|false`: whether to traverse symlinked directories (default: false)
- `--max-file-size-bytes N`: skip files larger than N bytes

When `--root` is provided, the emitted `file_id` is the path relative to root, with normalized `/` separators.

By default, 2.1.1 runs in `--declared-only` mode to match the current golden corpus.

To index *all* identifiers (lexer-only), use:

```bash
dotnet run --project src/CodeIndex.Cli/CodeIndex.Cli.csproj -- --declared-only false ...
```

## v2.2.2 — Emitters and Profile Registry

This release adds two repo-level capabilities:

### 1) Profile registry (mixed-language repos)

Use `--registry` to map discovered files to profiles by glob rules, producing **one output per profile**.

Example:

```bash
# indexes all *.java and *.cpp under src/ using profiles/registry.json
# outputs: out/java.symbol_index.json and out/cpp.symbol_index.json

dotnet run --project src/CodeIndex.Cli/CodeIndex.Cli.csproj -- \
  --registry profiles/registry.json \
  --root ./src \
  --recursive \
  --exclude-glob "**/bin/**" \
  --exclude-glob "**/obj/**" \
  --out out.json
```

Notes:
- If multiple profiles are used, `--out` acts as a base path and outputs are written to an `out/` directory next to it.
- Use `--out-dir <dir>` to choose the output directory explicitly.

### 2) Ingestion-friendly emitters

You can also emit a flat occurrence table for ingestion into databases.

```bash
# emits java.symbol_index.json and java.rows.csv

dotnet run --project src/CodeIndex.Cli/CodeIndex.Cli.csproj -- \
  --registry profiles/registry.json \
  --root ./src --recursive \
  --emit-rows csv \
  --out-dir ./out
```

`--emit-rows` supports:
- `csv` (header + rows)
- `jsonl` (one JSON object per row)

Row fields:
- `identifier`, `file_id`, `line`, `col_start`, `col_end`
- optional `byte_start`, `byte_end` when present

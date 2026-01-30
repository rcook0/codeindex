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

By default, 2.1.1 runs in `--declared-only` mode to match the current golden corpus.

To index *all* identifiers (lexer-only), use:

```bash
dotnet run --project src/CodeIndex.Cli/CodeIndex.Cli.csproj -- --declared-only false ...
```

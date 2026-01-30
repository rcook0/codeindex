#!/usr/bin/env bash
set -euo pipefail

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK not found. Install .NET 8 SDK." >&2
  exit 2
fi

dotnet build src/CodeIndex.sln -c Release

profile="corpus/profiles.java.json"
mkdir -p _tmp

dotnet run --project src/CodeIndex.Cli -c Release -- --profile "$profile" --input corpus/inputs/java_basic.java --out _tmp/java_basic.out.json

dotnet run --project src/CodeIndex.Cli -c Release -- --profile "$profile" --input corpus/inputs/tricky_comments.java --out _tmp/tricky_comments.out.json

# Optional: run the (no-deps) test runner

dotnet run --project src/CodeIndex.Tests -c Release

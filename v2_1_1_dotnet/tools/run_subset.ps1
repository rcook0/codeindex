# Runs 2.1.1 subset against the golden corpus on Windows.
# Usage:
#   pwsh tools/run_subset.ps1

$ErrorActionPreference = "Stop"

dotnet build src/CodeIndex.sln -c Release

$profile = "corpus/profiles.java.json"

function Run-One($input) {
  $out = "_tmp/$input.out.json"
  dotnet run --project src/CodeIndex.Cli -c Release -- --profile $profile --input "corpus/inputs/$input" --out $out
  return $out
}

$out1 = Run-One "java_basic.java"
$out2 = Run-One "tricky_comments.java"

Write-Host "Wrote $out1"
Write-Host "Wrote $out2"

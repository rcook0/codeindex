using CodeIndex.Core.Engine;
using CodeIndex.Core.IO;

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i] == name) return args[i + 1];
    return null;
}

static bool HasFlag(string[] args, string name) => args.Any(a => a == name);

var profilePath = GetArg(args, "--profile") ?? "corpus/profiles.java.json";
var inputPath = GetArg(args, "--input");
var outPath = GetArg(args, "--out") ?? "out.json";
var fileId = GetArg(args, "--file-id") ?? (inputPath is null ? "" : Path.GetFileName(inputPath));

if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
{
    Console.Error.WriteLine("Usage: codeindex --profile <profile.json> --input <source> [--out out.json] [--declared-only|--all-identifiers] [--include-single-letter]");
    Environment.Exit(2);
}

var profile = ProfileLoader.LoadFromPath(profilePath);

var options = new IndexOptions
{
    Profile = profile,
    DeclaredOnly = !HasFlag(args, "--all-identifiers"),
    ExcludeSingleLetter = !HasFlag(args, "--include-single-letter"),
    GeneratedAt = GetArg(args, "--generated-at") ?? "2026-01-16T00:00:00Z",
    Ordering = GetArg(args, "--ordering") ?? "lex"
};

var engine = new IndexEngine();
var index = engine.IndexFile(inputPath!, fileId, options);
IndexEngine.WriteJson(index, outPath);
Console.WriteLine($"Wrote {outPath}");

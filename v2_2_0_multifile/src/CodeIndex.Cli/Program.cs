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
var outPath = GetArg(args, "--out") ?? "out.json";

static List<string> GetArgsMulti(string[] args, string name)
{
    var res = new List<string>();
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i] == name) res.Add(args[i + 1]);
    return res;
}

static IEnumerable<string> ReadInputsFile(string path)
{
    foreach (var raw in File.ReadAllLines(path))
    {
        var line = raw.Trim();
        if (line.Length == 0) continue;
        if (line.StartsWith("#")) continue;
        yield return line;
    }
}

var inputs = new List<string>();
inputs.AddRange(GetArgsMulti(args, "--input"));

var inputsFile = GetArg(args, "--inputs-file");
if (!string.IsNullOrWhiteSpace(inputsFile))
{
    if (!File.Exists(inputsFile))
    {
        Console.Error.WriteLine($"inputs-file not found: {inputsFile}");
        Environment.Exit(2);
    }
    inputs.AddRange(ReadInputsFile(inputsFile));
}

inputs = inputs.Distinct(StringComparer.Ordinal).ToList();

if (inputs.Count == 0 || inputs.Any(p => string.IsNullOrWhiteSpace(p) || !File.Exists(p)))
{
    Console.Error.WriteLine("Usage: codeindex --profile <profile.json> (--input <source> ... | --inputs-file <file>) [--out out.json] [--declared-only|--all-identifiers] [--exclude-single-letter|--include-single-letter] [--qualified none|dot|scope|dot_and_scope] [--include-headers true|false] [--generated-at <iso8601>] [--ordering lex]");
    Environment.Exit(2);
}

var profile = ProfileLoader.LoadFromPath(profilePath);

var options = new IndexOptions
{
    Profile = profile,
    DeclaredOnly = HasFlag(args, "--declared-only") ? true : (HasFlag(args, "--all-identifiers") ? false : (bool?)null),
    ExcludeSingleLetter = HasFlag(args, "--exclude-single-letter") ? true : (HasFlag(args, "--include-single-letter") ? false : (bool?)null),
    IncludeQualifiedIdentifiers = GetArg(args, "--qualified"),
    IncludeIncludeHeaders = GetArg(args, "--include-headers") is string s ? bool.Parse(s) : (bool?)null,
    GeneratedAt = GetArg(args, "--generated-at") ?? "2026-01-16T00:00:00Z",
    Ordering = GetArg(args, "--ordering") ?? "lex"
};

var engine = new IndexEngine();
var sourceInputs = inputs
    .Select(p => new SourceInput(p, Path.GetFileName(p)))
    .ToList();

var index = engine.IndexFiles(sourceInputs, options);
IndexEngine.WriteJson(index, outPath);
Console.WriteLine($"Wrote {outPath}");

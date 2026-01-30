using CodeIndex.Core.Engine;
using CodeIndex.Core.IO;
using System.Text.RegularExpressions;

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i] == name) return args[i + 1];
    return null;
}

static bool HasFlag(string[] args, string name) => args.Any(a => a == name);

static string NormalizeSlashes(string path) => path.Replace('\\', '/');

static string MakeFileId(string fullPath, string? root)
{
    // Stable IDs: if root is provided, emit root-relative path; otherwise, emit the basename.
    if (!string.IsNullOrWhiteSpace(root))
    {
        var rel = Path.GetRelativePath(root, fullPath);
        return NormalizeSlashes(rel);
    }
    return Path.GetFileName(fullPath);
}

static Regex GlobToRegex(string pattern)
{
    // Minimal globbing: *, ?, **. Path separators normalized to '/'.
    var p = NormalizeSlashes(pattern.Trim());
    var sb = new System.Text.StringBuilder();
    sb.Append('^');
    for (int i = 0; i < p.Length; i++)
    {
        char c = p[i];
        if (c == '*')
        {
            bool isDouble = (i + 1 < p.Length && p[i + 1] == '*');
            if (isDouble)
            {
                sb.Append(".*");
                i++;
            }
            else
            {
                sb.Append("[^/]*");
            }
        }
        else if (c == '?')
        {
            sb.Append("[^/]");
        }
        else
        {
            sb.Append(Regex.Escape(c.ToString()));
        }
    }
    sb.Append('$');
    return new Regex(sb.ToString(), RegexOptions.Compiled);
}

static IEnumerable<string> DiscoverFiles(
    string root,
    bool recursive,
    bool followSymlinks,
    long? maxFileSizeBytes,
    IReadOnlyList<Regex> include,
    IReadOnlyList<Regex> exclude)
{
    // Deterministic DFS with sorted directory entries.
    var stack = new Stack<string>();
    stack.Push(Path.GetFullPath(root));

    while (stack.Count > 0)
    {
        var dir = stack.Pop();
        IEnumerable<string> files;
        IEnumerable<string> dirs;

        try
        {
            files = Directory.EnumerateFiles(dir);
            dirs = Directory.EnumerateDirectories(dir);
        }
        catch
        {
            continue;
        }

        foreach (var file in files.OrderBy(p => p, StringComparer.Ordinal))
        {
            var full = Path.GetFullPath(file);
            var rel = NormalizeSlashes(Path.GetRelativePath(root, full));

            if (exclude.Any(r => r.IsMatch(rel))) continue;
            if (include.Count > 0 && !include.Any(r => r.IsMatch(rel))) continue;

            if (maxFileSizeBytes.HasValue)
            {
                try
                {
                    var fi = new FileInfo(full);
                    if (fi.Length > maxFileSizeBytes.Value) continue;
                }
                catch
                {
                    continue;
                }
            }

            yield return full;
        }

        if (!recursive) continue;

        foreach (var sub in dirs.OrderBy(p => p, StringComparer.Ordinal).Reverse())
        {
            try
            {
                var di = new DirectoryInfo(sub);
                if (!followSymlinks && di.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    continue;
                stack.Push(di.FullName);
            }
            catch
            {
                // ignore
            }
        }
    }
}

var profilePath = GetArg(args, "--profile") ?? "corpus/profiles.java.json";
var outPath = GetArg(args, "--out") ?? "out.json";

var root = GetArg(args, "--root");
var recursive = HasFlag(args, "--recursive");
var followSymlinks = GetArg(args, "--follow-symlinks") is string fs ? bool.Parse(fs) : false;
var maxFileSizeBytes = GetArg(args, "--max-file-size-bytes") is string mx ? long.Parse(mx) : (long?)null;

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

// Project discovery mode
var includeGlobs = GetArgsMulti(args, "--include-glob");
var excludeGlobs = GetArgsMulti(args, "--exclude-glob");
var includeRe = includeGlobs.Select(GlobToRegex).ToList();
var excludeRe = excludeGlobs.Select(GlobToRegex).ToList();

if (!string.IsNullOrWhiteSpace(root))
{
    if (!Directory.Exists(root))
    {
        Console.Error.WriteLine($"root not found: {root}");
        Environment.Exit(2);
    }

    var discovered = DiscoverFiles(root, recursive: recursive, followSymlinks: followSymlinks,
        maxFileSizeBytes: maxFileSizeBytes, include: includeRe, exclude: excludeRe);
    inputs.AddRange(discovered);
}

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
    Console.Error.WriteLine("Usage: codeindex --profile <profile.json> ( --input <source> ... | --inputs-file <file> | --root <dir> [--recursive] [--include-glob <pat> ...] [--exclude-glob <pat> ...] ) [--out out.json] [--follow-symlinks true|false] [--max-file-size-bytes N] [--declared-only|--all-identifiers] [--exclude-single-letter|--include-single-letter] [--qualified none|dot|scope|dot_and_scope] [--include-headers true|false] [--generated-at <iso8601>] [--ordering lex]");
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
    .Select(p => new SourceInput(p, MakeFileId(Path.GetFullPath(p), root)))
    .ToList();

var index = engine.IndexFiles(sourceInputs, options);
IndexEngine.WriteJson(index, outPath);
Console.WriteLine($"Wrote {outPath}");

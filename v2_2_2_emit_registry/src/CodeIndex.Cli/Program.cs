using CodeIndex.Core.Engine;
using CodeIndex.Core.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i] == name) return args[i + 1];
    return null;
}

static bool HasFlag(string[] args, string name) => args.Any(a => a == name);

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
    var sb = new StringBuilder();
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

// ------------------- Profile Registry -------------------

typealias Rule = (Regex re, string alias);

static (Dictionary<string, string> profiles, List<Rule> rules) LoadProfileRegistry(string registryPath)
{
    using var fs = File.OpenRead(registryPath);
    var doc = JsonDocument.Parse(fs);

    var profiles = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var prop in doc.RootElement.GetProperty("profiles").EnumerateObject())
        profiles[prop.Name] = prop.Value.GetString() ?? throw new Exception("Invalid registry: profile path null");

    var rules = new List<Rule>();
    foreach (var rule in doc.RootElement.GetProperty("rules").EnumerateArray())
    {
        var glob = rule.GetProperty("match").GetProperty("glob").GetString() ?? "";
        var alias = rule.GetProperty("profile").GetString() ?? "";
        rules.Add((GlobToRegex(glob), alias));
    }

    return (profiles, rules);
}

static (string alias, string profilePath) ResolveProfileForFile(
    (Dictionary<string, string> profiles, List<Rule> rules) reg,
    string fileId)
{
    // fileId is expected to be root-relative and '/' normalized.
    foreach (var (re, alias) in reg.rules)
    {
        if (re.IsMatch(fileId))
        {
            if (!reg.profiles.TryGetValue(alias, out var profilePath))
                throw new Exception($"Registry refers to unknown profile alias: {alias}");
            return (alias, profilePath);
        }
    }
    throw new Exception($"No profile rule matched file_id: {fileId}");
}

// ------------------- Rows Emitter -------------------

static void EmitRows(SymbolIndex index, string format, string outPath)
{
    // format: csv | jsonl
    if (format != "csv" && format != "jsonl")
        throw new Exception("emit-rows format must be csv or jsonl");

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);

    if (format == "jsonl")
    {
        using var w = new StreamWriter(outPath, false, Encoding.UTF8);
        foreach (var sym in index.Symbols)
        {
            foreach (var occ in sym.Occurrences)
            {
                var row = new Dictionary<string, object?>
                {
                    ["identifier"] = sym.Identifier,
                    ["file_id"] = occ.FileId,
                    ["line"] = occ.Line,
                    ["col_start"] = occ.ColStart,
                    ["col_end"] = occ.ColEnd,
                    ["byte_start"] = occ.ByteStart,
                    ["byte_end"] = occ.ByteEnd
                };
                // Remove null byte offsets for cleanliness.
                if (occ.ByteStart is null) row.Remove("byte_start");
                if (occ.ByteEnd is null) row.Remove("byte_end");

                w.WriteLine(JsonSerializer.Serialize(row));
            }
        }
        return;
    }

    // CSV
    using (var w = new StreamWriter(outPath, false, Encoding.UTF8))
    {
        bool hasBytes = index.Symbols.Any(s => s.Occurrences.Any(o => o.ByteStart is not null || o.ByteEnd is not null));
        var header = hasBytes
            ? "identifier,file_id,line,col_start,col_end,byte_start,byte_end"
            : "identifier,file_id,line,col_start,col_end";
        w.WriteLine(header);

        static string Esc(string s)
        {
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
                return '"' + s.Replace("\"", "\"\"") + '"';
            return s;
        }

        foreach (var sym in index.Symbols)
        {
            foreach (var occ in sym.Occurrences)
            {
                var parts = new List<string>
                {
                    Esc(sym.Identifier),
                    Esc(occ.FileId),
                    occ.Line.ToString(),
                    occ.ColStart.ToString(),
                    occ.ColEnd.ToString()
                };

                if (hasBytes)
                {
                    parts.Add(occ.ByteStart?.ToString() ?? "");
                    parts.Add(occ.ByteEnd?.ToString() ?? "");
                }

                w.WriteLine(string.Join(',', parts));
            }
        }
    }
}

// ------------------- Main -------------------

var outPath = GetArg(args, "--out") ?? "out.json";
var outDir = GetArg(args, "--out-dir");

var root = GetArg(args, "--root");
var recursive = HasFlag(args, "--recursive");
var followSymlinks = GetArg(args, "--follow-symlinks") is string fs ? bool.Parse(fs) : false;
var maxFileSizeBytes = GetArg(args, "--max-file-size-bytes") is string mx ? long.Parse(mx) : (long?)null;

var includeGlobs = GetArgsMulti(args, "--include-glob");
var excludeGlobs = GetArgsMulti(args, "--exclude-glob");
var includeRe = includeGlobs.Select(GlobToRegex).ToList();
var excludeRe = excludeGlobs.Select(GlobToRegex).ToList();

var inputs = new List<string>();
inputs.AddRange(GetArgsMulti(args, "--input"));

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

var registryPath = GetArg(args, "--registry");
var singleProfilePath = GetArg(args, "--profile") ?? "corpus/profiles.java.json";

var emitRowsFormat = GetArg(args, "--emit-rows"); // csv|jsonl
var emitRowsOut = GetArg(args, "--emit-rows-out"); // path or dir

if (inputs.Count == 0 || inputs.Any(p => string.IsNullOrWhiteSpace(p) || !File.Exists(p)))
{
    Console.Error.WriteLine(
        "Usage: codeindex [--profile <profile.json>] [--registry <registry.json>] " +
        "( --input <source> ... | --inputs-file <file> | --root <dir> [--recursive] [--include-glob <pat> ...] [--exclude-glob <pat> ...] ) " +
        "[--out out.json] [--out-dir <dir>] [--follow-symlinks true|false] [--max-file-size-bytes N] " +
        "[--declared-only|--all-identifiers] [--exclude-single-letter|--include-single-letter] [--qualified none|dot|scope|dot_and_scope] " +
        "[--include-headers true|false] [--generated-at <iso8601>] [--ordering lex] " +
        "[--emit-rows csv|jsonl] [--emit-rows-out <path-or-dir>]");
    Environment.Exit(2);
}

var engine = new IndexEngine();

var commonOptions = new IndexOptions
{
    DeclaredOnly = HasFlag(args, "--declared-only") ? true : (HasFlag(args, "--all-identifiers") ? false : (bool?)null),
    ExcludeSingleLetter = HasFlag(args, "--exclude-single-letter") ? true : (HasFlag(args, "--include-single-letter") ? false : (bool?)null),
    IncludeQualifiedIdentifiers = GetArg(args, "--qualified"),
    IncludeIncludeHeaders = GetArg(args, "--include-headers") is string s ? bool.Parse(s) : (bool?)null,
    GeneratedAt = GetArg(args, "--generated-at") ?? "2026-01-16T00:00:00Z",
    Ordering = GetArg(args, "--ordering") ?? "lex"
};

// Build SourceInputs with stable file IDs.
var sourceInputs = inputs
    .Select(p => new SourceInput(p, MakeFileId(Path.GetFullPath(p), root)))
    .ToList();

if (string.IsNullOrWhiteSpace(registryPath))
{
    // Single-profile run (existing behavior).
    var profile = ProfileLoader.LoadFromPath(singleProfilePath);
    var options = commonOptions with { Profile = profile };

    var index = engine.IndexFiles(sourceInputs, options);
    IndexEngine.WriteJson(index, outPath);
    Console.WriteLine($"Wrote {outPath}");

    if (!string.IsNullOrWhiteSpace(emitRowsFormat))
    {
        var rowsOut = emitRowsOut ?? (Path.ChangeExtension(outPath, emitRowsFormat == "csv" ? ".csv" : ".jsonl"));
        EmitRows(index, emitRowsFormat, rowsOut);
        Console.WriteLine($"Wrote {rowsOut}");
    }

    Environment.Exit(0);
}

// Registry run: group inputs by resolved profile alias.
if (!File.Exists(registryPath))
{
    Console.Error.WriteLine($"registry not found: {registryPath}");
    Environment.Exit(2);
}

var reg = LoadProfileRegistry(registryPath);

var groups = new Dictionary<(string alias, string profilePath), List<SourceInput>>();
foreach (var si in sourceInputs)
{
    var (alias, profPath) = ResolveProfileForFile(reg, si.FileId);
    var key = (alias, profPath);
    if (!groups.TryGetValue(key, out var list))
    {
        list = new List<SourceInput>();
        groups[key] = list;
    }
    list.Add(si);
}

// Determine output directory.
var effectiveOutDir = outDir;
if (string.IsNullOrWhiteSpace(effectiveOutDir))
{
    // If --out-dir is not provided, default to a sibling folder 'out' next to --out.
    effectiveOutDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? ".", "out");
}
Directory.CreateDirectory(effectiveOutDir);

foreach (var kv in groups.OrderBy(k => k.Key.alias, StringComparer.Ordinal))
{
    var (alias, profPath) = kv.Key;
    var profile = ProfileLoader.LoadFromPath(profPath);
    var options = commonOptions with { Profile = profile };

    var index = engine.IndexFiles(kv.Value, options);

    var jsonOut = Path.Combine(effectiveOutDir, $"{alias}.symbol_index.json");
    IndexEngine.WriteJson(index, jsonOut);
    Console.WriteLine($"Wrote {jsonOut}");

    if (!string.IsNullOrWhiteSpace(emitRowsFormat))
    {
        var rowsOutPath = emitRowsOut;
        if (string.IsNullOrWhiteSpace(rowsOutPath))
        {
            rowsOutPath = Path.Combine(effectiveOutDir, $"{alias}.rows.{(emitRowsFormat == "csv" ? "csv" : "jsonl")}");
        }
        else if (Directory.Exists(rowsOutPath) || rowsOutPath.EndsWith(Path.DirectorySeparatorChar) || rowsOutPath.EndsWith('/'))
        {
            rowsOutPath = Path.Combine(rowsOutPath, $"{alias}.rows.{(emitRowsFormat == "csv" ? "csv" : "jsonl")}");
        }
        EmitRows(index, emitRowsFormat, rowsOutPath);
        Console.WriteLine($"Wrote {rowsOutPath}");
    }
}

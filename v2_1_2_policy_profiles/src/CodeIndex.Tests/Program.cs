using System.Text.Json;
using CodeIndex.Core.Engine;
using CodeIndex.Core.IO;

static void Assert(bool cond, string msg)
{
    if (!cond) throw new Exception(msg);
}

static JsonElement LoadJson(string path)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    return doc.RootElement.Clone();
}

static void Compare(string expectedPath, string actualPath)
{
    var e = LoadJson(expectedPath);
    var a = LoadJson(actualPath);

    // Compare with a small normalization: ignore generated_at if present.
    // (You can make this strict later.)
    string Normalize(JsonElement el)
    {
        using var doc = JsonDocument.Parse(el.GetRawText());
        var obj = doc.RootElement;
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(obj.GetRawText())!;
        dict["generated_at"] = "2026-01-16T00:00:00Z";
        return JsonSerializer.Serialize(dict);
    }

    Assert(Normalize(e) == Normalize(a), $"Mismatch: {expectedPath} vs {actualPath}");
}

var repoRoot = Directory.GetCurrentDirectory();
var profile = ProfileLoader.LoadFromPath(Path.Combine(repoRoot, "corpus", "profiles.java.json"));

var engine = new IndexEngine();

string RunCase(string inputName)
{
    var inputPath = Path.Combine(repoRoot, "corpus", "inputs", inputName);
    var outPath = Path.Combine(repoRoot, "_tmp", inputName + ".out.json");
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

    var idx = engine.IndexFile(inputPath, inputName, new IndexOptions { Profile = profile });
    IndexEngine.WriteJson(idx, outPath);
    return outPath;
}

var a1 = RunCase("java_basic.java");
Compare(Path.Combine(repoRoot, "corpus", "expected", "java_basic.expected.json"), a1);

var a2 = RunCase("tricky_comments.java");
Compare(Path.Combine(repoRoot, "corpus", "expected", "tricky_comments.expected.json"), a2);

Console.WriteLine("OK: subset corpus matches");

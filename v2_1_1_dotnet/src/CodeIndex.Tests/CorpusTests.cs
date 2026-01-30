using System.Text.Json;
using Xunit;
using CodeIndex.Engine;
using CodeIndex.IO;

namespace CodeIndex.Tests;

public class CorpusTests
{
    [Fact]
    public void CanLoadProfiles()
    {
        var java = ProfileLoader.LoadFromFile(Path.Combine("..","..","..","..","corpus","profiles.java.json"));
        Assert.Contains("java", java.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanIndexJavaBasic_Deterministic()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..","..","..",".."));
        var profilePath = Path.Combine(root, "corpus","profiles.java.json");
        var inputPath = Path.Combine(root, "corpus","inputs","java_basic.java");

        var profile = ProfileLoader.LoadFromFile(profilePath);
        var engine = new IndexEngine();
        var idx = engine.IndexFile(inputPath, "java_basic.java", profile, new IndexOptions());

        // must be sorted
        var idents = idx.Symbols.Select(s => s.Identifier).ToList();
        var sorted = idents.OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, idents);

        // JSON roundtrip
        var json = JsonSerializer.Serialize(idx, JsonUtil.Options);
        Assert.Contains("\"schema_version\": \"2.1\"", json);
    }
}

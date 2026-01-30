using System.Text.Json;
using Xunit;
using CodeIndex.Core.Engine;
using CodeIndex.Core.IO;
using CodeIndex.Core.Model;

namespace CodeIndex.Tests;

public class CorpusTests
{
    [Fact]
    public void CanLoadProfiles()
    {
        var java = ProfileLoader.LoadFromPath(Path.Combine("..","..","..","..","corpus","profiles.java.json"));
        Assert.Contains("java", java.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanIndexJavaBasic_Deterministic()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..","..","..",".."));
        var profilePath = Path.Combine(root, "corpus","profiles.java.json");
        var inputPath = Path.Combine(root, "corpus","inputs","java_basic.java");

        var profile = ProfileLoader.LoadFromPath(profilePath);
        var engine = new IndexEngine();
        var idx = engine.IndexFile(inputPath, "java_basic.java", new IndexOptions { Profile = profile });

        // must be sorted
        var idents = idx.Symbols.Select(s => s.Identifier).ToList();
        var sorted = idents.OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, idents);

        // JSON roundtrip
        var json = JsonSerializer.Serialize(idx);
        Assert.Contains("\"schema_version\": \"2.1\"", json);
    }

    [Fact]
    public void CanIndexMultiFile_AndStatsUseFileAndLine()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..","..","..",".."));
        var profilePath = Path.Combine(root, "corpus","profiles.java.json");
        var inputsDir = Path.Combine(root, "corpus","multifile","inputs");
        var expectedPath = Path.Combine(root, "corpus","multifile","expected","java_two_files.expected.json");

        var profile = ProfileLoader.LoadFromPath(profilePath);
        var engine = new IndexEngine();

        var a = Path.Combine(inputsDir, "A.java");
        var b = Path.Combine(inputsDir, "B.java");

        var actual = engine.IndexFiles(new[]
        {
            new SourceInput(a, "A.java"),
            new SourceInput(b, "B.java"),
        }, new IndexOptions { Profile = profile });

        // Load expected and compare key invariants.
        var expectedJson = File.ReadAllText(expectedPath);
        var expected = JsonSerializer.Deserialize<SymbolIndex>(expectedJson) ?? throw new Exception("expected parse failed");

        Assert.Equal(expected.ProfileId, actual.ProfileId);
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);

        // files sorted by file_id
        var filesSorted = actual.Files.Select(f => f.FileId).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(filesSorted, actual.Files.Select(f => f.FileId).ToList());

        // unique_line_count should count distinct (file_id,line)
        foreach (var s in actual.Symbols)
        {
            if (s.Stats is null) continue;
            var distinct = s.Occurrences.Select(o => (o.FileId, o.Line)).Distinct().Count();
            Assert.Equal(distinct, s.Stats.UniqueLineCount);
        }

        // Basic structural equality: identifiers and occurrence counts match expected.
        Assert.Equal(expected.Symbols.Select(x => x.Identifier), actual.Symbols.Select(x => x.Identifier));
        for (int i = 0; i < expected.Symbols.Count; i++)
        {
            Assert.Equal(expected.Symbols[i].Occurrences.Count, actual.Symbols[i].Occurrences.Count);
        }
    }
}

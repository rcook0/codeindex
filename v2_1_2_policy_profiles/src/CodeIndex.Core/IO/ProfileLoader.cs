using System.Text.Json;
using CodeIndex.Core.Model;

namespace CodeIndex.Core.IO;

public static class ProfileLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static LanguageProfile LoadFromPath(string path)
    {
        var json = File.ReadAllText(path);
        var profile = JsonSerializer.Deserialize<LanguageProfile>(json, Options);
        if (profile is null) throw new InvalidOperationException($"Failed to parse LanguageProfile: {path}");
        return profile;
    }
}

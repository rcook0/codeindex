using System.Text.Json.Serialization;

namespace CodeIndex.Core.Model;

public sealed class ProjectIndex
{
    [JsonPropertyName("schema_version")] public required string SchemaVersion { get; init; }
    [JsonPropertyName("project_root")] public required string ProjectRoot { get; init; }
    [JsonPropertyName("generated_at")] public required string GeneratedAt { get; init; }

    [JsonPropertyName("engine_version")] public string? EngineVersion { get; init; }
    [JsonPropertyName("registry_id")] public string? RegistryId { get; init; }
    [JsonPropertyName("project_sha256")] public string? ProjectSha256 { get; init; }

    [JsonPropertyName("indexes")] public required List<SymbolIndex> Indexes { get; init; }

    [JsonPropertyName("artifacts")] public ProjectArtifacts? Artifacts { get; init; }
    [JsonPropertyName("diagnostics")] public List<Diagnostic> Diagnostics { get; init; } = new();
}

public sealed class ProjectArtifacts
{
    [JsonPropertyName("rows")] public List<RowArtifact>? Rows { get; init; }
}

public sealed class RowArtifact
{
    [JsonPropertyName("profile_id")] public required string ProfileId { get; init; }
    [JsonPropertyName("format")] public required string Format { get; init; } // csv | jsonl
    [JsonPropertyName("path")] public required string Path { get; init; }
}

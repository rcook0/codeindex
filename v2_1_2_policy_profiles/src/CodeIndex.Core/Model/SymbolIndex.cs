using System.Text.Json.Serialization;

namespace CodeIndex.Core.Model;

public sealed class SymbolIndex
{
    [JsonPropertyName("schema_version")] public required string SchemaVersion { get; init; }
    [JsonPropertyName("profile_id")] public required string ProfileId { get; init; }
    [JsonPropertyName("ordering")] public required string Ordering { get; init; } // lex
    [JsonPropertyName("generated_at")] public required string GeneratedAt { get; init; }

    [JsonPropertyName("files")] public required List<FileSummary> Files { get; init; }
    [JsonPropertyName("symbols")] public required List<SymbolEntry> Symbols { get; init; }
    [JsonPropertyName("diagnostics")] public List<Diagnostic> Diagnostics { get; init; } = new();
}

public sealed class FileSummary
{
    [JsonPropertyName("file_id")] public required string FileId { get; init; }
    [JsonPropertyName("lines")] public required int Lines { get; init; }
    [JsonPropertyName("bytes")] public required int Bytes { get; init; }
    [JsonPropertyName("sha256")] public required string Sha256 { get; init; }
}

public sealed class SymbolEntry
{
    [JsonPropertyName("identifier")] public required string Identifier { get; init; }
    [JsonPropertyName("occurrences")] public required List<Occurrence> Occurrences { get; init; }
    [JsonPropertyName("stats")] public SymbolStats? Stats { get; init; }
}

public sealed class Occurrence
{
    [JsonPropertyName("file_id")] public required string FileId { get; init; }
    [JsonPropertyName("line")] public required int Line { get; init; }
    [JsonPropertyName("col_start")] public required int ColStart { get; init; }
    [JsonPropertyName("col_end")] public required int ColEnd { get; init; }
    [JsonPropertyName("byte_start")] public int? ByteStart { get; init; }
    [JsonPropertyName("byte_end")] public int? ByteEnd { get; init; }
}

public sealed class SymbolStats
{
    [JsonPropertyName("occurrence_count")] public required int OccurrenceCount { get; init; }
    [JsonPropertyName("unique_line_count")] public required int UniqueLineCount { get; init; }
}

public sealed class Diagnostic
{
    [JsonPropertyName("severity")] public required string Severity { get; init; }
    [JsonPropertyName("file_id")] public required string FileId { get; init; }
    [JsonPropertyName("line")] public required int Line { get; init; }
    [JsonPropertyName("col")] public required int Col { get; init; }
    [JsonPropertyName("message")] public required string Message { get; init; }
    [JsonPropertyName("code")] public required string Code { get; init; }
}

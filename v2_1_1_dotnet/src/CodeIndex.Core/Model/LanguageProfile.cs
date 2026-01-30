using System.Text.Json.Serialization;

namespace CodeIndex.Core.Model;

public sealed class LanguageProfile
{
    [JsonPropertyName("profile_id")] public required string ProfileId { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }

    [JsonPropertyName("case_sensitivity")] public required string CaseSensitivity { get; init; } // sensitive | insensitive
    [JsonPropertyName("normalization")] public required NormalizationPolicy Normalization { get; init; }
    [JsonPropertyName("identifier_rule")] public required IdentifierRule IdentifierRule { get; init; }
    [JsonPropertyName("stop_words")] public required StopWords StopWords { get; init; }
    [JsonPropertyName("comment_syntax")] public required CommentSyntax CommentSyntax { get; init; }
    [JsonPropertyName("literal_syntax")] public required LiteralSyntax LiteralSyntax { get; init; }
}

public sealed class NormalizationPolicy
{
    [JsonPropertyName("mode")] public required string Mode { get; init; } // none | nfkc | lowercase_ascii
    [JsonPropertyName("preserve_original_spelling")] public bool PreserveOriginalSpelling { get; init; } = true;
}

public sealed class IdentifierRule
{
    [JsonPropertyName("mode")] public required string Mode { get; init; } // regex | unicode_identifier
    [JsonPropertyName("pattern")] public string? Pattern { get; init; }
}

public sealed class StopWords
{
    [JsonPropertyName("mode")] public required string Mode { get; init; } // inline | uri | none
    [JsonPropertyName("words")] public List<string>? Words { get; init; }
    [JsonPropertyName("uri")] public string? Uri { get; init; }
}

public sealed class CommentSyntax
{
    [JsonPropertyName("line_comment_starts")] public required List<string> LineCommentStarts { get; init; }
    [JsonPropertyName("block_comment_starts")] public required List<string> BlockCommentStarts { get; init; }
    [JsonPropertyName("block_comment_ends")] public required List<string> BlockCommentEnds { get; init; }
}

public sealed class LiteralSyntax
{
    [JsonPropertyName("exclude_literals")] public required bool ExcludeLiterals { get; init; }
    [JsonPropertyName("string_delims")] public required List<string> StringDelims { get; init; }
    [JsonPropertyName("char_delims")] public required List<string> CharDelims { get; init; }
    [JsonPropertyName("escape_char")] public required string EscapeChar { get; init; }
    [JsonPropertyName("allow_multiline_strings")] public required bool AllowMultilineStrings { get; init; }
}

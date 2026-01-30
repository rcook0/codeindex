namespace CodeIndex.Engine;

public sealed class IndexOptions
{
    public bool DeclaredOnly { get; init; } = true; // 2.1.1 default to satisfy current corpus expectations
    public bool ExcludeSingleLetter { get; init; } = true;
    public string? GeneratedAt { get; init; } = "2026-01-16T00:00:00Z";
}

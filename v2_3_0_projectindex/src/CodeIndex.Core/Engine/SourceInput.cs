namespace CodeIndex.Core.Engine;

/// <summary>
/// Represents a single source input to be indexed.
/// Path is the filesystem path to read; FileId is the stable identifier emitted in the SymbolIndex.
/// </summary>
public sealed record SourceInput(string Path, string FileId);

namespace CodeIndex.Engine;

public enum TokenKind
{
    Identifier,
    Other,
    EndOfFile
}

public readonly record struct Token(
    TokenKind Kind,
    string Text,
    int Line,
    int ColStart,
    int ColEnd
);

using System.Text;
using System.Text.RegularExpressions;
using CodeIndex.Core.Model;

namespace CodeIndex.Core.Engine;

public enum LexState { Default, LineComment, BlockComment, String, Char }

public sealed record Token(
    string Kind, // Identifier | Other
    string Text,
    int Line,
    int ColStart,
    int ColEnd,
    int ByteStart,
    int ByteEnd
);

public sealed class Lexer
{
    private readonly LanguageProfile _profile;
    private readonly Regex _idRegex;

    public Lexer(LanguageProfile profile)
    {
        _profile = profile;
        if (!string.Equals(profile.IdentifierRule.Mode, "regex", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Only identifier_rule.mode=regex is supported in v2.1.1");
        if (string.IsNullOrWhiteSpace(profile.IdentifierRule.Pattern))
            throw new InvalidOperationException("identifier_rule.pattern is required for regex mode");

        _idRegex = new Regex($"\\G(?:{profile.IdentifierRule.Pattern})", RegexOptions.Compiled);
    }

    public IEnumerable<Token> Tokenize(string fileId, ReadOnlySpan<char> text)
    {
        // We track both char index and byte index as UTF-8 bytes for reproducibility.
        // v2.1.1 emits only line/col; byte offsets are available but optional.
        var state = LexState.Default;
        int line = 1;
        int col = 1;
        int i = 0;
        int bytePos = 0;

        // Precompute UTF-8 byte lengths incrementally (fast enough for corpus scale).
        static int Utf8Len(char ch)
        {
            if (ch <= 0x7F) return 1;
            if (ch <= 0x7FF) return 2;
            if (char.IsSurrogate(ch)) return 2; // best-effort; proper surrogate handling would need pairs
            return 3;
        }

        bool MatchAt(ReadOnlySpan<char> s, int idx, string marker)
        {
            if (marker.Length == 0) return false;
            if (idx + marker.Length > s.Length) return false;
            for (int k = 0; k < marker.Length; k++)
                if (s[idx + k] != marker[k]) return false;
            return true;
        }

        string? lineStart = _profile.CommentSyntax.LineCommentStarts.FirstOrDefault();
        string? blockStart = _profile.CommentSyntax.BlockCommentStarts.FirstOrDefault();
        string? blockEnd = _profile.CommentSyntax.BlockCommentEnds.FirstOrDefault();

        char stringDelim = _profile.LiteralSyntax.StringDelims.FirstOrDefault() is { Length: > 0 } sd ? sd[0] : '"';
        char charDelim = _profile.LiteralSyntax.CharDelims.FirstOrDefault() is { Length: > 0 } cd ? cd[0] : '\'';
        char escape = _profile.LiteralSyntax.EscapeChar is { Length: > 0 } ec ? ec[0] : '\\';

        while (i < text.Length)
        {
            char ch = text[i];

            // Newline normalization: treat \r\n and \r as newline.
            bool isCr = ch == '\r';
            bool isLf = ch == '\n';
            bool isNewline = isLf || isCr;

            // State transitions for comments/literals.
            if (state == LexState.Default)
            {
                if (lineStart != null && MatchAt(text, i, lineStart))
                {
                    state = LexState.LineComment;
                    // consume marker
                    for (int k = 0; k < lineStart.Length; k++) { bytePos += Utf8Len(text[i]); i++; col++; }
                    continue;
                }
                if (blockStart != null && MatchAt(text, i, blockStart))
                {
                    state = LexState.BlockComment;
                    for (int k = 0; k < blockStart.Length; k++) { bytePos += Utf8Len(text[i]); i++; col++; }
                    continue;
                }
                if (_profile.LiteralSyntax.ExcludeLiterals && ch == stringDelim)
                {
                    state = LexState.String;
                    bytePos += Utf8Len(ch); i++; col++;
                    continue;
                }
                if (_profile.LiteralSyntax.ExcludeLiterals && ch == charDelim)
                {
                    state = LexState.Char;
                    bytePos += Utf8Len(ch); i++; col++;
                    continue;
                }

                // Identifier match at current position.
                var m = _idRegex.Match(text.ToString(), i);
                if (m.Success && m.Index == i)
                {
                    var ident = m.Value;
                    int startI = i;
                    int startCol = col;
                    int startByte = bytePos;

                    // advance
                    for (int k = 0; k < ident.Length; k++)
                    {
                        bytePos += Utf8Len(text[i]);
                        i++;
                        col++;
                    }

                    yield return new Token("Identifier", ident, line, startCol, col, startByte, bytePos);
                    continue;
                }

                // otherwise consume one char
                bytePos += Utf8Len(ch);
                i++;
                col++;
            }
            else if (state == LexState.LineComment)
            {
                if (isNewline)
                {
                    // newline ends line comment
                    state = LexState.Default;
                }
                // consume newline or normal char
                if (isCr && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    bytePos += Utf8Len(text[i]); i++; // CR
                    bytePos += Utf8Len(text[i]); i++; // LF
                    line++; col = 1;
                    continue;
                }
                if (isNewline)
                {
                    bytePos += Utf8Len(ch); i++;
                    line++; col = 1;
                    continue;
                }
                bytePos += Utf8Len(ch); i++; col++;
            }
            else if (state == LexState.BlockComment)
            {
                if (blockEnd != null && MatchAt(text, i, blockEnd))
                {
                    state = LexState.Default;
                    for (int k = 0; k < blockEnd.Length; k++) { bytePos += Utf8Len(text[i]); i++; col++; }
                    continue;
                }
                // consume
                if (isCr && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    bytePos += Utf8Len(text[i]); i++;
                    bytePos += Utf8Len(text[i]); i++;
                    line++; col = 1;
                    continue;
                }
                if (isNewline)
                {
                    bytePos += Utf8Len(ch); i++;
                    line++; col = 1;
                    continue;
                }
                bytePos += Utf8Len(ch); i++; col++;
            }
            else if (state == LexState.String)
            {
                // consume until closing quote, honoring escapes
                if (ch == escape)
                {
                    // consume escape + next char if any
                    bytePos += Utf8Len(ch); i++; col++;
                    if (i < text.Length)
                    {
                        bytePos += Utf8Len(text[i]); i++; col++;
                    }
                    continue;
                }
                if (ch == stringDelim)
                {
                    state = LexState.Default;
                    bytePos += Utf8Len(ch); i++; col++;
                    continue;
                }
                if (!_profile.LiteralSyntax.AllowMultilineStrings && isNewline)
                {
                    // tolerate: treat newline as terminating string
                    state = LexState.Default;
                }
                if (isCr && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    bytePos += Utf8Len(text[i]); i++;
                    bytePos += Utf8Len(text[i]); i++;
                    line++; col = 1;
                    continue;
                }
                if (isNewline)
                {
                    bytePos += Utf8Len(ch); i++;
                    line++; col = 1;
                    continue;
                }
                bytePos += Utf8Len(ch); i++; col++;
            }
            else // Char literal
            {
                if (ch == escape)
                {
                    bytePos += Utf8Len(ch); i++; col++;
                    if (i < text.Length) { bytePos += Utf8Len(text[i]); i++; col++; }
                    continue;
                }
                if (ch == charDelim)
                {
                    state = LexState.Default;
                    bytePos += Utf8Len(ch); i++; col++;
                    continue;
                }
                if (isCr && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    bytePos += Utf8Len(text[i]); i++;
                    bytePos += Utf8Len(text[i]); i++;
                    line++; col = 1;
                    continue;
                }
                if (isNewline)
                {
                    bytePos += Utf8Len(ch); i++;
                    line++; col = 1;
                    continue;
                }
                bytePos += Utf8Len(ch); i++; col++;
            }
        }

        // EOF: nothing to do; tolerant mode means unterminated constructs are accepted.
    }
}

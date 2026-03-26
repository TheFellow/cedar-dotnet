using System;
using System.Collections.Generic;

namespace Cedar.Schema.Internal;

internal static class SchemaTokenizer
{
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.Ordinal)
    {
        "true",
        "false",
        "if",
        "then",
        "else",
        "in",
        "like",
        "has",
        "is",
        "__cedar"
    };

    public static IReadOnlyList<SchemaToken> Tokenize(string source, string filename = "")
    {
        ArgumentNullException.ThrowIfNull(source);

        Tokenizer tokenizer = new(source, filename);
        return tokenizer.Tokenize();
    }

    internal static bool IsReservedKeyword(string value)
    {
        return ReservedKeywords.Contains(value);
    }

    private sealed class Tokenizer
    {
        private readonly string _source;
        private readonly string _filename;
        private int _index;
        private int _line;
        private int _column;

        public Tokenizer(string source, string filename)
        {
            _source = source;
            _filename = filename;
            _line = 1;
            _column = 1;
        }

        public IReadOnlyList<SchemaToken> Tokenize()
        {
            List<SchemaToken> tokens = [];

            while (true)
            {
                SkipWhitespaceAndComments();

                SchemaPosition position = CurrentPosition();
                if (IsAtEnd)
                {
                    tokens.Add(new SchemaToken(SchemaTokenType.EndOfFile, string.Empty, position));
                    return tokens;
                }

                char current = Peek();
                if (IsIdentifierStart(current))
                {
                    string text = ScanIdentifier();
                    SchemaTokenType type = ReservedKeywords.Contains(text) ? SchemaTokenType.ReservedKeyword : SchemaTokenType.Identifier;
                    tokens.Add(new SchemaToken(type, text, position));
                    continue;
                }

                if (current == '"')
                {
                    tokens.Add(new SchemaToken(SchemaTokenType.String, ScanString(position), position));
                    continue;
                }

                Advance();
                tokens.Add(current switch
                {
                    '@' => new SchemaToken(SchemaTokenType.At, "@", position),
                    '{' => new SchemaToken(SchemaTokenType.LeftBrace, "{", position),
                    '}' => new SchemaToken(SchemaTokenType.RightBrace, "}", position),
                    '[' => new SchemaToken(SchemaTokenType.LeftBracket, "[", position),
                    ']' => new SchemaToken(SchemaTokenType.RightBracket, "]", position),
                    '<' => new SchemaToken(SchemaTokenType.LeftAngle, "<", position),
                    '>' => new SchemaToken(SchemaTokenType.RightAngle, ">", position),
                    '(' => new SchemaToken(SchemaTokenType.LeftParen, "(", position),
                    ')' => new SchemaToken(SchemaTokenType.RightParen, ")", position),
                    ',' => new SchemaToken(SchemaTokenType.Comma, ",", position),
                    ';' => new SchemaToken(SchemaTokenType.Semicolon, ";", position),
                    '?' => new SchemaToken(SchemaTokenType.Question, "?", position),
                    '=' => new SchemaToken(SchemaTokenType.Equals, "=", position),
                    ':' when Peek() == ':' => ScanDoubleColon(position),
                    ':' => new SchemaToken(SchemaTokenType.Colon, ":", position),
                    _ => throw new SchemaParseException(position, $"unexpected character {current.ToString().Replace("'", "\\'", StringComparison.Ordinal)}")
                });
            }
        }

        private bool IsAtEnd => _index >= _source.Length;

        private SchemaPosition CurrentPosition()
        {
            return new SchemaPosition(_filename, _index, _line, _column);
        }

        private char Peek(int lookahead = 0)
        {
            int target = _index + lookahead;
            return target < _source.Length ? _source[target] : '\0';
        }

        private char Advance()
        {
            if (IsAtEnd)
            {
                throw new SchemaParseException(CurrentPosition(), "unexpected end of input");
            }

            char value = _source[_index++];
            if (value == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }

            return value;
        }

        private void SkipWhitespaceAndComments()
        {
            while (!IsAtEnd)
            {
                char current = Peek();
                if (current is ' ' or '\t' or '\r' or '\n')
                {
                    Advance();
                    continue;
                }

                if (current == '/' && Peek(1) == '/')
                {
                    Advance();
                    Advance();

                    while (!IsAtEnd && Peek() != '\n')
                    {
                        Advance();
                    }

                    continue;
                }

                if (current == '/' && Peek(1) == '*')
                {
                    SchemaPosition start = CurrentPosition();
                    Advance();
                    Advance();

                    while (!IsAtEnd)
                    {
                        if (Peek() == '*' && Peek(1) == '/')
                        {
                            Advance();
                            Advance();
                            break;
                        }

                        Advance();
                    }

                    if (IsAtEnd && (_source.Length < 2 || _source[^2] != '*' || _source[^1] != '/'))
                    {
                        throw new SchemaParseException(start, "unterminated block comment");
                    }

                    continue;
                }

                return;
            }
        }

        private string ScanIdentifier()
        {
            int start = _index;
            Advance();

            while (!IsAtEnd && IsIdentifierPart(Peek()))
            {
                Advance();
            }

            return _source[start.._index];
        }

        private string ScanString(SchemaPosition position)
        {
            int start = _index;
            Advance();

            while (!IsAtEnd)
            {
                char current = Peek();
                if (current == '"')
                {
                    Advance();
                    string raw = _source[start.._index];
                    try
                    {
                        return SchemaStringHelper.Unquote(raw);
                    }
                    catch (FormatException ex)
                    {
                        throw new SchemaParseException(position, $"invalid string escape: {ex.Message}");
                    }
                }

                if (current == '\n')
                {
                    throw new SchemaParseException(position, "unterminated string literal");
                }

                if (current == '\\')
                {
                    Advance();
                    if (IsAtEnd)
                    {
                        throw new SchemaParseException(position, "unterminated string literal");
                    }
                }

                Advance();
            }

            throw new SchemaParseException(position, "unterminated string literal");
        }

        private SchemaToken ScanDoubleColon(SchemaPosition position)
        {
            Advance();
            return new SchemaToken(SchemaTokenType.DoubleColon, "::", position);
        }

        private static bool IsIdentifierStart(char value)
        {
            return value == '_' || value is >= 'A' and <= 'Z' || value is >= 'a' and <= 'z';
        }

        private static bool IsIdentifierPart(char value)
        {
            return IsIdentifierStart(value) || value is >= '0' and <= '9';
        }
    }
}

internal enum SchemaTokenType
{
    EndOfFile,
    Identifier,
    String,
    At,
    LeftBrace,
    RightBrace,
    LeftBracket,
    RightBracket,
    LeftAngle,
    RightAngle,
    LeftParen,
    RightParen,
    Comma,
    Semicolon,
    Colon,
    DoubleColon,
    Question,
    Equals,
    ReservedKeyword
}

internal readonly record struct SchemaToken(SchemaTokenType Type, string Text, SchemaPosition Position);

internal readonly record struct SchemaPosition(string Filename, int Offset, int Line, int Column);

internal sealed class SchemaParseException : Exception
{
    public SchemaParseException(SchemaPosition position, string message)
        : base($"{FormatFilename(position.Filename)}:{position.Line}:{position.Column}: {message}")
    {
        Position = position;
    }

    public SchemaPosition Position { get; }

    private static string FormatFilename(string filename)
    {
        return string.IsNullOrEmpty(filename) ? "<input>" : filename;
    }
}

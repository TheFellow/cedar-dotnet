using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Cedar.Core.Internal.Parser;

internal ref struct CedarTokenizer
{
    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.Ordinal)
    {
        ["permit"] = TokenType.Permit,
        ["forbid"] = TokenType.Forbid,
        ["when"] = TokenType.When,
        ["unless"] = TokenType.Unless,
        ["true"] = TokenType.True,
        ["false"] = TokenType.False,
        ["if"] = TokenType.If,
        ["then"] = TokenType.Then,
        ["else"] = TokenType.Else,
        ["in"] = TokenType.In,
        ["like"] = TokenType.Like,
        ["has"] = TokenType.Has,
        ["is"] = TokenType.Is
    };

    private readonly ReadOnlySpan<byte> _input;
    private readonly string _filename;
    private int _index;
    private int _line;
    private int _column;

    private CedarTokenizer(ReadOnlySpan<byte> input, string filename)
    {
        _input = input;
        _filename = filename;
        _index = 0;
        _line = 1;
        _column = 1;
    }

    public static ImmutableArray<Token> Tokenize(ReadOnlySpan<byte> input, string filename = "")
    {
        CedarTokenizer tokenizer = new(input, filename);
        ImmutableArray<Token>.Builder tokens = ImmutableArray.CreateBuilder<Token>();

        while (true)
        {
            tokenizer.SkipTrivia();
            Position position = tokenizer.CurrentPosition();

            if (tokenizer.IsAtEnd)
            {
                tokens.Add(new Token(TokenType.EOF, string.Empty, position));
                break;
            }

            byte current = tokenizer.PeekByte();

            if (IsIdentStart(current))
            {
                tokens.Add(tokenizer.ScanIdentifier(position));
                continue;
            }

            if (IsDigit(current))
            {
                tokens.Add(tokenizer.ScanInteger(position));
                continue;
            }

            if (current == (byte)'"')
            {
                tokens.Add(tokenizer.ScanString(position));
                continue;
            }

            tokens.Add(tokenizer.ScanOperatorOrAnnotation(position));
        }

        return tokens.ToImmutable();
    }

    private bool IsAtEnd => _index >= _input.Length;

    private Position CurrentPosition()
    {
        return new Position(_filename, _index, _line, _column);
    }

    private byte PeekByte(int lookahead = 0)
    {
        int target = _index + lookahead;
        return target < _input.Length ? _input[target] : (byte)0;
    }

    private byte ConsumeByte()
    {
        if (IsAtEnd)
        {
            throw Error("Unexpected end of input.");
        }

        byte value = _input[_index++];
        if (value == (byte)'\n')
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

    private void ConsumeRune()
    {
        if (IsAtEnd)
        {
            throw Error("Unexpected end of input.");
        }

        ReadOnlySpan<byte> slice = _input[_index..];
        OperationStatus status = Rune.DecodeFromUtf8(slice, out Rune rune, out int bytesConsumed);
        if (status != OperationStatus.Done)
        {
            throw Error("Invalid UTF-8 sequence.");
        }

        _index += bytesConsumed;

        if (rune.Value == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
    }

    private void SkipTrivia()
    {
        while (!IsAtEnd)
        {
            byte current = PeekByte();

            if (current is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                ConsumeByte();
                continue;
            }

            if (current == (byte)'/' && PeekByte(1) == (byte)'/')
            {
                ConsumeByte();
                ConsumeByte();

                while (!IsAtEnd && PeekByte() != (byte)'\n')
                {
                    ConsumeByte();
                }

                continue;
            }

            if (current == (byte)'/' && PeekByte(1) == (byte)'*')
            {
                Position start = CurrentPosition();
                bool terminated = false;
                ConsumeByte();
                ConsumeByte();

                while (!IsAtEnd)
                {
                    if (PeekByte() == (byte)'*' && PeekByte(1) == (byte)'/')
                    {
                        ConsumeByte();
                        ConsumeByte();
                        terminated = true;
                        break;
                    }

                    if (PeekByte() < 0x80)
                    {
                        ConsumeByte();
                    }
                    else
                    {
                        ConsumeRune();
                    }
                }

                if (!terminated)
                {
                    throw new ParseException(start, "Comment not terminated.");
                }

                continue;
            }

            break;
        }
    }

    private Token ScanIdentifier(Position position)
    {
        int start = _index;
        ConsumeByte();

        while (!IsAtEnd)
        {
            byte next = PeekByte();
            if (!IsIdentPart(next))
            {
                break;
            }

            ConsumeByte();
        }

        string text = Encoding.UTF8.GetString(_input.Slice(start, _index - start));
        TokenType type = Keywords.TryGetValue(text, out TokenType keywordType) ? keywordType : TokenType.Ident;
        return new Token(type, text, position);
    }

    private Token ScanInteger(Position position)
    {
        int start = _index;
        ConsumeByte();

        while (!IsAtEnd && IsDigit(PeekByte()))
        {
            ConsumeByte();
        }

        int length = _index - start;
        if (length > 1 && _input[start] == (byte)'0')
        {
            throw new ParseException(position, "Invalid integer literal with leading zero.");
        }

        string text = Encoding.UTF8.GetString(_input.Slice(start, length));
        return new Token(TokenType.Int, text, position);
    }

    private Token ScanString(Position position)
    {
        int start = _index;
        ConsumeByte();

        while (!IsAtEnd)
        {
            byte current = PeekByte();
            if (current == (byte)'"')
            {
                ConsumeByte();
                string text = Encoding.UTF8.GetString(_input.Slice(start, _index - start));
                return new Token(TokenType.String, text, position);
            }

            if (current is (byte)'\n' or (byte)'\r')
            {
                throw new ParseException(position, "String literal not terminated.");
            }

            if (current == (byte)'\\')
            {
                ConsumeByte();
                ScanEscape(position, allowEscapedStar: true);
                continue;
            }

            if (current < 0x80)
            {
                ConsumeByte();
            }
            else
            {
                ConsumeRune();
            }
        }

        throw new ParseException(position, "String literal not terminated.");
    }

    private void ScanEscape(Position position, bool allowEscapedStar)
    {
        if (IsAtEnd)
        {
            throw new ParseException(position, "Invalid escape sequence.");
        }

        byte escape = ConsumeByte();
        switch (escape)
        {
            case (byte)'n':
            case (byte)'t':
            case (byte)'r':
            case (byte)'\\':
            case (byte)'"':
            case (byte)'\'':
            case (byte)'0':
                return;
            case (byte)'*':
                if (!allowEscapedStar)
                {
                    throw new ParseException(position, "Invalid escape sequence.");
                }

                return;
            case (byte)'u':
                ScanUnicodeEscape(position);
                return;
            default:
                throw new ParseException(position, "Invalid escape sequence.");
        }
    }

    private void ScanUnicodeEscape(Position position)
    {
        if (IsAtEnd || PeekByte() != (byte)'{')
        {
            throw new ParseException(position, "Invalid unicode escape sequence.");
        }

        ConsumeByte();

        int digits = 0;
        int codePoint = 0;

        while (!IsAtEnd && PeekByte() != (byte)'}')
        {
            int hex = HexValue(PeekByte());
            if (hex < 0)
            {
                throw new ParseException(position, "Invalid unicode escape sequence.");
            }

            digits++;
            if (digits > 6)
            {
                throw new ParseException(position, "Invalid unicode escape sequence.");
            }

            codePoint = checked((codePoint * 16) + hex);
            ConsumeByte();
        }

        if (digits == 0 || IsAtEnd || PeekByte() != (byte)'}')
        {
            throw new ParseException(position, "Invalid unicode escape sequence.");
        }

        ConsumeByte();

        if (codePoint > 0x10FFFF || codePoint is >= 0xD800 and <= 0xDFFF || !Rune.IsValid(codePoint))
        {
            throw new ParseException(position, "Invalid unicode escape sequence.");
        }
    }

    private Token ScanOperatorOrAnnotation(Position position)
    {
        byte current = PeekByte();

        switch (current)
        {
            case (byte)'@':
                return ScanAtOrAnnotation(position);
            case (byte)'(':
                ConsumeByte();
                return new Token(TokenType.LParen, "(", position);
            case (byte)')':
                ConsumeByte();
                return new Token(TokenType.RParen, ")", position);
            case (byte)'{':
                ConsumeByte();
                return new Token(TokenType.LBrace, "{", position);
            case (byte)'}':
                ConsumeByte();
                return new Token(TokenType.RBrace, "}", position);
            case (byte)'[':
                ConsumeByte();
                return new Token(TokenType.LBracket, "[", position);
            case (byte)']':
                ConsumeByte();
                return new Token(TokenType.RBracket, "]", position);
            case (byte)',':
                ConsumeByte();
                return new Token(TokenType.Comma, ",", position);
            case (byte)';':
                ConsumeByte();
                return new Token(TokenType.Semicolon, ";", position);
            case (byte)':':
                ConsumeByte();
                if (!IsAtEnd && PeekByte() == (byte)':')
                {
                    ConsumeByte();
                    return new Token(TokenType.ColonColon, "::", position);
                }

                return new Token(TokenType.Colon, ":", position);
            case (byte)'.':
                ConsumeByte();
                return new Token(TokenType.Dot, ".", position);
            case (byte)'+':
                ConsumeByte();
                return new Token(TokenType.Plus, "+", position);
            case (byte)'-':
                ConsumeByte();
                return new Token(TokenType.Dash, "-", position);
            case (byte)'*':
                ConsumeByte();
                return new Token(TokenType.Star, "*", position);
            case (byte)'!':
                ConsumeByte();
                if (!IsAtEnd && PeekByte() == (byte)'=')
                {
                    ConsumeByte();
                    return new Token(TokenType.BangEq, "!=", position);
                }

                return new Token(TokenType.Bang, "!", position);
            case (byte)'=':
                ConsumeByte();
                if (!IsAtEnd && PeekByte() == (byte)'=')
                {
                    ConsumeByte();
                    return new Token(TokenType.EqEq, "==", position);
                }

                return new Token(TokenType.Eq, "=", position);
            case (byte)'<':
                ConsumeByte();
                if (!IsAtEnd && PeekByte() == (byte)'=')
                {
                    ConsumeByte();
                    return new Token(TokenType.LtEq, "<=", position);
                }

                return new Token(TokenType.Lt, "<", position);
            case (byte)'>':
                ConsumeByte();
                if (!IsAtEnd && PeekByte() == (byte)'=')
                {
                    ConsumeByte();
                    return new Token(TokenType.GtEq, ">=", position);
                }

                return new Token(TokenType.Gt, ">", position);
            case (byte)'&':
                ConsumeByte();
                if (!IsAtEnd && PeekByte() == (byte)'&')
                {
                    ConsumeByte();
                    return new Token(TokenType.AmpAmp, "&&", position);
                }

                return new Token(TokenType.Amp, "&", position);
            case (byte)'|':
                ConsumeByte();
                if (!IsAtEnd && PeekByte() == (byte)'|')
                {
                    ConsumeByte();
                    return new Token(TokenType.PipePipe, "||", position);
                }

                return new Token(TokenType.Pipe, "|", position);
            default:
                throw new ParseException(position, $"Unexpected character '{GetPrintableRune(current)}'.");
        }
    }

    private Token ScanAtOrAnnotation(Position position)
    {
        TokenizerState snapshot = CaptureState();
        ConsumeByte();

        SkipWhitespaceOnly();
        if (IsAtEnd || !IsIdentStart(PeekByte()))
        {
            RestoreState(snapshot);
            ConsumeByte();
            return new Token(TokenType.At, "@", position);
        }

        ScanIdentifier(CurrentPosition());
        int annotationTextEnd = _index;

        SkipWhitespaceOnly();
        if (IsAtEnd || PeekByte() != (byte)'(')
        {
            string bareText = Encoding.UTF8.GetString(_input.Slice(snapshot.Index, annotationTextEnd - snapshot.Index));
            return new Token(TokenType.Annotation, bareText, position);
        }

        ConsumeByte();
        SkipWhitespaceOnly();

        if (IsAtEnd || PeekByte() != (byte)'"')
        {
            throw new ParseException(position, "Invalid annotation literal.");
        }

        ScanString(position);

        SkipWhitespaceOnly();
        if (IsAtEnd || PeekByte() != (byte)')')
        {
            throw new ParseException(position, "Invalid annotation literal.");
        }

        ConsumeByte();

        string text = Encoding.UTF8.GetString(_input.Slice(snapshot.Index, _index - snapshot.Index));
        return new Token(TokenType.Annotation, text, position);
    }

    private void SkipWhitespaceOnly()
    {
        while (!IsAtEnd && PeekByte() is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            ConsumeByte();
        }
    }

    private ParseException Error(string message)
    {
        return new ParseException(CurrentPosition(), message);
    }

    private TokenizerState CaptureState()
    {
        return new TokenizerState(_index, _line, _column);
    }

    private void RestoreState(TokenizerState state)
    {
        _index = state.Index;
        _line = state.Line;
        _column = state.Column;
    }

    private static bool IsIdentStart(byte value)
    {
        return value == (byte)'_' || value is >= (byte)'A' and <= (byte)'Z' || value is >= (byte)'a' and <= (byte)'z';
    }

    private static bool IsIdentPart(byte value)
    {
        return IsIdentStart(value) || IsDigit(value);
    }

    private static bool IsDigit(byte value)
    {
        return value is >= (byte)'0' and <= (byte)'9';
    }

    private static int HexValue(byte value)
    {
        if (value is >= (byte)'0' and <= (byte)'9')
        {
            return value - (byte)'0';
        }

        if (value is >= (byte)'a' and <= (byte)'f')
        {
            return value - (byte)'a' + 10;
        }

        if (value is >= (byte)'A' and <= (byte)'F')
        {
            return value - (byte)'A' + 10;
        }

        return -1;
    }

    private static string GetPrintableRune(byte value)
    {
        if (value is >= 32 and < 127)
        {
            return ((char)value).ToString();
        }

        return $"\\x{value:x2}";
    }

    private readonly record struct TokenizerState(int Index, int Line, int Column);
}

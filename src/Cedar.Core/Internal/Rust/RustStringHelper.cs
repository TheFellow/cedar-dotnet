using System;
using System.Text;

namespace Cedar.Core.Internal.Rust;

internal static class RustStringHelper
{
    public static string Unquote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            throw new FormatException("String literal must be surrounded by double quotes.");
        }

        return Unescape(value.AsSpan(1, value.Length - 2), allowEscapedStar: false);
    }

    internal static string Unescape(ReadOnlySpan<char> value, bool allowEscapedStar)
    {
        return UnescapeCore(value, allowEscapedStar, stopAtWildcard: false, out _);
    }

    internal static string UnescapeUntilWildcard(ReadOnlySpan<char> value, ref int index)
    {
        int consumed;
        string result = UnescapeCore(value[index..], allowEscapedStar: true, stopAtWildcard: true, out consumed);
        index += consumed;
        return result;
    }

    private static string UnescapeCore(ReadOnlySpan<char> value, bool allowEscapedStar, bool stopAtWildcard, out int consumed)
    {
        StringBuilder builder = new();
        int i = 0;

        while (i < value.Length)
        {
            char current = value[i];
            if (stopAtWildcard && current == '*')
            {
                break;
            }

            if (current != '\\')
            {
                builder.Append(current);
                i++;
                continue;
            }

            i++;
            if (i >= value.Length)
            {
                throw new FormatException("Invalid escape sequence.");
            }

            char escape = value[i++];
            switch (escape)
            {
                case 'n':
                    builder.Append('\n');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case '\\':
                    builder.Append('\\');
                    break;
                case '"':
                    builder.Append('"');
                    break;
                case '\'':
                    builder.Append('\'');
                    break;
                case '0':
                    builder.Append('\0');
                    break;
                case '*':
                    if (!allowEscapedStar)
                    {
                        throw new FormatException("Invalid escape sequence.");
                    }

                    builder.Append('*');
                    break;
                case 'u':
                    builder.Append(ParseUnicodeEscape(value, ref i));
                    break;
                default:
                    throw new FormatException("Invalid escape sequence.");
            }
        }

        consumed = i;
        return builder.ToString();
    }

    private static string ParseUnicodeEscape(ReadOnlySpan<char> value, ref int index)
    {
        if (index >= value.Length || value[index] != '{')
        {
            throw new FormatException("Invalid unicode escape sequence.");
        }

        index++;

        int digits = 0;
        int codePoint = 0;

        while (index < value.Length && value[index] != '}')
        {
            int hex = HexValue(value[index]);
            if (hex < 0)
            {
                throw new FormatException("Invalid unicode escape sequence.");
            }

            digits++;
            if (digits > 6)
            {
                throw new FormatException("Invalid unicode escape sequence.");
            }

            codePoint = checked((codePoint * 16) + hex);
            index++;
        }

        if (index >= value.Length || value[index] != '}' || digits is 0)
        {
            throw new FormatException("Invalid unicode escape sequence.");
        }

        index++;

        if (codePoint > 0x10FFFF || codePoint is >= 0xD800 and <= 0xDFFF || !Rune.IsValid(codePoint))
        {
            throw new FormatException("Invalid unicode escape sequence.");
        }

        if (!Rune.TryCreate(codePoint, out Rune rune))
        {
            throw new FormatException("Invalid unicode escape sequence.");
        }

        return rune.ToString();
    }

    private static int HexValue(char value)
    {
        if (value is >= '0' and <= '9')
        {
            return value - '0';
        }

        if (value is >= 'a' and <= 'f')
        {
            return value - 'a' + 10;
        }

        if (value is >= 'A' and <= 'F')
        {
            return value - 'A' + 10;
        }

        return -1;
    }
}

using System;
using System.Text;

namespace Cedar.Schema.Internal;

internal static class SchemaStringHelper
{
    public static string Unquote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            throw new FormatException("string literal must be surrounded by double quotes");
        }

        return Unescape(value.AsSpan(1, value.Length - 2));
    }

    private static string Unescape(ReadOnlySpan<char> value)
    {
        StringBuilder builder = new();

        for (int index = 0; index < value.Length;)
        {
            char current = value[index++];
            if (current != '\\')
            {
                builder.Append(current);
                continue;
            }

            if (index >= value.Length)
            {
                throw new FormatException("invalid escape sequence");
            }

            char escape = value[index++];
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
                case 'u':
                    builder.Append(ParseUnicodeEscape(value, ref index));
                    break;
                default:
                    throw new FormatException("invalid escape sequence");
            }
        }

        return builder.ToString();
    }

    private static string ParseUnicodeEscape(ReadOnlySpan<char> value, ref int index)
    {
        if (index >= value.Length || value[index] != '{')
        {
            throw new FormatException("invalid unicode escape sequence");
        }

        index++;

        int digits = 0;
        int codePoint = 0;

        while (index < value.Length && value[index] != '}')
        {
            int hex = HexValue(value[index]);
            if (hex < 0)
            {
                throw new FormatException("invalid unicode escape sequence");
            }

            digits++;
            if (digits > 6)
            {
                throw new FormatException("invalid unicode escape sequence");
            }

            codePoint = checked((codePoint * 16) + hex);
            index++;
        }

        if (digits == 0 || index >= value.Length || value[index] != '}')
        {
            throw new FormatException("invalid unicode escape sequence");
        }

        index++;

        if (codePoint > 0x10FFFF || codePoint is >= 0xD800 and <= 0xDFFF || !Rune.IsValid(codePoint))
        {
            throw new FormatException("invalid unicode escape sequence");
        }

        return Rune.GetRuneAt(char.ConvertFromUtf32(codePoint), 0).ToString();
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

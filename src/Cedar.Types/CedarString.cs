using System;
using System.Globalization;
using System.Text;
using Cedar.Core.Internal.Rust;

namespace Cedar.Types;

public sealed record CedarString(string Value) : CedarValue
{
    public override string MarshalCedar()
    {
        return "\"" + Escape(Value) + "\"";
    }

    public override int GetHashCode()
    {
        return CedarHash.ForString(nameof(CedarString), Value);
    }

    internal static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        StringBuilder builder = new();
        bool isFirst = true;

        foreach (Rune rune in value.EnumerateRunes())
        {
            builder.Append(EscapeRune(rune, isFirst));
            isFirst = false;
        }

        return builder.ToString();
    }

    internal static string EscapeCharAll(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        StringBuilder builder = new();

        foreach (Rune rune in value.EnumerateRunes())
        {
            builder.Append(EscapeRune(rune, escapeGraphemeExtend: true));
        }

        return builder.ToString();
    }

    private static string EscapeRune(Rune rune, bool escapeGraphemeExtend)
    {
        return rune.Value switch
        {
            0x00 => @"\0",
            0x09 => @"\t",
            0x0D => @"\r",
            0x0A => @"\n",
            0x5C => @"\\",
            0x22 => "\\\"",
            0x27 => @"\'",
            _ when escapeGraphemeExtend && RustPrintable.IsGraphemeExtended(rune.Value) => UnicodeEscape(rune),
            _ when !RustPrintable.IsPrintable(rune.Value) => UnicodeEscape(rune),
            _ => rune.ToString()
        };
    }

    private static string UnicodeEscape(Rune rune)
    {
        return @"\u{" + rune.Value.ToString("x", CultureInfo.InvariantCulture) + "}";
    }
}

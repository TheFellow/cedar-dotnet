using System.Globalization;
using System.Text;

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
        StringBuilder builder = new();
        bool isFirst = true;

        foreach (Rune rune in value.EnumerateRunes())
        {
            builder.Append(EscapeRune(rune, isFirst));
            isFirst = false;
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
            _ when escapeGraphemeExtend && IsGraphemeExtend(rune) => UnicodeEscape(rune),
            _ when NeedsUnicodeEscape(rune) => UnicodeEscape(rune),
            _ => rune.ToString()
        };
    }

    private static bool IsGraphemeExtend(Rune rune)
    {
        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark;
    }

    private static bool NeedsUnicodeEscape(Rune rune)
    {
        UnicodeCategory category = Rune.GetUnicodeCategory(rune);

        return category is UnicodeCategory.Control
            or UnicodeCategory.Format
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator
            or UnicodeCategory.OtherNotAssigned
            or UnicodeCategory.PrivateUse
            or UnicodeCategory.Surrogate
            || rune.Value == 0x00A0;
    }

    private static string UnicodeEscape(Rune rune)
    {
        return @"\u{" + rune.Value.ToString("x", CultureInfo.InvariantCulture) + "}";
    }
}

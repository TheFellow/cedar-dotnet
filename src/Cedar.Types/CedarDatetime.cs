using System;
using System.Globalization;
using Cedar.Core.Internal.Consts;
using Cedar.Internal.DateTimeSupport;

namespace Cedar.Types;

public sealed record CedarDatetime(long Value) : CedarValue
{
    public static CedarDatetime Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
        {
            throw new FormatException("Datetime values must not be empty.");
        }

        int cursor = 0;
        int yearSign = 1;
        int yearDigits = 4;

        if (value[cursor] is '+' or '-')
        {
            yearDigits = 9;
            if (value[cursor] == '-')
            {
                yearSign = -1;
            }

            cursor++;
        }

        int yearMagnitude = ParseDigits(value, ref cursor, yearDigits, "year");
        int year = checked(yearSign * yearMagnitude);

        ExpectCharacter(value, ref cursor, '-');
        int month = ParseDigits(value, ref cursor, 2, "month");
        ExpectCharacter(value, ref cursor, '-');
        int day = ParseDigits(value, ref cursor, 2, "day");

        ValidateDate(year, month, day);

        if (cursor == value.Length)
        {
            return CreateFromParts(year, month, day, 0, 0, 0, 0, 0);
        }

        ExpectCharacter(value, ref cursor, 'T');
        int hour = ParseDigits(value, ref cursor, 2, "hour");
        ExpectCharacter(value, ref cursor, ':');
        int minute = ParseDigits(value, ref cursor, 2, "minute");
        ExpectCharacter(value, ref cursor, ':');
        int second = ParseDigits(value, ref cursor, 2, "second");

        if (hour > 23)
        {
            throw new FormatException("Hour is greater than 23.");
        }

        if (minute > 59)
        {
            throw new FormatException("Minute is greater than 59.");
        }

        if (second > 59)
        {
            throw new FormatException("Second is greater than 59.");
        }

        int milliseconds = 0;

        if (cursor < value.Length && value[cursor] == '.')
        {
            cursor++;
            milliseconds = ParseDigits(value, ref cursor, 3, "millisecond");
        }

        if (cursor == value.Length)
        {
            throw new FormatException("Datetime values must include a time zone designator.");
        }

        long offsetMilliseconds = value[cursor] switch
        {
            'Z' => ReadUtcTrailer(value, ref cursor),
            '+' => ReadOffset(value, ref cursor, 1),
            '-' => ReadOffset(value, ref cursor, -1),
            _ => throw new FormatException("Invalid time zone designator.")
        };

        if (cursor != value.Length)
        {
            throw new FormatException("Unexpected trailing characters after the time zone designator.");
        }

        return CreateFromParts(year, month, day, hour, minute, second, milliseconds, offsetMilliseconds);
    }

    public static CedarDatetime FromDateTimeOffset(DateTimeOffset value)
    {
        return new CedarDatetime(value.ToUnixTimeMilliseconds());
    }

    public DateTimeOffset ToDateTimeOffset()
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(Value).ToUniversalTime();
    }

    private GregorianDateTimeParts GetParts()
    {
        return GregorianDateTime.FromUnixMilliseconds(Value);
    }

    public override string MarshalCedar()
    {
        GregorianDateTimeParts parts = GetParts();
        string year = FormatYear(parts.Year);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"datetime(\"{year}-{parts.Month:D2}-{parts.Day:D2}T{parts.Hour:D2}:{parts.Minute:D2}:{parts.Second:D2}.{parts.Millisecond:D3}Z\")");
    }

    public override int GetHashCode()
    {
        return CedarHash.ForInt64(nameof(CedarDatetime), Value);
    }

    private static CedarDatetime CreateFromParts(int year, int month, int day, int hour, int minute, int second, int millisecond, long offsetMilliseconds)
    {
        try
        {
            long milliseconds = GregorianDateTime.ToUnixMilliseconds(year, month, day, hour, minute, second, millisecond);

            // Use 128-bit arithmetic to avoid overflow on boundary values.
            // Go uses int64 throughout and relies on its own overflow checks;
            // we mirror that by computing in wider precision then range-checking.
            Int128 wideResult = (Int128)milliseconds - (Int128)offsetMilliseconds;
            if (wideResult < long.MinValue || wideResult > long.MaxValue)
            {
                throw new FormatException("Timestamp is out of range.");
            }

            return new CedarDatetime((long)wideResult);
        }
        catch (OverflowException)
        {
            throw new FormatException("Timestamp is out of range.");
        }
    }

    private static string FormatYear(int year)
    {
        if (year is >= 0 and <= 9999)
        {
            return year.ToString("D4", CultureInfo.InvariantCulture);
        }

        long absoluteYear = year < 0 ? -(long)year : year;
        string sign = year < 0 ? "-" : "+";
        return sign + absoluteYear.ToString("D9", CultureInfo.InvariantCulture);
    }

    private static void ExpectCharacter(string value, ref int cursor, char expected)
    {
        if (value.Length <= cursor || value[cursor] != expected)
        {
            throw new FormatException($"Expected '{expected}' at position {cursor}.");
        }

        cursor++;
    }

    private static int ParseDigits(string value, ref int cursor, int length, string component)
    {
        if (value.Length < cursor + length)
        {
            throw new FormatException($"Invalid {component} component.");
        }

        int parsed = 0;
        for (int index = 0; index < length; index++)
        {
            char character = value[cursor + index];
            if (!char.IsAsciiDigit(character))
            {
                throw new FormatException($"Invalid {component} component.");
            }

            parsed = checked(parsed * 10 + (character - '0'));
        }

        cursor += length;
        return parsed;
    }

    private static long ReadUtcTrailer(string value, ref int cursor)
    {
        cursor++;
        return 0;
    }

    private static long ReadOffset(string value, ref int cursor, int sign)
    {
        cursor++;

        if (value.Length != cursor + 4)
        {
            throw new FormatException("Time zone offsets must use the +hhmm or -hhmm form.");
        }

        int hours = ParseDigits(value, ref cursor, 2, "time zone hour");
        int minutes = ParseDigits(value, ref cursor, 2, "time zone minute");

        if (hours > 23)
        {
            throw new FormatException("Time zone offset hours are greater than 23.");
        }

        if (minutes > 59)
        {
            throw new FormatException("Time zone offset minutes are greater than 59.");
        }

        long magnitude = (hours * CedarConsts.MillisPerHour) + (minutes * CedarConsts.MillisPerMinute);
        return sign * magnitude;
    }

    private static void ValidateDate(int year, int month, int day)
    {
        if (month is < 1 or > 12)
        {
            throw new FormatException("Month is greater than 12.");
        }

        if (day is < 1 or > 31)
        {
            throw new FormatException("Day is greater than 31.");
        }

        int daysInMonth = GregorianDateTime.DaysInMonth(year, month);
        if (day > daysInMonth)
        {
            throw new FormatException("Date is invalid.");
        }
    }
}

using System;
using System.Globalization;

namespace Cedar.Types;

public sealed record CedarDatetime(long Value) : CedarValue
{
    public static CedarDatetime Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length < 10)
        {
            throw new FormatException("Datetime values must contain at least a full date.");
        }

        int year = ParseDigits(value, 0, 4, "year");
        ExpectCharacter(value, 4, '-');
        int month = ParseDigits(value, 5, 2, "month");
        ExpectCharacter(value, 7, '-');
        int day = ParseDigits(value, 8, 2, "day");

        ValidateDate(year, month, day);

        if (value.Length == 10)
        {
            return new CedarDatetime(new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
        }

        if (value.Length < 20)
        {
            throw new FormatException("Datetime values must contain a full time component.");
        }

        ExpectCharacter(value, 10, 'T');
        int hour = ParseDigits(value, 11, 2, "hour");
        ExpectCharacter(value, 13, ':');
        int minute = ParseDigits(value, 14, 2, "minute");
        ExpectCharacter(value, 16, ':');
        int second = ParseDigits(value, 17, 2, "second");

        if (hour > 23)
        {
            throw new FormatException("Hour is out of range.");
        }

        if (minute > 59)
        {
            throw new FormatException("Minute is out of range.");
        }

        if (second > 59)
        {
            throw new FormatException("Second is out of range.");
        }

        int milliseconds = 0;
        int trailerOffset = 19;

        if (value[19] == '.')
        {
            if (value.Length < 23)
            {
                throw new FormatException("Millisecond component is invalid.");
            }

            milliseconds = ParseDigits(value, 20, 3, "millisecond");
            trailerOffset = 23;
        }

        if (value.Length == trailerOffset)
        {
            throw new FormatException("Datetime values must include a time zone designator.");
        }

        TimeSpan offset = value[trailerOffset] switch
        {
            'Z' => ReadUtcTrailer(value, trailerOffset),
            '+' => ReadOffset(value, trailerOffset, 1),
            '-' => ReadOffset(value, trailerOffset, -1),
            _ => throw new FormatException("Invalid time zone designator.")
        };

        DateTimeOffset timestamp = new(year, month, day, hour, minute, second, milliseconds, TimeSpan.Zero);
        return new CedarDatetime(timestamp.ToUnixTimeMilliseconds() - (long)offset.TotalMilliseconds);
    }

    public static CedarDatetime FromDateTimeOffset(DateTimeOffset value)
    {
        return new CedarDatetime(value.ToUnixTimeMilliseconds());
    }

    public DateTimeOffset ToDateTimeOffset()
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(Value).ToUniversalTime();
    }

    public override string MarshalCedar()
    {
        return "datetime(\"" + ToDateTimeOffset().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture) + "\")";
    }

    public override int GetHashCode()
    {
        return CedarHash.ForInt64(nameof(CedarDatetime), Value);
    }

    private static void ExpectCharacter(string value, int index, char expected)
    {
        if (value.Length <= index || value[index] != expected)
        {
            throw new FormatException($"Expected '{expected}' at position {index}.");
        }
    }

    private static int ParseDigits(string value, int offset, int length, string component)
    {
        if (value.Length < offset + length)
        {
            throw new FormatException($"Invalid {component} component.");
        }

        ReadOnlySpan<char> span = value.AsSpan(offset, length);
        foreach (char character in span)
        {
            if (!char.IsAsciiDigit(character))
            {
                throw new FormatException($"Invalid {component} component.");
            }
        }

        return int.Parse(span, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private static TimeSpan ReadUtcTrailer(string value, int offset)
    {
        if (value.Length != offset + 1)
        {
            throw new FormatException("Unexpected trailing characters after the time zone designator.");
        }

        return TimeSpan.Zero;
    }

    private static TimeSpan ReadOffset(string value, int offset, int sign)
    {
        if (value.Length != offset + 5)
        {
            throw new FormatException("Time zone offsets must use the +hhmm or -hhmm form.");
        }

        int hours = ParseDigits(value, offset + 1, 2, "time zone hour");
        int minutes = ParseDigits(value, offset + 3, 2, "time zone minute");

        if (hours > 23)
        {
            throw new FormatException("Time zone offset hours are out of range.");
        }

        if (minutes > 59)
        {
            throw new FormatException("Time zone offset minutes are out of range.");
        }

        TimeSpan magnitude = new(hours, minutes, 0);
        return TimeSpan.FromTicks(sign * magnitude.Ticks);
    }

    private static void ValidateDate(int year, int month, int day)
    {
        if (month is < 1 or > 12)
        {
            throw new FormatException("Month is out of range.");
        }

        int daysInMonth = DateTime.DaysInMonth(year, month);
        if (day is < 1 or > 31)
        {
            throw new FormatException("Day is out of range.");
        }

        if (day > daysInMonth)
        {
            throw new FormatException("Date is invalid.");
        }
    }
}

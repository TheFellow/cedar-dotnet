using System;
using System.Globalization;
using Cedar.Core.Internal.Consts;
using Cedar.Internal.DateTimeSupport;

namespace Cedar.Types;

public sealed record CedarDatetime(long Value) : CedarValue
{
    private static readonly GregorianDateTimeParts MinSupportedInstant = new(-292275055, 5, 17, 16, 47, 4, 192);
    private static readonly GregorianDateTimeParts MaxSupportedInstant = new(292278994, 8, 17, 7, 12, 55, 807);

    public static CedarDatetime Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
        {
            throw UnexpectedEof();
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
        else if (!char.IsAsciiDigit(value[cursor]))
        {
            throw InvalidComponent("year");
        }

        int yearMagnitude = ParseDigits(value, ref cursor, yearDigits, yearDigits == 4 ? 9999 : 999_999_999, "year");
        int year = checked(yearSign * yearMagnitude);

        ExpectCharacter(value, ref cursor, '-');
        int month = ParseDigits(value, ref cursor, 2, 12, "month");
        ExpectCharacter(value, ref cursor, '-');
        int day = ParseDigits(value, ref cursor, 2, 31, "day");

        ValidateDate(year, month, day);

        if (cursor == value.Length)
        {
            return CreateFromParts(year, month, day, 0, 0, 0, 0, 0);
        }

        ExpectCharacter(value, ref cursor, 'T');
        int hour = ParseDigits(value, ref cursor, 2, 23, "hour");
        ExpectCharacter(value, ref cursor, ':');
        int minute = ParseDigits(value, ref cursor, 2, 59, "minute");
        ExpectCharacter(value, ref cursor, ':');
        int second = ParseDigits(value, ref cursor, 2, 59, "second");

        int milliseconds = 0;

        if (cursor < value.Length && value[cursor] == '.')
        {
            cursor++;
            milliseconds = ParseDigits(value, ref cursor, 3, 999, "millisecond");
        }

        if (cursor == value.Length)
        {
            throw UnexpectedEof();
        }

        long offsetMilliseconds = value[cursor] switch
        {
            'Z' => ReadUtcTrailer(value, ref cursor),
            '+' => ReadOffset(value, ref cursor, 1),
            '-' => ReadOffset(value, ref cursor, -1),
            _ => throw new FormatException("invalid time zone designator")
        };

        if (cursor != value.Length)
        {
            throw new FormatException("unexpected additional characters");
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
        GregorianDateTimeParts utcParts = NormalizeUtcParts(year, month, day, hour, minute, second, millisecond, offsetMilliseconds);
        if (CompareParts(utcParts, MinSupportedInstant) < 0 || CompareParts(utcParts, MaxSupportedInstant) > 0)
        {
            throw new FormatException("timestamp out of range");
        }

        // Compute entirely in Int128 so that boundary dates with timezone offsets
        // are handled correctly. The offset must be applied before range-checking,
        // otherwise a pre-offset value that exceeds long range would be rejected
        // even if the final UTC result fits (e.g. +292278994-08-17T08:12:55.807+0100).
        long days = GregorianDateTime.DaysFromCivil(year, month, day);
        Int128 totalMs =
            (Int128)days * CedarConsts.MillisPerDay
            + hour * (long)CedarConsts.MillisPerHour
            + minute * (long)CedarConsts.MillisPerMinute
            + second * (long)CedarConsts.MillisPerSecond
            + millisecond
            - offsetMilliseconds;

        if (totalMs < long.MinValue || totalMs > long.MaxValue)
        {
            throw new FormatException("timestamp out of range");
        }

        return new CedarDatetime((long)totalMs);
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

    private static GregorianDateTimeParts NormalizeUtcParts(int year, int month, int day, int hour, int minute, int second, int millisecond, long offsetMilliseconds)
    {
        long timeOfDayMilliseconds =
            (hour * (long)CedarConsts.MillisPerHour)
            + (minute * (long)CedarConsts.MillisPerMinute)
            + (second * (long)CedarConsts.MillisPerSecond)
            + millisecond
            - offsetMilliseconds;

        int dayAdjustment = 0;
        if (timeOfDayMilliseconds < 0)
        {
            dayAdjustment = -1;
            timeOfDayMilliseconds += CedarConsts.MillisPerDay;
        }
        else if (timeOfDayMilliseconds >= CedarConsts.MillisPerDay)
        {
            dayAdjustment = 1;
            timeOfDayMilliseconds -= CedarConsts.MillisPerDay;
        }

        AdjustCivilDate(ref year, ref month, ref day, dayAdjustment);

        int utcHour = (int)(timeOfDayMilliseconds / CedarConsts.MillisPerHour);
        timeOfDayMilliseconds %= CedarConsts.MillisPerHour;

        int utcMinute = (int)(timeOfDayMilliseconds / CedarConsts.MillisPerMinute);
        timeOfDayMilliseconds %= CedarConsts.MillisPerMinute;

        int utcSecond = (int)(timeOfDayMilliseconds / CedarConsts.MillisPerSecond);
        int utcMillisecond = (int)(timeOfDayMilliseconds % CedarConsts.MillisPerSecond);

        return new GregorianDateTimeParts(year, month, day, utcHour, utcMinute, utcSecond, utcMillisecond);
    }

    private static void AdjustCivilDate(ref int year, ref int month, ref int day, int dayAdjustment)
    {
        if (dayAdjustment == 0)
        {
            return;
        }

        if (dayAdjustment > 0)
        {
            day++;
            int daysInMonth = GregorianDateTime.DaysInMonth(year, month);
            if (day > daysInMonth)
            {
                day = 1;
                month++;
                if (month > 12)
                {
                    month = 1;
                    year = checked(year + 1);
                }
            }

            return;
        }

        day--;
        if (day > 0)
        {
            return;
        }

        month--;
        if (month < 1)
        {
            month = 12;
            year = checked(year - 1);
        }

        day = GregorianDateTime.DaysInMonth(year, month);
    }

    private static int CompareParts(GregorianDateTimeParts left, GregorianDateTimeParts right)
    {
        int comparison = left.Year.CompareTo(right.Year);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Month.CompareTo(right.Month);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Day.CompareTo(right.Day);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Hour.CompareTo(right.Hour);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Minute.CompareTo(right.Minute);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Second.CompareTo(right.Second);
        if (comparison != 0)
        {
            return comparison;
        }

        return left.Millisecond.CompareTo(right.Millisecond);
    }

    private static void ExpectCharacter(string value, ref int cursor, char expected)
    {
        if (value.Length <= cursor)
        {
            throw UnexpectedEof();
        }

        if (value[cursor] != expected)
        {
            throw new FormatException($"unexpected character {value[cursor]}");
        }

        cursor++;
    }

    private static int ParseDigits(string value, ref int cursor, int length, int maxValue, string component)
    {
        if (value.Length < cursor + length)
        {
            throw UnexpectedEof();
        }

        int parsed = 0;
        for (int index = 0; index < length; index++)
        {
            char character = value[cursor + index];
            if (!char.IsAsciiDigit(character))
            {
                throw InvalidComponent(component);
            }

            parsed = checked(parsed * 10 + (character - '0'));
        }

        if (parsed > maxValue)
        {
            throw GreaterThan(component, maxValue);
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

        int hours = ParseDigits(value, ref cursor, 2, 23, "offset hours");
        int minutes = ParseDigits(value, ref cursor, 2, 59, "offset minutes");

        long magnitude = (hours * CedarConsts.MillisPerHour) + (minutes * CedarConsts.MillisPerMinute);
        return sign * magnitude;
    }

    private static void ValidateDate(int year, int month, int day)
    {
        if (month > 12)
        {
            throw GreaterThan("month", 12);
        }

        if (day > 31)
        {
            throw GreaterThan("day", 31);
        }

        if (month == 0 || day == 0)
        {
            throw new FormatException("invalid date");
        }

        int daysInMonth = GregorianDateTime.DaysInMonth(year, month);
        if (day > daysInMonth)
        {
            throw new FormatException("invalid date");
        }
    }

    private static FormatException UnexpectedEof()
    {
        return new FormatException("unexpected EOF");
    }

    private static FormatException InvalidComponent(string component)
    {
        return new FormatException($"invalid {component}");
    }

    private static FormatException GreaterThan(string component, int maxValue)
    {
        return new FormatException($"{component} is greater than {maxValue}");
    }
}

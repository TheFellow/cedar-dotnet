using System;
using Cedar.Core.Internal.Consts;

namespace Cedar.Internal.DateTimeSupport;

internal readonly record struct GregorianDateTimeParts(
    int Year,
    int Month,
    int Day,
    int Hour,
    int Minute,
    int Second,
    int Millisecond)
{
    public int DayOfYear => GregorianDateTime.GetDayOfYear(Year, Month, Day);

    public int IsoDayOfWeek => GregorianDateTime.GetIsoDayOfWeek(Year, Month, Day);
}

internal static class GregorianDateTime
{
    public static long ToUnixMilliseconds(int year, int month, int day, int hour, int minute, int second, int millisecond)
    {
        long days = DaysFromCivil(year, month, day);

        return checked(
            days * CedarConsts.MillisPerDay
            + (hour * CedarConsts.MillisPerHour)
            + (minute * CedarConsts.MillisPerMinute)
            + (second * CedarConsts.MillisPerSecond)
            + millisecond);
    }

    public static GregorianDateTimeParts FromUnixMilliseconds(long value)
    {
        long days = FloorDiv(value, CedarConsts.MillisPerDay);
        long timeOfDay = value - (days * CedarConsts.MillisPerDay);

        (int year, int month, int day) = CivilFromDays(days);

        int hour = (int)(timeOfDay / CedarConsts.MillisPerHour);
        timeOfDay %= CedarConsts.MillisPerHour;

        int minute = (int)(timeOfDay / CedarConsts.MillisPerMinute);
        timeOfDay %= CedarConsts.MillisPerMinute;

        int second = (int)(timeOfDay / CedarConsts.MillisPerSecond);
        int millisecond = (int)(timeOfDay % CedarConsts.MillisPerSecond);

        return new GregorianDateTimeParts(year, month, day, hour, minute, second, millisecond);
    }

    public static bool IsLeapYear(int year)
    {
        return PositiveMod(year, 4) == 0
            && (PositiveMod(year, 100) != 0 || PositiveMod(year, 400) == 0);
    }

    public static int DaysInMonth(int year, int month)
    {
        return month switch
        {
            1 or 3 or 5 or 7 or 8 or 10 or 12 => 31,
            4 or 6 or 9 or 11 => 30,
            2 => IsLeapYear(year) ? 29 : 28,
            _ => throw new ArgumentOutOfRangeException(nameof(month))
        };
    }

    public static int GetDayOfYear(int year, int month, int day)
    {
        int[] commonYearOffsets = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334];
        int dayOfYear = commonYearOffsets[month - 1] + day;

        if (month > 2 && IsLeapYear(year))
        {
            dayOfYear++;
        }

        return dayOfYear;
    }

    public static int GetIsoDayOfWeek(int year, int month, int day)
    {
        long days = DaysFromCivil(year, month, day);
        return (int)(PositiveMod(days + 3, 7) + 1);
    }

    public static long DaysFromCivil(int year, int month, int day)
    {
        long adjustedYear = month <= 2 ? year - 1L : year;
        long era = FloorDiv(adjustedYear, 400);
        long yearOfEra = adjustedYear - (era * 400);
        long monthPrime = month > 2 ? month - 3L : month + 9L;
        long dayOfYear = ((153 * monthPrime) + 2) / 5 + day - 1L;
        long dayOfEra = yearOfEra * 365 + yearOfEra / 4 - yearOfEra / 100 + dayOfYear;

        return era * 146097 + dayOfEra - 719468;
    }

    public static (int Year, int Month, int Day) CivilFromDays(long days)
    {
        long z = days + 719468;
        long era = FloorDiv(z, 146097);
        long dayOfEra = z - (era * 146097);
        long yearOfEra = (dayOfEra - dayOfEra / 1460 + dayOfEra / 36524 - dayOfEra / 146096) / 365;
        long year = yearOfEra + (era * 400);
        long dayOfYear = dayOfEra - (365 * yearOfEra + yearOfEra / 4 - yearOfEra / 100);
        long monthPrime = (5 * dayOfYear + 2) / 153;
        int day = (int)(dayOfYear - ((153 * monthPrime) + 2) / 5 + 1);
        int month = (int)(monthPrime < 10 ? monthPrime + 3 : monthPrime - 9);
        year += month <= 2 ? 1 : 0;

        return ((int)year, month, day);
    }

    private static long FloorDiv(long value, long divisor)
    {
        long quotient = value / divisor;
        long remainder = value % divisor;

        if (remainder != 0 && ((remainder > 0) != (divisor > 0)))
        {
            quotient--;
        }

        return quotient;
    }

    private static long PositiveMod(long value, long modulus)
    {
        long remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }
}
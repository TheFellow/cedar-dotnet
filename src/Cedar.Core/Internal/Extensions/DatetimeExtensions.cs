using System;
using Cedar.Core.Internal.Consts;
using Cedar.Core.Internal.Eval;
using Cedar.Internal.DateTimeSupport;
using Cedar.Types;

namespace Cedar.Core.Internal.Extensions;

internal static class DatetimeExtensions
{
    public static ICedarData ToDate(ICedarData[] args)
    {
        CedarDatetime value = TypeConversion.ValueToDatetime(args[0]);
        long millis = value.Value - (value.Value % CedarConsts.MillisPerDay);
        return new CedarDatetime(millis);
    }

    public static ICedarData ToTime(ICedarData[] args)
    {
        CedarDatetime value = TypeConversion.ValueToDatetime(args[0]);
        return new CedarDuration(value.Value % CedarConsts.MillisPerDay);
    }

    public static ICedarData Offset(ICedarData[] args)
    {
        CedarDatetime datetime = TypeConversion.ValueToDatetime(args[0]);
        CedarDuration duration = TypeConversion.ValueToDuration(args[1]);

        try
        {
            return new CedarDatetime(checked(datetime.Value + duration.Value));
        }
        catch (OverflowException)
        {
            throw new EvalException($"{EvalErrors.Overflow} while attempting to compute datetime offset");
        }
    }

    public static ICedarData DurationSince(ICedarData[] args)
    {
        CedarDatetime left = TypeConversion.ValueToDatetime(args[0]);
        CedarDatetime right = TypeConversion.ValueToDatetime(args[1]);

        try
        {
            return new CedarDuration(checked(left.Value - right.Value));
        }
        catch (OverflowException)
        {
            throw new EvalException($"{EvalErrors.Overflow} while attempting to compute datetime duration");
        }
    }

    public static ICedarData DaysInMonth(ICedarData[] args)
    {
        GregorianDateTimeParts value = GregorianDateTime.FromUnixMilliseconds(TypeConversion.ValueToDatetime(args[0]).Value);
        return new CedarLong(GregorianDateTime.DaysInMonth(value.Year, value.Month));
    }

    public static ICedarData Year(ICedarData[] args)
    {
        return new CedarLong(GregorianDateTime.FromUnixMilliseconds(TypeConversion.ValueToDatetime(args[0]).Value).Year);
    }

    public static ICedarData Month(ICedarData[] args)
    {
        return new CedarLong(GregorianDateTime.FromUnixMilliseconds(TypeConversion.ValueToDatetime(args[0]).Value).Month);
    }

    public static ICedarData Day(ICedarData[] args)
    {
        return new CedarLong(GregorianDateTime.FromUnixMilliseconds(TypeConversion.ValueToDatetime(args[0]).Value).Day);
    }

    public static ICedarData DayOfWeek(ICedarData[] args)
    {
        return new CedarLong(GregorianDateTime.FromUnixMilliseconds(TypeConversion.ValueToDatetime(args[0]).Value).IsoDayOfWeek);
    }

    public static ICedarData DayOfYear(ICedarData[] args)
    {
        return new CedarLong(GregorianDateTime.FromUnixMilliseconds(TypeConversion.ValueToDatetime(args[0]).Value).DayOfYear);
    }

    public static ICedarData Hour(ICedarData[] args)
    {
        return new CedarLong(GregorianDateTime.FromUnixMilliseconds(TypeConversion.ValueToDatetime(args[0]).Value).Hour);
    }

    public static ICedarData Minute(ICedarData[] args)
    {
        return new CedarLong(GregorianDateTime.FromUnixMilliseconds(TypeConversion.ValueToDatetime(args[0]).Value).Minute);
    }

    public static ICedarData Second(ICedarData[] args)
    {
        return new CedarLong(GregorianDateTime.FromUnixMilliseconds(TypeConversion.ValueToDatetime(args[0]).Value).Second);
    }

    public static ICedarData Millisecond(ICedarData[] args)
    {
        return new CedarLong(GregorianDateTime.FromUnixMilliseconds(TypeConversion.ValueToDatetime(args[0]).Value).Millisecond);
    }
}

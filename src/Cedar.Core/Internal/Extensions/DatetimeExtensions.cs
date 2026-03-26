using System;
using Cedar.Core.Internal.Consts;
using Cedar.Core.Internal.Eval;
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
        DateTimeOffset value = TypeConversion.ValueToDatetime(args[0]).ToDateTimeOffset();
        return new CedarLong(DateTime.DaysInMonth(value.Year, value.Month));
    }

    public static ICedarData Year(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDatetime(args[0]).ToDateTimeOffset().Year);
    }

    public static ICedarData Month(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDatetime(args[0]).ToDateTimeOffset().Month);
    }

    public static ICedarData Day(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDatetime(args[0]).ToDateTimeOffset().Day);
    }

    public static ICedarData DayOfWeek(ICedarData[] args)
    {
        DayOfWeek day = TypeConversion.ValueToDatetime(args[0]).ToDateTimeOffset().DayOfWeek;
        return new CedarLong(day == System.DayOfWeek.Sunday ? 7 : (int)day);
    }

    public static ICedarData DayOfYear(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDatetime(args[0]).ToDateTimeOffset().DayOfYear);
    }

    public static ICedarData Hour(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDatetime(args[0]).ToDateTimeOffset().Hour);
    }

    public static ICedarData Minute(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDatetime(args[0]).ToDateTimeOffset().Minute);
    }

    public static ICedarData Second(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDatetime(args[0]).ToDateTimeOffset().Second);
    }

    public static ICedarData Millisecond(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDatetime(args[0]).ToDateTimeOffset().Millisecond);
    }
}

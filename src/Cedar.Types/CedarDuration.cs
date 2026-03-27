using System;
using System.Globalization;
using System.Text;
using Cedar.Core.Internal.Consts;

namespace Cedar.Types;

public sealed record CedarDuration(long Value) : CedarValue
{
    private static readonly (string Unit, long Milliseconds)[] OrderedUnits =
    [
        ("d", CedarConsts.MillisPerDay),
        ("h", CedarConsts.MillisPerHour),
        ("m", CedarConsts.MillisPerMinute),
        ("s", CedarConsts.MillisPerSecond),
        ("ms", 1)
    ];

    public static CedarDuration Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length <= 1)
        {
            throw new FormatException("Duration values must include at least one quantity and unit.");
        }

        int index = 0;
        long sign = 1;
        if (value[index] == '-')
        {
            sign = -1;
            index++;
        }

        long total = 0;
        int unitIndex = 0;

        while (index < value.Length)
        {
            if (!char.IsAsciiDigit(value[index]))
            {
                throw new FormatException("Expected a numeric quantity before the unit.");
            }

            long quantity = 0;
            try
            {
                while (index < value.Length && char.IsAsciiDigit(value[index]))
                {
                    quantity = checked((quantity * 10) + (value[index] - '0'));
                    index++;
                }
            }
            catch (OverflowException)
            {
                throw new FormatException("Duration value overflows the valid int64 range.");
            }

            string unit = ReadUnit(value, ref index);
            int nextUnitIndex = FindUnitIndex(unit, unitIndex);
            if (nextUnitIndex < 0)
            {
                throw new FormatException($"Unexpected duration unit '{unit}'.");
            }

            unitIndex = nextUnitIndex + 1;
            try
            {
                total = checked(total + (quantity * OrderedUnits[nextUnitIndex].Milliseconds));
            }
            catch (OverflowException)
            {
                throw new FormatException("Duration value overflows the valid int64 range.");
            }
        }

        return new CedarDuration(sign * total);
    }

    public long ToDays()
    {
        return Value / CedarConsts.MillisPerDay;
    }

    public long ToHours()
    {
        return Value / CedarConsts.MillisPerHour;
    }

    public long ToMinutes()
    {
        return Value / CedarConsts.MillisPerMinute;
    }

    public long ToSeconds()
    {
        return Value / CedarConsts.MillisPerSecond;
    }

    public long ToMilliseconds()
    {
        return Value;
    }

    public TimeSpan ToTimeSpan()
    {
        return TimeSpan.FromMilliseconds(Value);
    }

    public override string MarshalCedar()
    {
        return "duration(\"" + FormatValue() + "\")";
    }

    public override int GetHashCode()
    {
        return CedarHash.ForInt64(nameof(CedarDuration), Value);
    }

    private static int FindUnitIndex(string unit, int start)
    {
        for (int index = start; index < OrderedUnits.Length; index++)
        {
            if (OrderedUnits[index].Unit == unit)
            {
                return index;
            }
        }

        return -1;
    }

    private static string ReadUnit(string value, ref int index)
    {
        if (index >= value.Length)
        {
            throw new FormatException("Duration values must end with a unit.");
        }

        return value[index] switch
        {
            'd' => ConsumeSingleCharacterUnit("d", ref index),
            'h' => ConsumeSingleCharacterUnit("h", ref index),
            's' => ConsumeSingleCharacterUnit("s", ref index),
            'm' when index + 1 < value.Length && value[index + 1] == 's' => ConsumeMillisecondsUnit(ref index),
            'm' => ConsumeSingleCharacterUnit("m", ref index),
            _ => throw new FormatException($"Unexpected character '{value[index]}'.")
        };
    }

    private static string ConsumeSingleCharacterUnit(string unit, ref int index)
    {
        index++;
        return unit;
    }

    private static string ConsumeMillisecondsUnit(ref int index)
    {
        index += 2;
        return "ms";
    }

    private string FormatValue()
    {
        if (Value == 0)
        {
            return "0ms";
        }

        StringBuilder builder = new();
        long remaining = Value;

        if (remaining < 0)
        {
            builder.Append('-');
            remaining = -remaining;
        }

        foreach ((string unit, long milliseconds) in OrderedUnits)
        {
            long quantity = remaining / milliseconds;
            if (quantity <= 0)
            {
                continue;
            }

            builder.Append(quantity.ToString(CultureInfo.InvariantCulture));
            builder.Append(unit);
            remaining %= milliseconds;
        }

        return builder.ToString();
    }
}

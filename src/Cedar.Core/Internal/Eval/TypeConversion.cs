using System.Net;
using Cedar.Types;

namespace Cedar.Core.Internal.Eval;

internal static class TypeConversion
{
    public static bool ValueToBool(ICedarData value)
    {
        if (value is CedarBool b)
        {
            return b.Value;
        }

        throw new EvalException($"expected bool, got {TypeName(value)}");
    }

    public static long ValueToLong(ICedarData value)
    {
        if (value is CedarLong l)
        {
            return l.Value;
        }

        throw new EvalException($"expected long, got {TypeName(value)}");
    }

    public static string ValueToString(ICedarData value)
    {
        if (value is CedarString s)
        {
            return s.Value;
        }

        throw new EvalException($"expected string, got {TypeName(value)}");
    }

    public static CedarSet ValueToSet(ICedarData value)
    {
        if (value is CedarSet s)
        {
            return s;
        }

        throw new EvalException($"expected set, got {TypeName(value)}");
    }

    public static CedarRecord ValueToRecord(ICedarData value)
    {
        if (value is CedarRecord r)
        {
            return r;
        }

        throw new EvalException($"expected record, got {TypeName(value)}");
    }

    public static EntityUid ValueToEntity(ICedarData value)
    {
        if (value is EntityUid e)
        {
            return e;
        }

        throw new EvalException($"expected entity, got {TypeName(value)}");
    }

    public static CedarDecimal ValueToDecimal(ICedarData value)
    {
        if (value is CedarDecimal d)
        {
            return d;
        }

        throw new EvalException($"expected decimal, got {TypeName(value)}");
    }

    public static CedarDatetime ValueToDatetime(ICedarData value)
    {
        if (value is CedarDatetime d)
        {
            return d;
        }

        throw new EvalException($"expected datetime, got {TypeName(value)}");
    }

    public static CedarDuration ValueToDuration(ICedarData value)
    {
        if (value is CedarDuration d)
        {
            return d;
        }

        throw new EvalException($"expected duration, got {TypeName(value)}");
    }

    public static CedarIpAddress ValueToIp(ICedarData value)
    {
        if (value is CedarIpAddress ip)
        {
            return ip;
        }

        throw new EvalException($"expected IP address, got {TypeName(value)}");
    }

    private static string TypeName(ICedarData value)
    {
        return value switch
        {
            CedarBool => "bool",
            CedarLong => "long",
            CedarString => "string",
            CedarDecimal => "decimal",
            CedarDatetime => "datetime",
            CedarDuration => "duration",
            CedarIpAddress => "IP address",
            CedarSet => "set",
            CedarRecord => "record",
            CedarPattern => "pattern",
            EntityUid => "entity",
            _ => value.GetType().Name
        };
    }
}

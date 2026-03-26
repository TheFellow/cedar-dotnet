using System;
using Cedar.Core.Internal.Eval;
using Cedar.Types;

namespace Cedar.Core.Internal.Extensions;

internal static class ConstructorExtensions
{
    public static ICedarData Decimal(ICedarData[] args)
    {
        return Parse(args[0], CedarDecimal.Parse);
    }

    public static ICedarData Ip(ICedarData[] args)
    {
        return Parse(args[0], CedarIpAddress.Parse);
    }

    public static ICedarData Datetime(ICedarData[] args)
    {
        return Parse(args[0], CedarDatetime.Parse);
    }

    public static ICedarData Duration(ICedarData[] args)
    {
        return Parse(args[0], CedarDuration.Parse);
    }

    private static ICedarData Parse(ICedarData arg, Func<string, ICedarData> parser)
    {
        string value = TypeConversion.ValueToString(arg);

        try
        {
            return parser(value);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            throw new EvalException(exception.Message);
        }
    }
}

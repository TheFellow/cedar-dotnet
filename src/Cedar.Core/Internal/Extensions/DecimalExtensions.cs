using Cedar.Core.Internal.Eval;
using Cedar.Types;

namespace Cedar.Core.Internal.Extensions;

internal static class DecimalExtensions
{
    public static ICedarData LessThan(ICedarData[] args)
    {
        CedarDecimal left = TypeConversion.ValueToDecimal(args[0]);
        CedarDecimal right = TypeConversion.ValueToDecimal(args[1]);
        return new CedarBool(left.CompareTo(right) < 0);
    }

    public static ICedarData LessThanOrEqual(ICedarData[] args)
    {
        CedarDecimal left = TypeConversion.ValueToDecimal(args[0]);
        CedarDecimal right = TypeConversion.ValueToDecimal(args[1]);
        return new CedarBool(left.CompareTo(right) <= 0);
    }

    public static ICedarData GreaterThan(ICedarData[] args)
    {
        CedarDecimal left = TypeConversion.ValueToDecimal(args[0]);
        CedarDecimal right = TypeConversion.ValueToDecimal(args[1]);
        return new CedarBool(left.CompareTo(right) > 0);
    }

    public static ICedarData GreaterThanOrEqual(ICedarData[] args)
    {
        CedarDecimal left = TypeConversion.ValueToDecimal(args[0]);
        CedarDecimal right = TypeConversion.ValueToDecimal(args[1]);
        return new CedarBool(left.CompareTo(right) >= 0);
    }
}

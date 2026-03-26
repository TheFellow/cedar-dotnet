using Cedar.Core.Internal.Eval;
using Cedar.Types;

namespace Cedar.Core.Internal.Extensions;

internal static class DurationExtensions
{
    public static ICedarData ToDays(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDuration(args[0]).ToDays());
    }

    public static ICedarData ToHours(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDuration(args[0]).ToHours());
    }

    public static ICedarData ToMinutes(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDuration(args[0]).ToMinutes());
    }

    public static ICedarData ToSeconds(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDuration(args[0]).ToSeconds());
    }

    public static ICedarData ToMilliseconds(ICedarData[] args)
    {
        return new CedarLong(TypeConversion.ValueToDuration(args[0]).ToMilliseconds());
    }
}

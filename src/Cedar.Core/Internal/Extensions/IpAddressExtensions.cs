using Cedar.Core.Internal.Eval;
using Cedar.Types;

namespace Cedar.Core.Internal.Extensions;

internal static class IpAddressExtensions
{
    public static ICedarData IsIpv4(ICedarData[] args)
    {
        return new CedarBool(TypeConversion.ValueToIp(args[0]).IsIPv4());
    }

    public static ICedarData IsIpv6(ICedarData[] args)
    {
        return new CedarBool(TypeConversion.ValueToIp(args[0]).IsIPv6());
    }

    public static ICedarData IsLoopback(ICedarData[] args)
    {
        return new CedarBool(TypeConversion.ValueToIp(args[0]).IsLoopback());
    }

    public static ICedarData IsMulticast(ICedarData[] args)
    {
        return new CedarBool(TypeConversion.ValueToIp(args[0]).IsMulticast());
    }

    public static ICedarData IsInRange(ICedarData[] args)
    {
        CedarIpAddress value = TypeConversion.ValueToIp(args[0]);
        CedarIpAddress range = TypeConversion.ValueToIp(args[1]);
        return new CedarBool(range.Contains(value));
    }
}

using Cedar.Core.Internal.Eval;
using Cedar.Types;

namespace Cedar.Core.Internal.Extensions;

internal static class IpAddressExtensions
{
    public static ICedarData IsIpv4(ICedarData[] args)
    {
        return TypeConversion.ValueToIp(args[0]).IsIPv4() ? CedarBool.True : CedarBool.False;
    }

    public static ICedarData IsIpv6(ICedarData[] args)
    {
        return TypeConversion.ValueToIp(args[0]).IsIPv6() ? CedarBool.True : CedarBool.False;
    }

    public static ICedarData IsLoopback(ICedarData[] args)
    {
        return TypeConversion.ValueToIp(args[0]).IsLoopback() ? CedarBool.True : CedarBool.False;
    }

    public static ICedarData IsMulticast(ICedarData[] args)
    {
        return TypeConversion.ValueToIp(args[0]).IsMulticast() ? CedarBool.True : CedarBool.False;
    }

    public static ICedarData IsInRange(ICedarData[] args)
    {
        CedarIpAddress value = TypeConversion.ValueToIp(args[0]);
        CedarIpAddress range = TypeConversion.ValueToIp(args[1]);
        return range.Contains(value) ? CedarBool.True : CedarBool.False;
    }
}

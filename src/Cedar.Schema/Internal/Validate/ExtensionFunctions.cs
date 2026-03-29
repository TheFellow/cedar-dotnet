using System;
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema.Internal.Validate;

internal sealed record ExtFuncSig(
    bool IsConstructor,
    IReadOnlyList<CedarType> ArgTypes,
    CedarType ReturnType);

internal static class ExtensionFunctions
{
    public static readonly IReadOnlyDictionary<string, ExtFuncSig> All
        = new Dictionary<string, ExtFuncSig>(StringComparer.Ordinal)
        {
            ["ip"] = new(true, [new CedarString()], new CedarExtType(new Ident("ipaddr"))),
            ["decimal"] = new(true, [new CedarString()], new CedarExtType(new Ident("decimal"))),
            ["datetime"] = new(true, [new CedarString()], new CedarExtType(new Ident("datetime"))),
            ["duration"] = new(true, [new CedarString()], new CedarExtType(new Ident("duration"))),
            ["lessThan"] = new(false, [new CedarExtType(new Ident("decimal")), new CedarExtType(new Ident("decimal"))], new CedarBool()),
            ["lessThanOrEqual"] = new(false, [new CedarExtType(new Ident("decimal")), new CedarExtType(new Ident("decimal"))], new CedarBool()),
            ["greaterThan"] = new(false, [new CedarExtType(new Ident("decimal")), new CedarExtType(new Ident("decimal"))], new CedarBool()),
            ["greaterThanOrEqual"] = new(false, [new CedarExtType(new Ident("decimal")), new CedarExtType(new Ident("decimal"))], new CedarBool()),
            ["isIpv4"] = new(false, [new CedarExtType(new Ident("ipaddr"))], new CedarBool()),
            ["isIpv6"] = new(false, [new CedarExtType(new Ident("ipaddr"))], new CedarBool()),
            ["isLoopback"] = new(false, [new CedarExtType(new Ident("ipaddr"))], new CedarBool()),
            ["isMulticast"] = new(false, [new CedarExtType(new Ident("ipaddr"))], new CedarBool()),
            ["isInRange"] = new(false, [new CedarExtType(new Ident("ipaddr")), new CedarExtType(new Ident("ipaddr"))], new CedarBool()),
            ["toDate"] = new(false, [new CedarExtType(new Ident("datetime"))], new CedarExtType(new Ident("datetime"))),
            ["toTime"] = new(false, [new CedarExtType(new Ident("datetime"))], new CedarExtType(new Ident("duration"))),
            ["offset"] = new(false, [new CedarExtType(new Ident("datetime")), new CedarExtType(new Ident("duration"))], new CedarExtType(new Ident("datetime"))),
            ["durationSince"] = new(false, [new CedarExtType(new Ident("datetime")), new CedarExtType(new Ident("datetime"))], new CedarExtType(new Ident("duration"))),
            ["toDays"] = new(false, [new CedarExtType(new Ident("duration"))], new CedarLong()),
            ["toHours"] = new(false, [new CedarExtType(new Ident("duration"))], new CedarLong()),
            ["toMinutes"] = new(false, [new CedarExtType(new Ident("duration"))], new CedarLong()),
            ["toSeconds"] = new(false, [new CedarExtType(new Ident("duration"))], new CedarLong()),
            ["toMilliseconds"] = new(false, [new CedarExtType(new Ident("duration"))], new CedarLong())
        };
}

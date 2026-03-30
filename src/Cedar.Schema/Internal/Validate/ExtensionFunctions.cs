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
    internal static readonly IReadOnlyDictionary<string, ExtFuncSig> All
        = new Dictionary<string, ExtFuncSig>(StringComparer.Ordinal)
        {
            ["ip"] = new(true, [CedarStringType.Instance], new CedarExtType(new Ident("ipaddr"))),
            ["decimal"] = new(true, [CedarStringType.Instance], new CedarExtType(new Ident("decimal"))),
            ["datetime"] = new(true, [CedarStringType.Instance], new CedarExtType(new Ident("datetime"))),
            ["duration"] = new(true, [CedarStringType.Instance], new CedarExtType(new Ident("duration"))),
            ["lessThan"] = new(false, [new CedarExtType(new Ident("decimal")), new CedarExtType(new Ident("decimal"))], CedarBoolType.Instance),
            ["lessThanOrEqual"] = new(false, [new CedarExtType(new Ident("decimal")), new CedarExtType(new Ident("decimal"))], CedarBoolType.Instance),
            ["greaterThan"] = new(false, [new CedarExtType(new Ident("decimal")), new CedarExtType(new Ident("decimal"))], CedarBoolType.Instance),
            ["greaterThanOrEqual"] = new(false, [new CedarExtType(new Ident("decimal")), new CedarExtType(new Ident("decimal"))], CedarBoolType.Instance),
            ["isIpv4"] = new(false, [new CedarExtType(new Ident("ipaddr"))], CedarBoolType.Instance),
            ["isIpv6"] = new(false, [new CedarExtType(new Ident("ipaddr"))], CedarBoolType.Instance),
            ["isLoopback"] = new(false, [new CedarExtType(new Ident("ipaddr"))], CedarBoolType.Instance),
            ["isMulticast"] = new(false, [new CedarExtType(new Ident("ipaddr"))], CedarBoolType.Instance),
            ["isInRange"] = new(false, [new CedarExtType(new Ident("ipaddr")), new CedarExtType(new Ident("ipaddr"))], CedarBoolType.Instance),
            ["toDate"] = new(false, [new CedarExtType(new Ident("datetime"))], new CedarExtType(new Ident("datetime"))),
            ["toTime"] = new(false, [new CedarExtType(new Ident("datetime"))], new CedarExtType(new Ident("duration"))),
            ["offset"] = new(false, [new CedarExtType(new Ident("datetime")), new CedarExtType(new Ident("duration"))], new CedarExtType(new Ident("datetime"))),
            ["durationSince"] = new(false, [new CedarExtType(new Ident("datetime")), new CedarExtType(new Ident("datetime"))], new CedarExtType(new Ident("duration"))),
            ["toDays"] = new(false, [new CedarExtType(new Ident("duration"))], CedarLongType.Instance),
            ["toHours"] = new(false, [new CedarExtType(new Ident("duration"))], CedarLongType.Instance),
            ["toMinutes"] = new(false, [new CedarExtType(new Ident("duration"))], CedarLongType.Instance),
            ["toSeconds"] = new(false, [new CedarExtType(new Ident("duration"))], CedarLongType.Instance),
            ["toMilliseconds"] = new(false, [new CedarExtType(new Ident("duration"))], CedarLongType.Instance)
        };
}

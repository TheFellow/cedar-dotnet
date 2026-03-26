using System;
using System.Collections.Generic;
using Cedar.Core.Internal.Eval;
using Cedar.Types;

namespace Cedar.Core.Internal.Extensions;

internal static class ExtensionRegistry
{
    private static readonly Dictionary<string, ExtensionDefinition> Definitions = new(StringComparer.Ordinal)
    {
        ["decimal"] = new ExtensionDefinition(1, false, ConstructorExtensions.Decimal),
        ["ip"] = new ExtensionDefinition(1, false, ConstructorExtensions.Ip),
        ["datetime"] = new ExtensionDefinition(1, false, ConstructorExtensions.Datetime),
        ["duration"] = new ExtensionDefinition(1, false, ConstructorExtensions.Duration),
        ["lessThan"] = new ExtensionDefinition(2, true, DecimalExtensions.LessThan),
        ["lessThanOrEqual"] = new ExtensionDefinition(2, true, DecimalExtensions.LessThanOrEqual),
        ["greaterThan"] = new ExtensionDefinition(2, true, DecimalExtensions.GreaterThan),
        ["greaterThanOrEqual"] = new ExtensionDefinition(2, true, DecimalExtensions.GreaterThanOrEqual),
        ["isIpv4"] = new ExtensionDefinition(1, true, IpAddressExtensions.IsIpv4),
        ["isIpv6"] = new ExtensionDefinition(1, true, IpAddressExtensions.IsIpv6),
        ["isLoopback"] = new ExtensionDefinition(1, true, IpAddressExtensions.IsLoopback),
        ["isMulticast"] = new ExtensionDefinition(1, true, IpAddressExtensions.IsMulticast),
        ["isInRange"] = new ExtensionDefinition(2, true, IpAddressExtensions.IsInRange),
        ["toDate"] = new ExtensionDefinition(1, true, DatetimeExtensions.ToDate),
        ["toTime"] = new ExtensionDefinition(1, true, DatetimeExtensions.ToTime),
        ["offset"] = new ExtensionDefinition(2, true, DatetimeExtensions.Offset),
        ["durationSince"] = new ExtensionDefinition(2, true, DatetimeExtensions.DurationSince),
        ["daysInMonth"] = new ExtensionDefinition(1, true, DatetimeExtensions.DaysInMonth),
        ["year"] = new ExtensionDefinition(1, true, DatetimeExtensions.Year),
        ["month"] = new ExtensionDefinition(1, true, DatetimeExtensions.Month),
        ["day"] = new ExtensionDefinition(1, true, DatetimeExtensions.Day),
        ["dayOfWeek"] = new ExtensionDefinition(1, true, DatetimeExtensions.DayOfWeek),
        ["dayOfYear"] = new ExtensionDefinition(1, true, DatetimeExtensions.DayOfYear),
        ["hour"] = new ExtensionDefinition(1, true, DatetimeExtensions.Hour),
        ["minute"] = new ExtensionDefinition(1, true, DatetimeExtensions.Minute),
        ["second"] = new ExtensionDefinition(1, true, DatetimeExtensions.Second),
        ["millisecond"] = new ExtensionDefinition(1, true, DatetimeExtensions.Millisecond),
        ["toDays"] = new ExtensionDefinition(1, true, DurationExtensions.ToDays),
        ["toHours"] = new ExtensionDefinition(1, true, DurationExtensions.ToHours),
        ["toMinutes"] = new ExtensionDefinition(1, true, DurationExtensions.ToMinutes),
        ["toSeconds"] = new ExtensionDefinition(1, true, DurationExtensions.ToSeconds),
        ["toMilliseconds"] = new ExtensionDefinition(1, true, DurationExtensions.ToMilliseconds)
    };

    public static bool TryGet(string name, out ExtensionDefinition definition)
    {
        return Definitions.TryGetValue(name, out definition);
    }

    public static ICedarData Invoke(string name, ICedarData[] args)
    {
        if (!Definitions.TryGetValue(name, out ExtensionDefinition definition))
        {
            throw new EvalException($"{EvalErrors.UnknownExtensionFunction}: {name}");
        }

        if (definition.Arity != args.Length)
        {
            throw new EvalException($"{EvalErrors.WrongArity}: {name} takes {definition.Arity} parameter(s), but {args.Length} provided");
        }

        return definition.Invoke(args);
    }
}

internal readonly record struct ExtensionDefinition(int Arity, bool IsMethod, Func<ICedarData[], ICedarData> Invoke);

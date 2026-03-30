using System;
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema.Internal.Validate;

internal static class ValueChecker
{
    internal const string DeserializationPrefix = "[deser] ";

    internal static (bool IsDeserError, string? Error) CheckValue(ICedarData value, ResolvedType expected)
    {
        return expected switch
        {
            ResolvedStringType => value is CedarString ? (false, null) : (false, $"expected String, got {value.GetType().Name}"),
            ResolvedLongType => value is CedarLong ? (false, null) : (false, $"expected Long, got {value.GetType().Name}"),
            ResolvedBoolType => value is CedarBool ? (false, null) : (false, $"expected Boolean, got {value.GetType().Name}"),
            ResolvedEntityType entityType => CheckEntityValue(value, entityType),
            ResolvedSetType setType => CheckSet(value, setType),
            ResolvedRecordType recordType => value is CedarRecord record
                ? CheckRecord(record, recordType)
                : (true, $"expected Record, got {value.GetType().Name}"),
            ResolvedExtensionType extensionType => CheckExtensionValue(value, extensionType),
            _ => (false, $"unsupported resolved type {expected.GetType().Name}")
        };
    }

    internal static (bool IsDeserError, string? Error) CheckRecord(CedarRecord record, ResolvedRecordType expected)
    {
        foreach ((string name, ResolvedAttribute attribute) in expected.Attributes)
        {
            if (!record.TryGetValue(new CedarString(name), out ICedarData? value))
            {
                if (!attribute.Optional)
                {
                    return (false, $"missing required attribute \"{name}\"");
                }

                continue;
            }

            (bool isDeserError, string? error) = CheckValue(value, attribute.Type);
            if (error is not null)
            {
                return (isDeserError, $"attribute \"{name}\": {error}");
            }
        }

        foreach (KeyValuePair<CedarString, ICedarData> entry in record)
        {
            if (!expected.Attributes.ContainsKey(entry.Key.Value))
            {
                return (true, $"unexpected attribute \"{entry.Key.Value}\"");
            }
        }

        return (false, null);
    }

    internal static (bool IsDeserError, string? Error) CheckExtensionValue(ICedarData value, ResolvedExtensionType expected)
    {
        return expected.Name.Value switch
        {
            "ipaddr" => CheckExtensionValue<CedarIpAddress>("IPAddr", value),
            "decimal" => CheckExtensionValue<CedarDecimal>("Decimal", value),
            "datetime" => CheckExtensionValue<CedarDatetime>("Datetime", value),
            "duration" => CheckExtensionValue<CedarDuration>("Duration", value),
            _ => (false, null)
        };
    }

    internal static string PrefixError(bool isDeserError, string error)
    {
        return isDeserError ? DeserializationPrefix + error : error;
    }

    private static (bool IsDeserError, string? Error) CheckEntityValue(ICedarData value, ResolvedEntityType expected)
    {
        if (value is not EntityUid uid)
        {
            return (true, $"expected EntityUID, got {value.GetType().Name}");
        }

        return uid.Type == expected.Name
            ? (false, null)
            : (false, $"expected entity type \"{expected.Name}\", got \"{uid.Type}\"");
    }

    private static (bool IsDeserError, string? Error) CheckSet(ICedarData value, ResolvedSetType expected)
    {
        if (value is not CedarSet set)
        {
            return (true, $"expected Set, got {value.GetType().Name}");
        }

        foreach (ICedarData element in set)
        {
            (bool isDeserError, string? error) = CheckValue(element, expected.Element);
            if (error is not null)
            {
                return (isDeserError, $"set element: {error}");
            }
        }

        return (false, null);
    }

    private static (bool IsDeserError, string? Error) CheckExtensionValue<TExtension>(string expectedName, ICedarData value)
        where TExtension : class, ICedarData
    {
        if (value is TExtension)
        {
            return (false, null);
        }

        string message = $"expected {expectedName}, got {value.GetType().Name}";
        return value is CedarIpAddress or CedarDecimal or CedarDatetime or CedarDuration
            ? (false, message)
            : (true, message);
    }
}

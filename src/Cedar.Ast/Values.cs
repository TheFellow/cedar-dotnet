using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Cedar.Ast.Internal;
using Cedar.Types;

namespace Cedar.Ast;

public static class Values
{
    public readonly record struct RecordElement(string Key, Node Value);

    public static Node Boolean(bool value)
    {
        return Value(new CedarBool(value));
    }

    public static Node String(string value)
    {
        return Value(new CedarString(value));
    }

    public static Node Long(long value)
    {
        return Value(new CedarLong(value));
    }

    public static Node Set(params Node[] values)
    {
        return SetNodes(values);
    }

    public static Node SetNodes(IEnumerable<Node> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        ImmutableArray<INode>.Builder builder = ImmutableArray.CreateBuilder<INode>();
        foreach (Node value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            builder.Add(value.Inner);
        }

        return new Node(new NodeSet(builder.ToImmutable()));
    }

    public static Node Set(CedarSet values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return SetNodes(values.Select(ValueToNode));
    }

    public static Node Record(params RecordElement[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Dictionary<string, int> seen = new(StringComparer.Ordinal);
        ImmutableArray<NodeRecordElement>.Builder builder = ImmutableArray.CreateBuilder<NodeRecordElement>();

        foreach (RecordElement entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry.Value);

            NodeRecordElement recordElement = new(new CedarString(entry.Key), entry.Value.Inner);
            if (seen.TryGetValue(entry.Key, out int existingIndex))
            {
                builder[existingIndex] = recordElement;
                continue;
            }

            seen.Add(entry.Key, builder.Count);
            builder.Add(recordElement);
        }

        return new Node(new NodeRecord(builder.ToImmutable()));
    }

    public static Node RecordNodes(IReadOnlyDictionary<string, Node> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        ImmutableArray<NodeRecordElement>.Builder builder = ImmutableArray.CreateBuilder<NodeRecordElement>(entries.Count);

        foreach (KeyValuePair<string, Node> entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry.Key);
            ArgumentNullException.ThrowIfNull(entry.Value);

            builder.Add(new NodeRecordElement(new CedarString(entry.Key), entry.Value.Inner));
        }

        return new Node(new NodeRecord(builder.ToImmutable()));
    }

    public static Node Record(CedarRecord entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return RecordNodes(entries.ToDictionary(static entry => entry.Key.Value, static entry => ValueToNode(entry.Value), StringComparer.Ordinal));
    }

    public static Node EntityUid(string entityType, string id)
    {
        return Value(new EntityUid(new EntityType(entityType), new CedarString(id)));
    }

    public static Node EntityUid(EntityType entityType, string id)
    {
        return Value(new EntityUid(entityType, new CedarString(id)));
    }

    public static Node EntityUid(EntityUid value)
    {
        return Value(value);
    }

    public static Node IpAddr(string value)
    {
        return Value(CedarIpAddress.Parse(value));
    }

    public static Node Decimal(string value)
    {
        return Value(CedarDecimal.Parse(value));
    }

    public static Node Decimal(long value, int exponent)
    {
        return Value(CedarDecimal.NewDecimal(value, exponent));
    }

    public static Node Datetime(string value)
    {
        return Value(CedarDatetime.Parse(value));
    }

    public static Node Datetime(DateTimeOffset value)
    {
        return Value(CedarDatetime.FromDateTimeOffset(value));
    }

    public static Node Duration(string value)
    {
        return Value(CedarDuration.Parse(value));
    }

    public static Node Duration(TimeSpan value)
    {
        return Value(new CedarDuration(value.Ticks / TimeSpan.TicksPerMillisecond));
    }

    public static Node Value(ICedarData value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Node(new NodeValue(value));
    }

    private static Node ValueToNode(ICedarData value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value switch
        {
            CedarBool boolean => Boolean(boolean.Value),
            CedarString @string => String(@string.Value),
            CedarLong number => Long(number.Value),
            CedarSet set => Set(set),
            CedarRecord record => Record(record),
            EntityUid entityUid => EntityUid(entityUid),
            CedarDecimal decimalValue => Value(decimalValue),
            CedarIpAddress ipAddress => Value(ipAddress),
            CedarDatetime datetime => Value(datetime),
            CedarDuration duration => Value(duration),
            _ => throw new InvalidOperationException($"Unexpected Cedar value type: {value.GetType().Name}"),
        };
    }
}

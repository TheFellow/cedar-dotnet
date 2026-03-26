using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cedar.Types.Internal;

namespace Cedar.Types;

public sealed record CedarRecord : CedarValue, IEnumerable<KeyValuePair<CedarString, ICedarData>>
{
    private readonly Dictionary<CedarString, ICedarData> _entries;
    private readonly KeyValuePair<CedarString, ICedarData>[] _orderedEntries;

    public CedarRecord()
        : this((IDictionary<CedarString, ICedarData>?)null)
    {
    }

    public CedarRecord(IDictionary<CedarString, ICedarData>? entries)
    {
        _entries = [];

        if (entries is not null)
        {
            foreach ((CedarString key, ICedarData value) in entries)
            {
                ArgumentNullException.ThrowIfNull(key);
                ArgumentNullException.ThrowIfNull(value);
                CedarData.EnsureSupported(value);
                _entries.Add(key, value);
            }
        }

        _orderedEntries = [.. _entries.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)];
    }

    public int Count => _entries.Count;

    public IEnumerable<CedarString> Keys => _orderedEntries.Select(static entry => entry.Key);

    public IEnumerable<ICedarData> Values => _orderedEntries.Select(static entry => entry.Value);

    public bool TryGetValue(CedarString key, out ICedarData value)
    {
        return _entries.TryGetValue(key, out value!);
    }

    public ICedarData this[CedarString key] => _entries[key];

    public override string MarshalCedar()
    {
        StringBuilder builder = new();
        builder.Append('{');

        for (int index = 0; index < _orderedEntries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            KeyValuePair<CedarString, ICedarData> entry = _orderedEntries[index];
            builder.Append(entry.Key.MarshalCedar());
            builder.Append(':');
            builder.Append(CedarData.MarshalCedar(entry.Value));
        }

        builder.Append('}');
        return builder.ToString();
    }

    public bool Equals(CedarRecord? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || Count != other.Count || GetHashCode() != other.GetHashCode())
        {
            return false;
        }

        foreach ((CedarString key, ICedarData value) in _entries)
        {
            if (!other._entries.TryGetValue(key, out ICedarData? otherValue) || !CedarData.Equals(value, otherValue))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        return CedarHash.ForXorCollection(
            nameof(CedarRecord),
            _orderedEntries.Select(entry => CedarHash.ForInt32Pair("Entry", entry.Key.GetHashCode(), CedarData.GetHashCode(entry.Value))));
    }

    public IEnumerator<KeyValuePair<CedarString, ICedarData>> GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<CedarString, ICedarData>>)_orderedEntries).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

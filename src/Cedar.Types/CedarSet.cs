using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cedar.Types.Internal;

namespace Cedar.Types;

public sealed record CedarSet : CedarValue, IEnumerable<ICedarData>
{
    private readonly Dictionary<int, List<ICedarData>> _buckets;
    private readonly ICedarData[] _orderedValues;

    public CedarSet()
        : this(Array.Empty<ICedarData>())
    {
    }

    public CedarSet(params ICedarData[] values)
        : this((IEnumerable<ICedarData>)values)
    {
    }

    public CedarSet(IEnumerable<ICedarData> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        _buckets = [];
        List<ICedarData> uniqueValues = [];

        foreach (ICedarData value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            CedarData.EnsureSupported(value);

            int hash = CedarData.GetHashCode(value);
            if (!_buckets.TryGetValue(hash, out List<ICedarData>? bucket))
            {
                bucket = [];
                _buckets.Add(hash, bucket);
            }

            if (bucket.Any(existing => CedarData.Equals(existing, value)))
            {
                continue;
            }

            bucket.Add(value);
            uniqueValues.Add(value);
        }

        uniqueValues.Sort(CedarData.CompareCanonical);
        _orderedValues = [.. uniqueValues];
    }

    public int Count => _orderedValues.Length;

    public bool Contains(ICedarData value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return _buckets.TryGetValue(CedarData.GetHashCode(value), out List<ICedarData>? bucket)
            && bucket.Any(existing => CedarData.Equals(existing, value));
    }

    public override string MarshalCedar()
    {
        StringBuilder builder = new();
        builder.Append('[');

        for (int index = 0; index < _orderedValues.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(CedarData.MarshalCedar(_orderedValues[index]));
        }

        builder.Append(']');
        return builder.ToString();
    }

    public bool Equals(CedarSet? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || Count != other.Count || GetHashCode() != other.GetHashCode())
        {
            return false;
        }

        foreach (ICedarData value in _orderedValues)
        {
            if (!other.Contains(value))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        return CedarHash.ForXorCollection(nameof(CedarSet), _orderedValues.Select(CedarData.GetHashCode));
    }

    public IEnumerator<ICedarData> GetEnumerator()
    {
        return ((IEnumerable<ICedarData>)_orderedValues).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

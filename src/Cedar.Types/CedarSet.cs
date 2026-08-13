using System;
using System.Collections;
using System.Collections.Generic;
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

            bool found = false;
            foreach (ICedarData existing in bucket)
            {
                if (CedarData.Equals(existing, value))
                {
                    found = true;
                    break;
                }
            }
            if (found)
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

        if (!_buckets.TryGetValue(CedarData.GetHashCode(value), out List<ICedarData>? bucket))
        {
            return false;
        }
        foreach (ICedarData existing in bucket)
        {
            if (CedarData.Equals(existing, value))
            {
                return true;
            }
        }
        return false;
    }

    public bool ContainsAll(CedarSet other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (ICedarData value in other._orderedValues)
        {
            if (!Contains(value))
            {
                return false;
            }
        }

        return true;
    }

    public bool ContainsAny(CedarSet other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (ICedarData value in other._orderedValues)
        {
            if (Contains(value))
            {
                return true;
            }
        }

        return false;
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
        ulong combined = 0;
        for (int i = 0; i < _orderedValues.Length; i++)
        {
            combined ^= unchecked((uint)CedarData.GetHashCode(_orderedValues[i]));
        }

        return CedarHash.ForXorCollection(nameof(CedarSet), combined, _orderedValues.Length);
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

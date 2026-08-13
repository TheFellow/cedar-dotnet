using System.Collections;
using System.Collections.Generic;

namespace Cedar.Core.Internal.MapSet;

internal sealed class ImmutableMapSet<T> : IReadOnlyCollection<T>
    where T : notnull
{
    private readonly HashSet<T> _items;

    public ImmutableMapSet()
    {
        _items = [];
    }

    internal ImmutableMapSet(HashSet<T> items)
    {
        _items = items;
    }

    public int Count => _items.Count;

    public bool Contains(T item)
    {
        return _items.Contains(item);
    }

    public bool Equal(ImmutableMapSet<T>? other)
    {
        if (other is null || Count != other.Count)
        {
            return false;
        }

        foreach (T item in _items)
        {
            if (!other.Contains(item))
            {
                return false;
            }
        }

        return true;
    }

    public ulong GetXorHashCode()
    {
        ulong combined = 0;
        foreach (T item in _items)
        {
            combined ^= unchecked((uint)item.GetHashCode());
        }

        return combined;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

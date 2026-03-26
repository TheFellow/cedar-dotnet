using System.Collections.Generic;

namespace Cedar.Core.Internal.MapSet;

internal sealed class MapSetBuilder<T>
    where T : notnull
{
    private readonly HashSet<T> _items;

    public MapSetBuilder()
    {
        _items = [];
    }

    public MapSetBuilder(IEnumerable<T> items)
    {
        _items = [.. items];
    }

    public int Count => _items.Count;

    public bool Add(T item)
    {
        return _items.Add(item);
    }

    public ImmutableMapSet<T> Build()
    {
        return new ImmutableMapSet<T>([.. _items]);
    }
}

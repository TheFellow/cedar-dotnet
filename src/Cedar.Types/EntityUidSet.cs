using System.Collections;
using System.Collections.Generic;
using Cedar.Core.Internal.MapSet;

namespace Cedar.Types;

public sealed record EntityUidSet : IReadOnlyCollection<EntityUid>
{
    private readonly ImmutableMapSet<EntityUid> _items;

    public EntityUidSet()
        : this(System.Array.Empty<EntityUid>())
    {
    }

    public EntityUidSet(IEnumerable<EntityUid> items)
    {
        MapSetBuilder<EntityUid> builder = new(items);
        _items = builder.Build();
    }

    public int Count => _items.Count;

    public bool Contains(EntityUid uid)
    {
        return _items.Contains(uid);
    }

    public bool Equals(EntityUidSet? other)
    {
        return other is not null && _items.Equal(other._items);
    }

    public override int GetHashCode()
    {
        int[] hashes = new int[Count];
        int index = 0;
        foreach (EntityUid item in this)
        {
            hashes[index++] = item.GetHashCode();
        }
        return CedarHash.ForXorCollection(nameof(EntityUidSet), hashes);
    }

    public IEnumerator<EntityUid> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

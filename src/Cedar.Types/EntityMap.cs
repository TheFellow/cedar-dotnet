using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Cedar.Types;

public sealed class EntityMap : IEntityGetter, IReadOnlyCollection<Entity>
{
    private readonly Dictionary<EntityUid, Entity> _entities;
    private readonly Entity[] _orderedEntities;

    public EntityMap()
        : this((IEnumerable<Entity>?)null)
    {
    }

    public EntityMap(IEnumerable<Entity>? entities)
    {
        _entities = [];

        if (entities is not null)
        {
            foreach (Entity entity in entities)
            {
                ArgumentNullException.ThrowIfNull(entity);
                _entities[entity.Uid] = entity;
            }
        }

        _orderedEntities = [.. _entities.Values.OrderBy(static entity => entity.Uid.ToString(), StringComparer.Ordinal)];
    }

    public int Count => _orderedEntities.Length;

    public bool TryGet(EntityUid uid, out Entity entity)
    {
        return _entities.TryGetValue(uid, out entity!);
    }

    public IEnumerator<Entity> GetEnumerator()
    {
        return ((IEnumerable<Entity>)_orderedEntities).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

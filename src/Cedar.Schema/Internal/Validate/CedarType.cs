using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Cedar.Types;

namespace Cedar.Schema.Internal.Validate;

internal abstract record CedarType;

internal sealed record CedarNever : CedarType;

internal sealed record CedarTrue : CedarType;

internal sealed record CedarFalse : CedarType;

internal sealed record CedarBool : CedarType;

internal sealed record CedarLong : CedarType;

internal sealed record CedarString : CedarType;

internal sealed record CedarSetType(CedarType Element) : CedarType;

internal sealed record CedarExtType(Ident Name) : CedarType;

internal sealed record CedarEntityType(EntityLub Lub) : CedarType;

internal sealed record CedarRecordType(
    IReadOnlyDictionary<string, CedarAttr> Attrs,
    EntityAttrSource? Source = null) : CedarType;

internal readonly record struct CedarAttr(CedarType Type, bool Required);

internal sealed record EntityAttrSource(EntityLub Lub, string Attr);

internal sealed class EntityLub : IEquatable<EntityLub>
{
    public EntityLub(ImmutableArray<EntityType> elements)
    {
        Elements = elements
            .Distinct()
            .OrderBy(static entityType => entityType.Value, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public ImmutableArray<EntityType> Elements { get; }

    public static EntityLub Single(EntityType entityType)
    {
        return new EntityLub(ImmutableArray.Create(entityType));
    }

    public EntityLub Union(EntityLub other)
    {
        return new EntityLub(Elements.AddRange(other.Elements));
    }

    public bool IsDisjoint(EntityLub other)
    {
        int left = 0;
        int right = 0;

        while (left < Elements.Length && right < other.Elements.Length)
        {
            int comparison = StringComparer.Ordinal.Compare(Elements[left].Value, other.Elements[right].Value);
            if (comparison == 0)
            {
                return false;
            }

            if (comparison < 0)
            {
                left++;
            }
            else
            {
                right++;
            }
        }

        return true;
    }

    public bool Equals(EntityLub? other)
    {
        if (other is null)
        {
            return false;
        }

        return Elements.SequenceEqual(other.Elements);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as EntityLub);
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (EntityType entityType in Elements)
        {
            hash.Add(entityType);
        }

        return hash.ToHashCode();
    }
}

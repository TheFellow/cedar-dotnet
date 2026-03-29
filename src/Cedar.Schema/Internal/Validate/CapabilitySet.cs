using System.Collections.Generic;

namespace Cedar.Schema.Internal.Validate;

internal readonly record struct Capability(string VarName, string Attr);

internal sealed class CapabilitySet
{
    private readonly HashSet<Capability> _caps;

    private CapabilitySet(HashSet<Capability> caps)
    {
        _caps = caps;
    }

    public static CapabilitySet Create()
    {
        return new CapabilitySet([]);
    }

    public CapabilitySet Clone()
    {
        return new CapabilitySet([.. _caps]);
    }

    public CapabilitySet Add(Capability capability)
    {
        CapabilitySet result = Clone();
        result._caps.Add(capability);
        return result;
    }

    public bool Has(Capability capability)
    {
        return _caps.Contains(capability);
    }

    public CapabilitySet Merge(CapabilitySet other)
    {
        CapabilitySet result = Clone();
        result._caps.UnionWith(other._caps);
        return result;
    }

    public CapabilitySet Intersect(CapabilitySet other)
    {
        HashSet<Capability> result = [.. _caps];
        result.IntersectWith(other._caps);
        return new CapabilitySet(result);
    }
}

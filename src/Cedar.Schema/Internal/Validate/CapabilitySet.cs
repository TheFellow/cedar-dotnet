using System.Collections.Immutable;

namespace Cedar.Schema.Internal.Validate;

internal readonly record struct Capability(string VarName, string Attr);

internal sealed class CapabilitySet
{
    private readonly ImmutableHashSet<Capability> _caps;

    private CapabilitySet(ImmutableHashSet<Capability> caps)
    {
        _caps = caps;
    }

    public static CapabilitySet Create()
    {
        return new CapabilitySet(ImmutableHashSet<Capability>.Empty);
    }

    public CapabilitySet Clone()
    {
        return new CapabilitySet(_caps);
    }

    public CapabilitySet Add(Capability capability)
    {
        return new CapabilitySet(_caps.Add(capability));
    }

    public bool Has(Capability capability)
    {
        return _caps.Contains(capability);
    }

    public CapabilitySet Merge(CapabilitySet other)
    {
        return new CapabilitySet(_caps.Union(other._caps));
    }

    public CapabilitySet Intersect(CapabilitySet other)
    {
        return new CapabilitySet(_caps.Intersect(other._caps));
    }
}

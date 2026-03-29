using Cedar.Schema.Internal.Validate;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class CapabilitySetTests
{
    [Fact]
    public void Create_StartsEmpty()
    {
        Capability capability = new("context", "token");

        Assert.False(CapabilitySet.Create().Has(capability));
    }

    [Fact]
    public void Add_ReturnsNewSetWithoutMutatingOriginal()
    {
        Capability capability = new("context", "token");
        CapabilitySet original = CapabilitySet.Create();

        CapabilitySet updated = original.Add(capability);

        Assert.False(original.Has(capability));
        Assert.True(updated.Has(capability));
    }

    [Fact]
    public void Add_UsesCapabilityValueSemantics()
    {
        CapabilitySet capabilities = CapabilitySet.Create().Add(new Capability("principal", "manager"));

        Assert.True(capabilities.Has(new Capability("principal", "manager")));
    }

    [Fact]
    public void Clone_CopiesCapabilitiesIndependently()
    {
        CapabilitySet original = CapabilitySet.Create().Add(new Capability("context", "token"));
        CapabilitySet clone = original.Clone();

        clone = clone.Add(new Capability("principal", "manager"));

        Assert.True(original.Has(new Capability("context", "token")));
        Assert.False(original.Has(new Capability("principal", "manager")));
        Assert.True(clone.Has(new Capability("principal", "manager")));
    }

    [Fact]
    public void Merge_UnionsCapabilities()
    {
        CapabilitySet left = CapabilitySet.Create().Add(new Capability("context", "token"));
        CapabilitySet right = CapabilitySet.Create().Add(new Capability("principal", "manager"));

        CapabilitySet merged = left.Merge(right);

        Assert.True(merged.Has(new Capability("context", "token")));
        Assert.True(merged.Has(new Capability("principal", "manager")));
    }

    [Fact]
    public void Merge_WithEmptySetPreservesExistingCapabilities()
    {
        CapabilitySet capabilities = CapabilitySet.Create().Add(new Capability("context", "token"));

        CapabilitySet merged = capabilities.Merge(CapabilitySet.Create());

        Assert.True(merged.Has(new Capability("context", "token")));
    }

    [Fact]
    public void Intersect_KeepsOnlySharedCapabilities()
    {
        Capability shared = new("context", "token");
        CapabilitySet left = CapabilitySet.Create().Add(shared).Add(new Capability("principal", "manager"));
        CapabilitySet right = CapabilitySet.Create().Add(shared).Add(new Capability("resource", "owner"));

        CapabilitySet intersection = left.Intersect(right);

        Assert.True(intersection.Has(shared));
        Assert.False(intersection.Has(new Capability("principal", "manager")));
        Assert.False(intersection.Has(new Capability("resource", "owner")));
    }

    [Fact]
    public void Intersect_WithNoOverlapReturnsEmptySet()
    {
        CapabilitySet left = CapabilitySet.Create().Add(new Capability("context", "token"));
        CapabilitySet right = CapabilitySet.Create().Add(new Capability("principal", "manager"));

        CapabilitySet intersection = left.Intersect(right);

        Assert.False(intersection.Has(new Capability("context", "token")));
        Assert.False(intersection.Has(new Capability("principal", "manager")));
    }
}

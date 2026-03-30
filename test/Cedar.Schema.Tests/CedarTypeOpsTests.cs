using System.Collections.Generic;
using System.Collections.Immutable;
using Cedar.Schema.Internal.Validate;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class CedarTypeOpsTests
{
    [Fact]
    public void EntityLub_UsesStructuralEquality()
    {
        EntityLub left = new(ImmutableArray.Create(new EntityType("B"), new EntityType("A")));
        EntityLub right = new(ImmutableArray.Create(new EntityType("A"), new EntityType("B")));

        Assert.Equal(left, right);
        Assert.Contains(new EntityType("C"), left.Union(EntityLub.Single(new EntityType("C"))).Elements);
    }

    [Fact]
    public void LeastUpperBound_MergesBooleanSingletons()
    {
        (CedarType? type, string? error) = CedarTypeOps.LeastUpperBound(CedarTrueType.Instance, CedarFalseType.Instance, strict: true);

        Assert.Null(error);
        Assert.IsType<CedarBoolType>(type);
    }

    [Fact]
    public void LeastUpperBound_StrictRecordMismatchFails()
    {
        CedarRecordType left = new(new Dictionary<string, CedarAttr> { ["a"] = new(CedarLongType.Instance, true) });
        CedarRecordType right = new(new Dictionary<string, CedarAttr> { ["b"] = new(CedarLongType.Instance, true) });

        (CedarType? _, string? error) = CedarTypeOps.LubRecord(left, right, strict: true);

        Assert.NotNull(error);
    }
}

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Cedar.Schema.Internal.Validate;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class CedarTypeTests
{
    public static IEnumerable<object[]> CedarTypeNameCases()
    {
        yield return [CedarNeverType.Instance, "__cedar::internal::Never"];
        yield return [CedarTrueType.Instance, "__cedar::internal::True"];
        yield return [CedarFalseType.Instance, "__cedar::internal::False"];
        yield return [CedarBoolType.Instance, "Bool"];
        yield return [CedarLongType.Instance, "Long"];
        yield return [CedarStringType.Instance, "String"];
        yield return [new CedarSetType(CedarLongType.Instance), "Set<Long>"];
        yield return [new CedarExtType(new Ident("ipaddr")), "ipaddr"];
        yield return [new CedarEntityType(EntityLub.Single(new EntityType("User"))), "User"];
        yield return
        [
            new CedarRecordType(
                new Dictionary<string, CedarAttr>
                {
                    ["name"] = new(CedarStringType.Instance, false),
                    ["age"] = new(CedarLongType.Instance, true)
                }),
            "{age: Long,name?: String,}"
        ];
    }

    [Fact]
    public void EntityLub_SingleWrapsEntityType()
    {
        EntityLub lub = EntityLub.Single(new EntityType("User"));

        Assert.Equal(["User"], lub.Elements.Select(static entityType => entityType.Value).ToArray());
    }

    [Fact]
    public void EntityLub_UnionDeduplicatesAndSorts()
    {
        EntityLub left = new(ImmutableArray.Create(new EntityType("User"), new EntityType("Admin")));
        EntityLub right = new(ImmutableArray.Create(new EntityType("Team"), new EntityType("User")));

        EntityLub union = left.Union(right);

        Assert.Equal(["Admin", "Team", "User"], union.Elements.Select(static entityType => entityType.Value).ToArray());
    }

    [Fact]
    public void EntityLub_IsDisjointReturnsTrueForDistinctSets()
    {
        EntityLub left = new(ImmutableArray.Create(new EntityType("Admin")));
        EntityLub right = new(ImmutableArray.Create(new EntityType("User"), new EntityType("Team")));

        Assert.True(left.IsDisjoint(right));
    }

    [Fact]
    public void EntityLub_IsDisjointReturnsFalseForOverlap()
    {
        EntityLub left = new(ImmutableArray.Create(new EntityType("Admin"), new EntityType("User")));
        EntityLub right = new(ImmutableArray.Create(new EntityType("User"), new EntityType("Team")));

        Assert.False(left.IsDisjoint(right));
    }

    [Fact]
    public void EntityLub_UsesStructuralEqualityAndHashCode()
    {
        EntityLub left = new(ImmutableArray.Create(new EntityType("Team"), new EntityType("User")));
        EntityLub right = new(ImmutableArray.Create(new EntityType("User"), new EntityType("Team")));

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(CedarTypeNameCases))]
    public void CedarTypeName_ReturnsExpectedNames(object type, string expected)
    {
        Assert.Equal(expected, CedarTypeOps.CedarTypeName(Assert.IsAssignableFrom<CedarType>(type)));
    }

    [Fact]
    public void CedarTypeKindRank_OrdersEveryVariant()
    {
        CedarType[] types =
        [
            CedarTrueType.Instance,
            CedarFalseType.Instance,
            CedarBoolType.Instance,
            CedarNeverType.Instance,
            CedarLongType.Instance,
            CedarStringType.Instance,
            new CedarSetType(CedarLongType.Instance),
            new CedarRecordType(new Dictionary<string, CedarAttr>()),
            new CedarEntityType(EntityLub.Single(new EntityType("User"))),
            new CedarExtType(new Ident("ipaddr"))
        ];

        for (int index = 0; index < types.Length; index++)
        {
            Assert.Equal(index, CedarTypeOps.CedarTypeKindRank(types[index]));
        }
    }

    [Fact]
    public void CompareCedarType_OrdersKindsBeforeStructure()
    {
        int comparison = CedarTypeOps.CompareCedarType(CedarTrueType.Instance, CedarLongType.Instance);

        Assert.True(comparison < 0);
    }

    [Fact]
    public void CompareCedarType_OrdersSetTypesByElementType()
    {
        int comparison = CedarTypeOps.CompareCedarType(
            new CedarSetType(CedarLongType.Instance),
            new CedarSetType(CedarStringType.Instance));

        Assert.True(comparison < 0);
    }

    [Fact]
    public void CompareCedarType_OrdersRecordTypesByAttributeNameThenType()
    {
        CedarRecordType left = new(
            new Dictionary<string, CedarAttr>
            {
                ["a"] = new(CedarLongType.Instance, true)
            });
        CedarRecordType right = new(
            new Dictionary<string, CedarAttr>
            {
                ["b"] = new(CedarLongType.Instance, true)
            });

        int comparison = CedarTypeOps.CompareCedarType(left, right);

        Assert.True(comparison < 0);
    }

    [Fact]
    public void CompareCedarType_OrdersEntityLubsStructurally()
    {
        CedarEntityType left = new(EntityLub.Single(new EntityType("Admin")));
        CedarEntityType right = new(new EntityLub(ImmutableArray.Create(new EntityType("Admin"), new EntityType("User"))));

        int comparison = CedarTypeOps.CompareCedarType(left, right);

        Assert.True(comparison < 0);
    }
}

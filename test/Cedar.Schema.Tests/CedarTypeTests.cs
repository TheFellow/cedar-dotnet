using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Cedar.Schema.Internal.Validate;
using Cedar.Types;
using Xunit;
using SchemaBoolType = Cedar.Schema.Internal.Validate.CedarBool;
using SchemaLongType = Cedar.Schema.Internal.Validate.CedarLong;
using SchemaStringType = Cedar.Schema.Internal.Validate.CedarString;

namespace Cedar.Schema.Tests;

public sealed class CedarTypeTests
{
    public static IEnumerable<object[]> CedarTypeNameCases()
    {
        yield return [new CedarNever(), "__cedar::internal::Never"];
        yield return [new CedarTrue(), "__cedar::internal::True"];
        yield return [new CedarFalse(), "__cedar::internal::False"];
        yield return [new SchemaBoolType(), "Bool"];
        yield return [new SchemaLongType(), "Long"];
        yield return [new SchemaStringType(), "String"];
        yield return [new CedarSetType(new SchemaLongType()), "Set<Long>"];
        yield return [new CedarExtType(new Ident("ipaddr")), "ipaddr"];
        yield return [new CedarEntityType(EntityLub.Single(new EntityType("User"))), "User"];
        yield return
        [
            new CedarRecordType(
                new Dictionary<string, CedarAttr>
                {
                    ["name"] = new(new SchemaStringType(), false),
                    ["age"] = new(new SchemaLongType(), true)
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
            new CedarTrue(),
            new CedarFalse(),
            new SchemaBoolType(),
            new CedarNever(),
            new SchemaLongType(),
            new SchemaStringType(),
            new CedarSetType(new SchemaLongType()),
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
        int comparison = CedarTypeOps.CompareCedarType(new CedarTrue(), new SchemaLongType());

        Assert.True(comparison < 0);
    }

    [Fact]
    public void CompareCedarType_OrdersSetTypesByElementType()
    {
        int comparison = CedarTypeOps.CompareCedarType(
            new CedarSetType(new SchemaLongType()),
            new CedarSetType(new SchemaStringType()));

        Assert.True(comparison < 0);
    }

    [Fact]
    public void CompareCedarType_OrdersRecordTypesByAttributeNameThenType()
    {
        CedarRecordType left = new(
            new Dictionary<string, CedarAttr>
            {
                ["a"] = new(new SchemaLongType(), true)
            });
        CedarRecordType right = new(
            new Dictionary<string, CedarAttr>
            {
                ["b"] = new(new SchemaLongType(), true)
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

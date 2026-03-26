using System.Linq;
using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class EntityMapTests
{
    [Fact]
    public void EmptyMapHasZeroCount()
    {
        Assert.Empty(new EntityMap());
    }

    [Fact]
    public void TryGetFindsStoredEntity()
    {
        Entity entity = CreateEntity("alice");
        EntityMap map = new([entity]);

        Assert.True(map.TryGet(entity.Uid, out Entity actual));
        Assert.Equal(entity, actual);
    }

    [Fact]
    public void TryGetReturnsFalseForMissingUid()
    {
        EntityMap map = new();

        Assert.False(map.TryGet(new EntityUid(new EntityType("User"), new CedarString("alice")), out _));
    }

    [Fact]
    public void DuplicateUidsUseLastEntity()
    {
        EntityMap map = new([CreateEntity("alice"), CreateEntity("alice") with { Tags = new CedarRecord(new RecordMap { [new CedarString("team")] = new CedarString("platform") }) }]);

        Assert.True(map.TryGet(new EntityUid(new EntityType("User"), new CedarString("alice")), out Entity actual));
        Assert.Equal("platform", ((CedarString)actual.Tags[new CedarString("team")]).Value);
    }

    [Fact]
    public void EnumerationIsSortedByUid()
    {
        EntityMap map = new([CreateEntity("bob"), CreateEntity("alice")]);

        Assert.Equal(["User::\"alice\"", "User::\"bob\""], map.Select(entity => entity.Uid.ToString()).ToArray());
    }

    [Fact]
    public void JsonSerializeUsesEntityArray()
    {
        EntityMap map = new([CreateEntity("alice"), CreateEntity("bob")]);

        string json = CedarJson.SerializeEntityMap(map);

        Assert.StartsWith("[", json);
        Assert.Contains("\"uid\":{\"type\":\"User\",\"id\":\"alice\"}", json);
        Assert.Contains("\"uid\":{\"type\":\"User\",\"id\":\"bob\"}", json);
    }

    [Fact]
    public void JsonDeserializeRoundTripsEntityMap()
    {
        EntityMap expected = new([CreateEntity("alice"), CreateEntity("bob")]);

        EntityMap actual = CedarJson.DeserializeEntityMap(CedarJson.SerializeEntityMap(expected));

        Assert.Equal(expected.Count, actual.Count);
        Assert.True(actual.TryGet(new EntityUid(new EntityType("User"), new CedarString("alice")), out _));
        Assert.True(actual.TryGet(new EntityUid(new EntityType("User"), new CedarString("bob")), out _));
    }

    [Fact]
    public void JsonDeserializePreservesTwoEntities()
    {
        EntityMap actual = CedarJson.DeserializeEntityMap("[{\"uid\":{\"type\":\"User\",\"id\":\"alice\"},\"parents\":[],\"attrs\":{},\"tags\":{}},{\"uid\":{\"type\":\"User\",\"id\":\"bob\"},\"parents\":[],\"attrs\":{},\"tags\":{}}]");

        Assert.Equal(2, actual.Count);
    }

    [Fact]
    public void EntityGetterInterfaceDelegatesToMap()
    {
        IEntityGetter getter = new EntityMap([CreateEntity("alice")]);

        Assert.True(getter.TryGet(new EntityUid(new EntityType("User"), new CedarString("alice")), out _));
    }

    [Fact]
    public void RoundTripPreservesNestedAttributesAndTags()
    {
        Entity entity = CreateEntity("alice") with
        {
            Attributes = new CedarRecord(new RecordMap
            {
                [new CedarString("context")] = new CedarRecord(new RecordMap
                {
                    [new CedarString("ip")] = CedarIpAddress.Parse("10.0.0.1")
                })
            })
        };
        EntityMap expected = new([entity]);

        EntityMap actual = CedarJson.DeserializeEntityMap(CedarJson.SerializeEntityMap(expected));

        Assert.True(actual.TryGet(entity.Uid, out Entity actualEntity));
        Assert.Equal(entity, actualEntity);
    }

    [Fact]
    public void EmptyMapSerializesToEmptyArray()
    {
        Assert.Equal("[]", CedarJson.SerializeEntityMap(new EntityMap()));
    }

    [Fact]
    public void ParentReferencesSurviveRoundTrip()
    {
        Entity alice = CreateEntity("alice");
        Entity child = CreateEntity("child") with
        {
            Parents = new EntityUidSet([alice.Uid])
        };
        EntityMap expected = new([alice, child]);

        EntityMap actual = CedarJson.DeserializeEntityMap(CedarJson.SerializeEntityMap(expected));

        Assert.True(actual.TryGet(child.Uid, out Entity actualChild));
        Assert.True(actualChild.Parents.Contains(alice.Uid));
    }

    private static Entity CreateEntity(string id)
    {
        return new(
            new EntityUid(new EntityType("User"), new CedarString(id)),
            new EntityUidSet(),
            new CedarRecord(new RecordMap
            {
                [new CedarString("active")] = new CedarBool(true)
            }),
            new CedarRecord(new RecordMap
            {
                [new CedarString("team")] = new CedarString("infra")
            }));
    }
}

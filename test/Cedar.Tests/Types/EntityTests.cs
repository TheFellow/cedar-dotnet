using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class EntityTests
{
    [Fact]
    public void ConstructorStoresAllComponents()
    {
        Entity entity = CreateEntity();

        Assert.Equal(new EntityUid(new EntityType("User"), new CedarString("alice")), entity.Uid);
        Assert.Equal(2, entity.Parents.Count);
        Assert.Equal(2, entity.Attributes.Count);
        Assert.Equal(1, entity.Tags.Count);
    }

    [Fact]
    public void EqualEntitiesCompareEqual()
    {
        Assert.Equal(CreateEntity(), CreateEntity());
    }

    [Fact]
    public void DifferentUidsAreNotEqual()
    {
        Assert.NotEqual(CreateEntity(), CreateEntity() with
        {
            Uid = new EntityUid(new EntityType("User"), new CedarString("bob"))
        });
    }

    [Fact]
    public void DifferentParentsAreNotEqual()
    {
        Assert.NotEqual(CreateEntity(), CreateEntity() with
        {
            Parents = new EntityUidSet([new EntityUid(new EntityType("Group"), new CedarString("ops"))])
        });
    }

    [Fact]
    public void DifferentAttributesAreNotEqual()
    {
        Assert.NotEqual(CreateEntity(), CreateEntity() with
        {
            Attributes = new CedarRecord(new RecordMap
            {
                [new CedarString("active")] = new CedarBool(false)
            })
        });
    }

    [Fact]
    public void DifferentTagsAreNotEqual()
    {
        Assert.NotEqual(CreateEntity(), CreateEntity() with
        {
            Tags = new CedarRecord(new RecordMap
            {
                [new CedarString("team")] = new CedarString("platform")
            })
        });
    }

    [Fact]
    public void JsonSerializeUsesImplicitUidForms()
    {
        string json = CedarJson.SerializeEntity(CreateEntity());

        Assert.Contains("\"uid\":{\"type\":\"User\",\"id\":\"alice\"}", json);
        Assert.Contains("\"parents\":[{\"type\":\"Group\",\"id\":\"dev\"},{\"type\":\"Group\",\"id\":\"ops\"}]", json);
    }

    [Fact]
    public void JsonDeserializeRoundTripsEntity()
    {
        Entity expected = CreateEntity();

        Entity actual = CedarJson.DeserializeEntity(CedarJson.SerializeEntity(expected));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JsonTagsSupportEntityUidAndExtensionValues()
    {
        Entity entity = new(
            new EntityUid(new EntityType("User"), new CedarString("alice")),
            new EntityUidSet(),
            new CedarRecord(),
            new CedarRecord(new RecordMap
            {
                [new CedarString("owner")] = new EntityUid(new EntityType("User"), new CedarString("alice")),
                [new CedarString("expires")] = CedarDatetime.Parse("1970-01-01T00:00:00.042Z")
            }));

        Entity actual = CedarJson.DeserializeEntity(CedarJson.SerializeEntity(entity));

        Assert.Equal(entity, actual);
    }

    [Fact]
    public void MissingParentsDefaultToEmpty()
    {
        Entity entity = CedarJson.DeserializeEntity("{\"uid\":{\"type\":\"User\",\"id\":\"alice\"},\"attrs\":{},\"tags\":{}}");

        Assert.Empty(entity.Parents);
    }

    [Fact]
    public void MissingAttrsAndTagsDefaultToEmpty()
    {
        Entity entity = CedarJson.DeserializeEntity("{\"uid\":{\"type\":\"User\",\"id\":\"alice\"},\"parents\":[]}");

        Assert.Empty(entity.Attributes);
        Assert.Empty(entity.Tags);
    }

    [Fact]
    public void AttributesAndTagsRemainIndependent()
    {
        Entity entity = CreateEntity();

        Assert.NotEqual(entity.Attributes, entity.Tags);
    }

    private static Entity CreateEntity()
    {
        return new(
            new EntityUid(new EntityType("User"), new CedarString("alice")),
            new EntityUidSet(
            [
                new EntityUid(new EntityType("Group"), new CedarString("ops")),
                new EntityUid(new EntityType("Group"), new CedarString("dev"))
            ]),
            new CedarRecord(new RecordMap
            {
                [new CedarString("active")] = new CedarBool(true),
                [new CedarString("age")] = new CedarLong(42)
            }),
            new CedarRecord(new RecordMap
            {
                [new CedarString("team")] = new CedarString("infra")
            }));
    }
}

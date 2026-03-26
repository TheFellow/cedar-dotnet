using Cedar.Core.Internal.Json;
using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class EntityUidTests
{
    [Fact]
    public void ConstructorStoresTypeAndId()
    {
        EntityUid uid = new(new EntityType("User"), new CedarString("alice"));

        Assert.Equal("User", uid.Type.Value);
        Assert.Equal("alice", uid.Id.Value);
    }

    [Fact]
    public void MarshalCedarFormatsSimpleUid()
    {
        EntityUid uid = new(new EntityType("User"), new CedarString("alice"));

        Assert.Equal("User::\"alice\"", uid.MarshalCedar());
    }

    [Fact]
    public void MarshalCedarEscapesIdentifierText()
    {
        EntityUid uid = new(new EntityType("User"), new CedarString("a\"b"));

        Assert.Equal("User::\"a\\\"b\"", uid.MarshalCedar());
    }

    [Fact]
    public void EqualValuesAreEqual()
    {
        Assert.Equal(new EntityUid(new EntityType("User"), new CedarString("alice")), new EntityUid(new EntityType("User"), new CedarString("alice")));
    }

    [Fact]
    public void DifferentTypesAreNotEqual()
    {
        Assert.NotEqual(new EntityUid(new EntityType("User"), new CedarString("alice")), new EntityUid(new EntityType("Action"), new CedarString("alice")));
    }

    [Fact]
    public void HashCodeIsStable()
    {
        EntityUid uid = new(new EntityType("User"), new CedarString("alice"));

        Assert.Equal(uid.GetHashCode(), uid.GetHashCode());
    }

    [Fact]
    public void DeserializeReadsExplicitEntityForm()
    {
        EntityUid uid = CedarJson.DeserializeEntityUid("{\"__entity\":{\"type\":\"User\",\"id\":\"alice\"}}");

        Assert.Equal(new EntityUid(new EntityType("User"), new CedarString("alice")), uid);
    }

    [Fact]
    public void DeserializeReadsImplicitEntityForm()
    {
        EntityUid uid = CedarJson.DeserializeEntityUid("{\"type\":\"User\",\"id\":\"alice\"}");

        Assert.Equal(new EntityUid(new EntityType("User"), new CedarString("alice")), uid);
    }

    [Fact]
    public void SerializeUsesExplicitEntityForm()
    {
        EntityUid uid = new(new EntityType("User"), new CedarString("alice"));

        Assert.Equal("{\"__entity\":{\"type\":\"User\",\"id\":\"alice\"}}", CedarJson.SerializeEntityUid(uid));
    }

    [Fact]
    public void ValueConverterReadsEntityUidIntoCedarData()
    {
        ICedarData actual = CedarJson.DeserializeData("{\"__entity\":{\"type\":\"User\",\"id\":\"alice\"}}");

        Assert.Equal(new EntityUid(new EntityType("User"), new CedarString("alice")), Assert.IsType<EntityUid>(actual));
    }

    [Fact]
    public void EntityUidCanRoundTripInsideSet()
    {
        EntityUid uid = new(new EntityType("User"), new CedarString("alice"));
        CedarSet set = new(uid);

        ICedarData actual = CedarJson.DeserializeData(CedarJson.SerializeData(set));

        CedarAssert.Equal(set, Assert.IsType<CedarSet>(actual));
    }

    [Fact]
    public void ToStringMatchesMarshalCedar()
    {
        EntityUid uid = new(new EntityType("User"), new CedarString("alice"));

        Assert.Equal(uid.MarshalCedar(), uid.ToString());
    }
}

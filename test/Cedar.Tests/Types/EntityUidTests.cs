using System;
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

    [Theory]
    [InlineData("\u0007", @"T::""\u{7}""")]
    [InlineData("\u0008", @"T::""\u{8}""")]
    [InlineData("\u000C", @"T::""\u{c}""")]
    [InlineData("\u000B", @"T::""\u{b}""")]
    public void MarshalCedarEscapesControlCharsWithCedarSyntax(string id, string expected)
    {
        EntityUid uid = new(new EntityType("T"), new CedarString(id));

        Assert.Equal(expected, uid.MarshalCedar());
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
    public void ValueConverterReadsImplicitEntityFormAsRecord()
    {
        ICedarData actual = CedarJson.DeserializeData("{\"type\":\"User\",\"id\":\"alice\"}");

        CedarRecord record = Assert.IsType<CedarRecord>(actual);
        Assert.Equal(new CedarString("User"), Assert.IsType<CedarString>(record[new CedarString("type")]));
        Assert.Equal(new CedarString("alice"), Assert.IsType<CedarString>(record[new CedarString("id")]));
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

    [Fact]
    public void TryParseCedar_SimpleUid()
    {
        Assert.True(EntityUid.TryParseCedar("User::\"alice\"", out EntityUid? result));
        Assert.Equal(new EntityUid(new EntityType("User"), new CedarString("alice")), result);
    }

    [Fact]
    public void TryParseCedar_NamespacedType()
    {
        Assert.True(EntityUid.TryParseCedar("Namespace::Type::\"id\"", out EntityUid? result));
        Assert.Equal("Namespace::Type", result!.Type.Value);
        Assert.Equal("id", result.Id.Value);
    }

    [Fact]
    public void TryParseCedar_IdContainingDoubleColon()
    {
        Assert.True(EntityUid.TryParseCedar("X::Y::\"asdf::\"", out EntityUid? result));
        Assert.Equal("X::Y", result!.Type.Value);
        Assert.Equal("asdf::", result.Id.Value);
    }

    [Theory]
    [InlineData("User::\"alice\"")]
    [InlineData("Namespace::Type::\"id\"")]
    [InlineData("X::Y::\"asdf::\"")]
    [InlineData("Search::Algorithm::\"A*\"")]
    [InlineData("Super::\"*\"")]
    [InlineData("namespace::type::\"\"")]
    public void TryParseCedar_RoundTrip(string input)
    {
        Assert.True(EntityUid.TryParseCedar(input, out EntityUid? parsed));
        Assert.True(EntityUid.TryParseCedar(parsed!.MarshalCedar(), out EntityUid? reparsed));
        Assert.Equal(parsed, reparsed);
    }

    [Theory]
    [InlineData("Type::id")]
    [InlineData("Type\"id\"")]
    [InlineData("Type::id\"")]
    [InlineData("Type::\"id")]
    [InlineData("")]
    [InlineData("\"id\"")]
    [InlineData("::\"id\"")]
    public void TryParseCedar_RejectsInvalidInput(string input)
    {
        Assert.False(EntityUid.TryParseCedar(input, out EntityUid? result));
        Assert.Null(result);
    }

    [Fact]
    public void ParseCedar_ThrowsOnInvalidInput()
    {
        Assert.Throws<FormatException>(() => EntityUid.ParseCedar("Type::id"));
    }
}

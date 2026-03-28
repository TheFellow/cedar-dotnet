using System.Linq;
using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class CedarSetTests
{
    [Fact]
    public void EmptySetHasZeroCount()
    {
        Assert.Equal(0, new CedarSet().Count);
    }

    [Fact]
    public void ConstructorDeduplicatesEqualValues()
    {
        CedarSet set = new(new CedarLong(42), new CedarLong(42));

        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void ContainsFindsPrimitiveValues()
    {
        CedarSet set = new(new CedarLong(42), new CedarBool(true));

        Assert.True(set.Contains(new CedarLong(42)));
    }

    [Fact]
    public void ContainsFindsEntityUids()
    {
        EntityUid uid = new(new EntityType("User"), new CedarString("alice"));
        CedarSet set = new(uid);

        Assert.True(set.Contains(uid));
    }

    [Fact]
    public void ContainsUsesStructuralEqualityRatherThanOnlyHashCode()
    {
        CedarSet set = new(CedarDecimal.Parse("0.0"));

        Assert.False(set.Contains(new CedarLong(0)));
    }

    [Fact]
    public void EqualityIsOrderIndependent()
    {
        CedarAssert.Equal(new CedarSet(new CedarLong(1), new CedarLong(2)), new CedarSet(new CedarLong(2), new CedarLong(1)));
    }

    [Fact]
    public void DifferentMembersAreNotEqual()
    {
        CedarAssert.NotEqual(new CedarSet(new CedarLong(1)), new CedarSet(new CedarLong(2)));
    }

    [Fact]
    public void HashCodeIsStable()
    {
        CedarAssert.HashStable(new CedarSet(new CedarLong(1), new CedarLong(2)));
    }

    [Fact]
    public void MarshalCedarFormatsSingleElementSet()
    {
        CedarAssert.CedarText(new CedarSet(new CedarLong(1)), "[1]");
    }

    [Fact]
    public void EnumerationReturnsUniqueMembers()
    {
        CedarSet set = new(new CedarLong(1), new CedarLong(1), new CedarLong(2));

        Assert.Equal(2, set.Count());
    }

    [Fact]
    public void JsonRoundTripSupportsPrimitiveMembers()
    {
        CedarSet expected = new(new CedarLong(1), new CedarBool(true));

        ICedarData actual = CedarJson.DeserializeData(CedarJson.SerializeData(expected));

        CedarAssert.Equal(expected, Assert.IsType<CedarSet>(actual));
    }

    [Fact]
    public void JsonRoundTripSupportsEntityMembers()
    {
        EntityUid uid = new(new EntityType("User"), new CedarString("alice"));
        CedarSet expected = new(uid, new CedarString("member"));

        ICedarData actual = CedarJson.DeserializeData(CedarJson.SerializeData(expected));

        CedarAssert.Equal(expected, Assert.IsType<CedarSet>(actual));
    }

    [Fact]
    public void JsonSerializesEmptySetAsEmptyArray()
    {
        string actual = CedarJson.SerializeData(new CedarSet());

        Assert.Equal("[]", actual);
    }

    [Fact]
    public void JsonSerializesSingleElementSet()
    {
        CedarSet set = new(new CedarLong(1));

        string actual = CedarJson.SerializeData(set);

        Assert.Equal("[1]", actual);
    }

    [Fact]
    public void JsonSerializesIntegerSetInLexicographicJsonOrder()
    {
        CedarSet set = new(new CedarLong(3), new CedarLong(2), new CedarLong(1));

        string actual = CedarJson.SerializeData(set);

        Assert.Equal("[1,2,3]", actual);
    }

    [Fact]
    public void JsonSerializesStringSetInLexicographicJsonOrder()
    {
        CedarSet set = new(new CedarString("3"), new CedarString("1"), new CedarString("2"));

        string actual = CedarJson.SerializeData(set);

        Assert.Equal("[\"1\",\"2\",\"3\"]", actual);
    }
}

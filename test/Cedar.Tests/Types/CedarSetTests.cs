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
    public void ContainsFindsDecimalValues()
    {
        CedarSet set = new(CedarDecimal.NewDecimalFromInt(42));

        Assert.True(set.Contains(CedarDecimal.NewDecimalFromInt(42)));
        Assert.False(set.Contains(CedarDecimal.NewDecimalFromInt(1234)));
    }

    [Fact]
    public void ContainsFindsDatetimeValues()
    {
        CedarSet set = new(new CedarDatetime(42));

        Assert.True(set.Contains(new CedarDatetime(42)));
        Assert.False(set.Contains(new CedarDatetime(1234)));
    }

    [Fact]
    public void ContainsFindsDurationValues()
    {
        CedarSet set = new(new CedarDuration(42));

        Assert.True(set.Contains(new CedarDuration(42)));
        Assert.False(set.Contains(new CedarDuration(1234)));
    }

    [Fact]
    public void ContainsReturnsFalseForMissingLong()
    {
        CedarSet set = new(new CedarLong(42));

        Assert.False(set.Contains(new CedarLong(1234)));
    }

    [Fact]
    public void ContainsAllRequiresEveryMember()
    {
        CedarSet set = new(new CedarLong(1), new CedarLong(2), new CedarLong(3));

        Assert.True(set.ContainsAll(new CedarSet(new CedarLong(1), new CedarLong(3))));
        Assert.False(set.ContainsAll(new CedarSet(new CedarLong(1), new CedarLong(4))));
        Assert.True(set.ContainsAll(new CedarSet()));
    }

    [Fact]
    public void ContainsAnyRequiresAtLeastOneMember()
    {
        CedarSet set = new(new CedarLong(1), new CedarLong(2), new CedarLong(3));

        Assert.True(set.ContainsAny(new CedarSet(new CedarLong(3), new CedarLong(4))));
        Assert.False(set.ContainsAny(new CedarSet(new CedarLong(4), new CedarLong(5))));
        Assert.False(set.ContainsAny(new CedarSet()));
    }

    [Fact]
    public void MarshalCedarFormatsEmptySet()
    {
        CedarAssert.CedarText(new CedarSet(), "[]");
    }

    [Fact]
    public void LenReturnsZeroForEmptySet()
    {
        Assert.Equal(0, new CedarSet().Count);
    }

    [Fact]
    public void LenReturnsTwoForTwoElementSet()
    {
        Assert.Equal(2, new CedarSet(new CedarLong(1), new CedarLong(2)).Count);
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
    public void SetNotEqualToNonSetType()
    {
        CedarSet set = new(new CedarLong(42));

        Assert.False(set.Equals(new CedarLong(42)));
    }

    [Fact]
    public void EqualityDeduplicatesDuplicateElements()
    {
        CedarSet oneTwoThree = new(new CedarLong(1), new CedarLong(2), new CedarLong(3));
        CedarSet threeTwoTwoOne = new(new CedarLong(3), new CedarLong(2), new CedarLong(2), new CedarLong(1));

        CedarAssert.Equal(oneTwoThree, threeTwoTwoOne);
    }

    [Fact]
    public void SameHashDifferentTypesAreNotEqual()
    {
        CedarSet left = new(new CedarLong(0));
        CedarSet right = new(CedarDecimal.NewDecimalFromInt(0));

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void EmptySetsAreEqual()
    {
        CedarAssert.Equal(new CedarSet(), new CedarSet());
    }

    [Fact]
    public void NestedSetEqualityMatchesGoUpstream()
    {
        CedarSet empty = new();
        CedarSet oneTrue = new(new CedarBool(true));
        CedarSet oneFalse = new(new CedarBool(false));
        CedarSet nestedOnce = new(empty, oneTrue, oneFalse);
        CedarSet nestedOnce2 = new(empty, oneTrue, oneFalse);
        CedarSet nestedTwice = new(empty, oneTrue, oneFalse, nestedOnce);
        CedarSet nestedTwice2 = new(empty, oneTrue, oneFalse, nestedOnce);

        CedarAssert.Equal(nestedOnce, nestedOnce2);
        CedarAssert.Equal(nestedTwice, nestedTwice2);
        Assert.False(nestedOnce.Equals(nestedTwice));
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

        Assert.Equal(2, set.Count);
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

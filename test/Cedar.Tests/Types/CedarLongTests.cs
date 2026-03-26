using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class CedarLongTests
{
    [Fact]
    public void ConstructorStoresValue()
    {
        CedarLong value = new(42);

        Assert.Equal(42, value.Value);
    }

    [Fact]
    public void SupportsLongMinValue()
    {
        CedarLong value = new(long.MinValue);

        Assert.Equal(long.MinValue, value.Value);
    }

    [Fact]
    public void SupportsLongMaxValue()
    {
        CedarLong value = new(long.MaxValue);

        Assert.Equal(long.MaxValue, value.Value);
    }

    [Fact]
    public void EqualValuesAreEqual()
    {
        CedarAssert.Equal(new CedarLong(123456789), new CedarLong(123456789));
    }

    [Fact]
    public void DifferentValuesAreNotEqual()
    {
        CedarAssert.NotEqual(new CedarLong(1), new CedarLong(0));
    }

    [Fact]
    public void DifferentValueTypesAreNotEqual()
    {
        CedarAssert.NotEqual(new CedarLong(1), new CedarBool(true));
    }

    [Fact]
    public void HashCodeIsStable()
    {
        CedarAssert.HashStable(new CedarLong(922337203685477580));
    }

    [Fact]
    public void EqualValuesHaveEqualHashCodes()
    {
        CedarAssert.Equal(new CedarLong(-42), new CedarLong(-42));
    }

    [Fact]
    public void MarshalCedarFormatsPositiveInteger()
    {
        CedarAssert.CedarText(new CedarLong(42), "42");
    }

    [Fact]
    public void MarshalCedarFormatsNegativeInteger()
    {
        CedarAssert.CedarText(new CedarLong(-42), "-42");
    }
}

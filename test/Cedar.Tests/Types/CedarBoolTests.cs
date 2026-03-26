using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class CedarBoolTests
{
    [Fact]
    public void ConstructorStoresTrueValue()
    {
        CedarBool value = new(true);

        Assert.True(value.Value);
    }

    [Fact]
    public void ConstructorStoresFalseValue()
    {
        CedarBool value = new(false);

        Assert.False(value.Value);
    }

    [Fact]
    public void TrueConstantRepresentsTrue()
    {
        Assert.True(CedarBool.True.Value);
    }

    [Fact]
    public void FalseConstantRepresentsFalse()
    {
        Assert.False(CedarBool.False.Value);
    }

    [Fact]
    public void EqualValuesAreEqual()
    {
        CedarAssert.Equal(new CedarBool(true), new CedarBool(true));
    }

    [Fact]
    public void DifferentValuesAreNotEqual()
    {
        CedarAssert.NotEqual(new CedarBool(true), new CedarBool(false));
    }

    [Fact]
    public void DifferentValueTypesAreNotEqual()
    {
        CedarAssert.NotEqual(new CedarBool(false), new CedarLong(0));
    }

    [Fact]
    public void HashCodeIsStable()
    {
        CedarAssert.HashStable(new CedarBool(true));
    }

    [Fact]
    public void MarshalCedarFormatsTrue()
    {
        CedarAssert.CedarText(new CedarBool(true), "true");
    }

    [Fact]
    public void MarshalCedarFormatsFalse()
    {
        CedarAssert.CedarText(new CedarBool(false), "false");
    }
}

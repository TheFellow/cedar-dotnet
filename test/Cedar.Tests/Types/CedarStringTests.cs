using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class CedarStringTests
{
    [Fact]
    public void ConstructorStoresValue()
    {
        CedarString value = new("hello");

        Assert.Equal("hello", value.Value);
    }

    [Fact]
    public void EqualValuesAreEqual()
    {
        CedarAssert.Equal(new CedarString("hello"), new CedarString("hello"));
    }

    [Fact]
    public void DifferentValuesAreNotEqual()
    {
        CedarAssert.NotEqual(new CedarString("hello"), new CedarString("goodbye"));
    }

    [Fact]
    public void DifferentValueTypesAreNotEqual()
    {
        CedarAssert.NotEqual(new CedarString("1"), new CedarLong(1));
    }

    [Fact]
    public void HashCodeIsStable()
    {
        CedarAssert.HashStable(new CedarString("stable hash"));
    }

    [Fact]
    public void EqualValuesHaveEqualHashCodes()
    {
        CedarAssert.Equal(new CedarString("same"), new CedarString("same"));
    }

    [Fact]
    public void MarshalCedarWrapsValueInQuotes()
    {
        CedarAssert.CedarText(new CedarString("hello"), "\"hello\"");
    }

    [Fact]
    public void MarshalCedarEscapesQuotesAndBackslashes()
    {
        CedarAssert.CedarText(new CedarString("say \"hi\" \\ now"), "\"say \\\"hi\\\" \\\\ now\"");
    }

    [Fact]
    public void MarshalCedarEscapesControlCharacters()
    {
        CedarAssert.CedarText(new CedarString("a\tb\nc\rd\0"), "\"a\\tb\\nc\\rd\\0\"");
    }

    [Fact]
    public void MarshalCedarEscapesSingleQuotes()
    {
        CedarAssert.CedarText(new CedarString("'quoted'"), "\"\\'quoted\\'\"");
    }

    [Fact]
    public void MarshalCedarPreservesPrintableUnicode()
    {
        CedarAssert.CedarText(new CedarString("cafe \u00e9 😀"), "\"cafe \u00e9 😀\"");
    }

    [Fact]
    public void MarshalCedarEscapesNonPrintableUnicode()
    {
        CedarAssert.CedarText(new CedarString("\u0001"), "\"\\u{1}\"");
    }
}

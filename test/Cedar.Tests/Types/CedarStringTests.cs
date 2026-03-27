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

    [Theory]
    [InlineData("\u00A0", "\"\\u{a0}\"")]
    [InlineData("\u0300", "\"\\u{300}\"")]
    [InlineData("a\u0300", "\"a\u0300\"")]
    [InlineData("\u20DD", "\"\\u{20dd}\"")]
    [InlineData("\u0903", "\"\u0903\"")]
    [InlineData("\u00E9", "\"\u00E9\"")]
    [InlineData("😀", "\"😀\"")]
    [InlineData("\uFFFE", "\"\\u{fffe}\"")]
    [InlineData("\u00AD", "\"\\u{ad}\"")]
    [InlineData("\uFF9E", "\"\\u{ff9e}\"")]
    [InlineData("a\uFF9E", "\"a\uFF9E\"")]
    public void MarshalCedarEscapesUsingRustParity(string value, string expected)
    {
        CedarAssert.CedarText(new CedarString(value), expected);
    }

    [Theory]
    [InlineData("\u0007", "\"\\u{7}\"")]
    [InlineData("\u0008", "\"\\u{8}\"")]
    [InlineData("\u000C", "\"\\u{c}\"")]
    [InlineData("\u000B", "\"\\u{b}\"")]
    [InlineData("*foo", "\"\\*foo\"")]
    [InlineData("a\u0300", "\"a\\u{300}\"")]
    [InlineData("a\uFF9E", "\"a\\u{ff9e}\"")]
    [InlineData("a\u0903", "\"a\u0903\"")]
    [InlineData("hello", "\"hello\"")]
    public void PatternMarshalCedarEscapesEachCharacterUsingRustParity(string literal, string expected)
    {
        CedarAssert.CedarText(new CedarPattern(literal), expected);
    }

    [Fact]
    public void PatternMarshalCedarEscapesLiteralStarFollowedByWildcard()
    {
        CedarAssert.CedarText(new CedarPattern("*foo", Wildcard.Instance), "\"\\*foo*\"");
    }
}

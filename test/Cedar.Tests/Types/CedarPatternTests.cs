using System;
using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class CedarPatternTests
{
    [Fact]
    public void ConstructorMergesAdjacentLiteralSegments()
    {
        CedarPattern expected = new("alphabet");
        CedarPattern actual = new("alpha", "bet");

        CedarAssert.Equal(expected, actual);
    }

    [Fact]
    public void ConstructorCollapsesAdjacentWildcards()
    {
        CedarPattern expected = new("a", Wildcard.Instance, "b");
        CedarPattern actual = new("a", Wildcard.Instance, Wildcard.Instance, "b");

        CedarAssert.Equal(expected, actual);
    }

    [Fact]
    public void MatchSupportsExactLiteral()
    {
        Assert.True(new CedarPattern("hello").Match(new CedarString("hello")));
    }

    [Fact]
    public void MatchSupportsWildcardInMiddle()
    {
        Assert.True(new CedarPattern("he", Wildcard.Instance, "o").Match(new CedarString("hello")));
    }

    [Fact]
    public void MatchSupportsTrailingWildcard()
    {
        Assert.True(new CedarPattern("pre", Wildcard.Instance).Match(new CedarString("prefix")));
    }

    [Fact]
    public void MatchSupportsMultipleWildcards()
    {
        Assert.True(new CedarPattern("a", Wildcard.Instance, "b", Wildcard.Instance, "c").Match(new CedarString("axbyc")));
    }

    [Fact]
    public void MatchReturnsFalseWhenLiteralDoesNotFit()
    {
        Assert.False(new CedarPattern("hello").Match(new CedarString("goodbye")));
    }

    [Fact]
    public void ParseTreatsEscapedWildcardAsLiteralCharacter()
    {
        CedarPattern pattern = CedarPattern.Parse(@"a\*b");

        Assert.True(pattern.Match(new CedarString("a*b")));
        Assert.False(pattern.Match(new CedarString("axxb")));
    }

    [Fact]
    public void ParseRoundTripsPatternText()
    {
        CedarPattern pattern = CedarPattern.Parse(@"a\*b*c");

        Assert.Equal(@"a\*b*c", pattern.ToPatternText());
    }

    [Fact]
    public void AddWildcard_IsIdempotent()
    {
        CedarPattern pattern = new CedarPattern().AddWildcard().AddWildcard();

        CedarAssert.Equal(new CedarPattern(Wildcard.Instance), pattern);
    }

    [Fact]
    public void AddLiteral_MergesConsecutiveLiterals()
    {
        CedarPattern pattern = new CedarPattern().AddLiteral("foo").AddLiteral("bar");

        CedarAssert.Equal(new CedarPattern("foobar"), pattern);
    }

    [Fact]
    public void AddWildcardThenLiteral_MergesIntoSingleWildcardComponent()
    {
        CedarPattern pattern = new CedarPattern().AddWildcard().AddLiteral("foo");

        CedarAssert.Equal(new CedarPattern(Wildcard.Instance, "foo"), pattern);
    }

    [Fact]
    public void AddLiteralThenWildcard_ProducesTwoComponents()
    {
        CedarPattern pattern = new CedarPattern().AddLiteral("foo").AddWildcard();

        CedarAssert.Equal(new CedarPattern("foo", Wildcard.Instance), pattern);
    }

    [Fact]
    public void AddWildcardThenLiteralThenWildcard_ProducesWildcardSandwich()
    {
        CedarPattern pattern = new CedarPattern().AddWildcard().AddLiteral("foo").AddWildcard();

        CedarAssert.Equal(new CedarPattern(Wildcard.Instance, "foo", Wildcard.Instance), pattern);
    }

    [Fact]
    public void AddMethods_DoNotMutateOriginalPattern()
    {
        CedarPattern original = new("foo");
        CedarPattern updated = original.AddWildcard();

        CedarAssert.Equal(new CedarPattern("foo"), original);
        CedarAssert.Equal(new CedarPattern("foo", Wildcard.Instance), updated);
    }

    [Fact]
    public void ConstructorRejectsUnsupportedComponentType()
    {
        Assert.Throws<ArgumentException>(() => new CedarPattern(42));
    }

    [Fact]
    public void EqualValuesAreEqual()
    {
        CedarAssert.Equal(new CedarPattern("a", Wildcard.Instance, "b"), new CedarPattern("a", Wildcard.Instance, "b"));
    }

    [Fact]
    public void DifferentValuesAreNotEqual()
    {
        CedarAssert.NotEqual(new CedarPattern("a", Wildcard.Instance, "b"), new CedarPattern("a", "b"));
    }

    [Fact]
    public void HashCodeIsStable()
    {
        CedarAssert.HashStable(new CedarPattern("a", Wildcard.Instance, "b"));
    }

    [Fact]
    public void JsonRoundTripUsesPatternExtension()
    {
        CedarPattern expected = CedarPattern.Parse(@"a\*b*c");

        string json = CedarJson.SerializeData(expected);
        ICedarData actual = CedarJson.DeserializeData(json);

        Assert.Equal("{\"__extn\":{\"fn\":\"pattern\",\"arg\":\"a\\\\*b*c\"}}", json);
        CedarAssert.Equal(expected, Assert.IsType<CedarPattern>(actual));
    }

    [Fact]
    public void ParseEmptyStringProducesPatternThatMatchesOnlyEmptyString()
    {
        CedarPattern pattern = CedarPattern.Parse(string.Empty);

        Assert.True(pattern.Match(new CedarString(string.Empty)));
        Assert.False(pattern.Match(new CedarString("a")));
        Assert.Equal(string.Empty, pattern.ToPatternText());
    }

    [Fact]
    public void JsonRoundTripPreservesEmptyStringPatternExtension()
    {
        CedarPattern expected = new(string.Empty);

        string json = CedarJson.SerializeData(expected);
        ICedarData actual = CedarJson.DeserializeData(json);

        Assert.Equal("{\"__extn\":{\"fn\":\"pattern\",\"arg\":\"\"}}", json);
        CedarAssert.Equal(expected, Assert.IsType<CedarPattern>(actual));
    }

    [Fact]
    public void JsonRoundTripEscapesNullByteInPatternExtension()
    {
        CedarPattern expected = new("\0");

        string json = CedarJson.SerializeData(expected);
        ICedarData actual = CedarJson.DeserializeData(json);

        Assert.Equal("{\"__extn\":{\"fn\":\"pattern\",\"arg\":\"\\u0000\"}}", json);
        CedarAssert.Equal(expected, Assert.IsType<CedarPattern>(actual));
    }

    [Fact]
    public void ParseSingleWildcardMatchesAnything()
    {
        CedarPattern pattern = CedarPattern.Parse("*");

        Assert.True(pattern.Match(new CedarString("hello")));
        Assert.True(pattern.Match(new CedarString(string.Empty)));
    }

    [Fact]
    public void ParseDoubleWildcardCollapses()
    {
        CedarPattern single = CedarPattern.Parse("*");
        CedarPattern doubled = CedarPattern.Parse("**");

        Assert.Equal(single.ToPatternText(), doubled.ToPatternText());
    }

    [Fact]
    public void ParseWildcardBeforeLiteral()
    {
        CedarPattern pattern = CedarPattern.Parse("*a");

        Assert.True(pattern.Match(new CedarString("xa")));
        Assert.True(pattern.Match(new CedarString("a")));
        Assert.False(pattern.Match(new CedarString("ab")));
    }

    [Fact]
    public void ParseLiteralAfterWildcard()
    {
        CedarPattern pattern = CedarPattern.Parse("a*");

        Assert.True(pattern.Match(new CedarString("a")));
        Assert.True(pattern.Match(new CedarString("abc")));
        Assert.False(pattern.Match(new CedarString("b")));
    }

    [Fact]
    public void ParseWildcardLiteralWildcard()
    {
        CedarPattern pattern = CedarPattern.Parse("*a*");

        Assert.True(pattern.Match(new CedarString("xax")));
        Assert.True(pattern.Match(new CedarString("a")));
        Assert.False(pattern.Match(new CedarString("b")));
    }

    [Fact]
    public void ParseComplexPattern()
    {
        CedarPattern pattern = CedarPattern.Parse("*abra*ca*dabra");

        Assert.True(pattern.Match(new CedarString("xxabrayycanndabra")));
        Assert.False(pattern.Match(new CedarString("xxabrayycann")));
    }

    [Theory]
    [InlineData("\"\"", "", true)]
    [InlineData("\"\"", "hello", false)]
    [InlineData("\"*\"", "hello", true)]
    [InlineData("\"e\"", "hello", false)]
    [InlineData("\"*e\"", "hello", false)]
    [InlineData("\"*e*\"", "hello", true)]
    [InlineData("\"hello\"", "hello", true)]
    [InlineData("\"hello*\"", "hello", true)]
    [InlineData("\"*h*llo*\"", "hello", true)]
    [InlineData("\"h*e*o\"", "hello", true)]
    [InlineData("\"h*e**o\"", "hello", true)]
    [InlineData("\"h*z*o\"", "hello", false)]
    public void PatternParseAndMatchFromGoTests(string patternWithQuotes, string target, bool expected)
    {
        string rawPattern = patternWithQuotes[1..^1];
        CedarPattern pattern = CedarPattern.Parse(rawPattern);

        Assert.Equal(expected, pattern.Match(new CedarString(target)));
    }

    [Theory]
    [InlineData("\"\\*\\**\\*\\*\"", "**foo**", true)]
    [InlineData("\"\\*\\**\\*\\*\"", "**bar**", true)]
    [InlineData("\"\\*\\**\\*\\*\"", "*bar*", false)]
    public void PatternWithEscapedAsterisksMatchCorrectly(string patternWithQuotes, string target, bool expected)
    {
        string rawPattern = patternWithQuotes[1..^1];
        CedarPattern pattern = CedarPattern.Parse(rawPattern);

        Assert.Equal(expected, pattern.Match(new CedarString(target)));
    }

    [Theory]
    [InlineData("abra*ca")]
    [InlineData("*abra*ca")]
    [InlineData("abra*ca*")]
    [InlineData("*abra*ca*")]
    public void ParsePatternVariantsWithWildcardPositions(string rawPattern)
    {
        CedarPattern pattern = CedarPattern.Parse(rawPattern);
        string text = pattern.ToPatternText();

        CedarPattern reparsed = CedarPattern.Parse(text);
        Assert.Equal(text, reparsed.ToPatternText());
    }

    [Fact]
    public void ParsePatternWithMixedEscapedStarsAndWildcards()
    {
        CedarPattern pattern = CedarPattern.Parse("*abra*c\\**da\\*bra");

        Assert.True(pattern.Match(new CedarString("xxabrayyc*zzda*bra")));
        Assert.False(pattern.Match(new CedarString("xxabrayycnndabra")));

        string text = pattern.ToPatternText();
        Assert.Contains("\\*", text, StringComparison.Ordinal);
    }
}

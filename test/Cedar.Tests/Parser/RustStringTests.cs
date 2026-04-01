using System;
using Cedar.Core.Internal.Rust;
using Xunit;

namespace Cedar.Tests.Parser;

public sealed class RustStringTests
{
    [Fact]
    public void UnquotePlainString()
    {
        Assert.Equal("hello", RustStringHelper.Unquote("\"hello\""));
    }

    [Fact]
    public void UnquoteHandlesCommonEscapes()
    {
        string actual = RustStringHelper.Unquote("\"a\\n\\t\\r\\\\\\\"\\'\\0b\"");

        Assert.Equal("a\n\t\r\\\"'\0b", actual);
    }

    [Fact]
    public void UnquoteHandlesUnicodeEscape()
    {
        Assert.Equal("A", RustStringHelper.Unquote("\"\\u{41}\""));
        Assert.Equal(char.ConvertFromUtf32(0x1F600), RustStringHelper.Unquote("\"\\u{1f600}\""));
    }

    [Fact]
    public void UnquoteRejectsMissingQuotes()
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("hello"));
    }

    [Fact]
    public void UnquoteRejectsUnknownEscape()
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\q\""));
    }

    [Fact]
    public void UnquoteRejectsEscapedStarInNormalStrings()
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\*\""));
    }

    [Fact]
    public void UnquoteRejectsInvalidUnicodeEscapeShape()
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\u41\""));
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\u{}\""));
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\u{1234567}\""));
    }

    [Fact]
    public void UnquoteRejectsSurrogateCodePoint()
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\u{d800}\""));
    }

    [Fact]
    public void UnquoteEmptyString()
    {
        Assert.Equal(string.Empty, RustStringHelper.Unquote("\"\""));
    }

    [Fact]
    public void UnquoteZeroEscape()
    {
        Assert.Equal("a\0b", RustStringHelper.Unquote("\"a\\0b\""));
    }

    [Fact]
    public void UnquoteRejectsSurrogateRangeEnd()
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\u{dfff}\""));
    }

    [Fact]
    public void UnquoteAcceptsBelowSurrogateRange()
    {
        string expected = char.ConvertFromUtf32(0xD7FF);
        Assert.Equal(expected, RustStringHelper.Unquote("\"\\u{d7ff}\""));
    }

    [Fact]
    public void UnquoteAcceptsAboveSurrogateRange()
    {
        string expected = char.ConvertFromUtf32(0xE000);
        Assert.Equal(expected, RustStringHelper.Unquote("\"\\u{e000}\""));
    }

    [Fact]
    public void UnquoteAcceptsMaxCodePoint()
    {
        string expected = char.ConvertFromUtf32(0x10FFFF);
        Assert.Equal(expected, RustStringHelper.Unquote("\"\\u{10ffff}\""));
    }

    [Fact]
    public void UnquoteRejectsCodePointAboveMax()
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\u{110000}\""));
    }

    [Fact]
    public void UnquoteRejectsTooManyHexDigits()
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\u{0000000}\""));
    }

    [Theory]
    [InlineData("\"\\u{A}\"", "\u000a")]
    [InlineData("\"\\u{aB}\"", "\u00ab")]
    [InlineData("\"\\u{AbC}\"", "\u0abc")]
    [InlineData("\"\\u{aBcD}\"", "\uabcd")]
    public void UnquoteUnicodeEscapesOfVaryingLengths(string input, string expected)
    {
        Assert.Equal(expected, RustStringHelper.Unquote(input));
    }

    [Theory]
    [InlineData("\"\\a\"")]
    [InlineData("\"\\b\"")]
    [InlineData("\"\\f\"")]
    [InlineData("\"\\v\"")]
    [InlineData("\"\\1\"")]
    public void UnquoteRejectsInvalidEscapeCharacters(string input)
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote(input));
    }

    [Fact]
    public void UnquoteRejectsTrailingBackslash()
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\\""));
    }

    [Fact]
    public void UnquoteRejectsCurlyBraceEscape()
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\{\""));
    }

    [Fact]
    public void UnquoteAcceptsFiveDigitCodePoint()
    {
        string expected = char.ConvertFromUtf32(0xABCDE);
        Assert.Equal(expected, RustStringHelper.Unquote("\"\\u{AbCdE}\""));
    }

    [Fact]
    public void UnquoteAcceptsSixDigitCodePoint()
    {
        string expected = char.ConvertFromUtf32(0x10CDEF);
        Assert.Equal(expected, RustStringHelper.Unquote("\"\\u{10cDeF}\""));
    }

    [Fact]
    public void UnquoteRejectsCodePointFfffff()
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\u{ffffff}\""));
    }

    [Fact]
    public void UnquoteRejectsHexEscapeSequence()
    {
        Assert.Throws<FormatException>(() => RustStringHelper.Unquote("\"\\x00\""));
    }
}

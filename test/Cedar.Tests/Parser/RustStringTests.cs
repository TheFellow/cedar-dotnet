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
}

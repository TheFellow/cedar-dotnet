using System;
using Cedar.Schema.Internal;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class SchemaTokenizerTests
{
    [Fact]
    public void UnterminatedBlockComment_ThrowsParseException()
    {
        SchemaParseException exception = Assert.Throws<SchemaParseException>(() =>
            SchemaTokenizer.Tokenize("entity Foo = { /* unterminated comment"));

        Assert.Contains("unterminated block comment", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnterminatedStringLiteral_ThrowsParseException()
    {
        SchemaParseException exception = Assert.Throws<SchemaParseException>(() =>
            SchemaTokenizer.Tokenize("entity Foo = { \"unclosed string"));

        Assert.Contains("unterminated string", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnterminatedStringLiteral_EscapeAtEOF_ThrowsParseException()
    {
        SchemaParseException exception = Assert.Throws<SchemaParseException>(() =>
            SchemaTokenizer.Tokenize("entity Foo = { \"\\"));

        Assert.Contains("unterminated string", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

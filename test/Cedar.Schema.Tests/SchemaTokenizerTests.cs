using System;
using System.Collections.Generic;
using System.Linq;
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

    // --- Ported from Go lex_test.go: TestLexer ---

    [Fact]
    public void Tokenize_SimpleNamespaceProducesTokensEndingInEOF()
    {
        IReadOnlyList<SchemaToken> tokens = SchemaTokenizer.Tokenize("namespace Demo {}");

        Assert.Equal(SchemaTokenType.EndOfFile, tokens[^1].Type);
        Assert.True(tokens.Count > 1, "Expected multiple tokens for 'namespace Demo {}'");
    }

    // --- Ported from Go lex_test.go: TestLexerExample ---

    [Fact]
    public void Tokenize_EntityWithEscapeSequences_ProducesExpectedTokens()
    {
        const string source = """
            namespace Demo {
              entity User {
                "name\0\n\r\t\"\'_": String,
              };
              type id = String;
            }
            """;

        IReadOnlyList<SchemaToken> tokens = SchemaTokenizer.Tokenize(source);

        Assert.Equal(SchemaTokenType.Identifier, tokens[0].Type);
        Assert.Equal("namespace", tokens[0].Text);

        Assert.Equal(SchemaTokenType.Identifier, tokens[1].Type);
        Assert.Equal("Demo", tokens[1].Text);

        Assert.Equal(SchemaTokenType.LeftBrace, tokens[2].Type);
        Assert.Equal(SchemaTokenType.Identifier, tokens[3].Type);
        Assert.Equal("entity", tokens[3].Text);

        Assert.Equal(SchemaTokenType.Identifier, tokens[4].Type);
        Assert.Equal("User", tokens[4].Text);

        Assert.Equal(SchemaTokenType.LeftBrace, tokens[5].Type);

        Assert.Equal(SchemaTokenType.String, tokens[6].Type);

        Assert.Equal(SchemaTokenType.Colon, tokens[7].Type);

        Assert.Equal(SchemaTokenType.Identifier, tokens[8].Type);
        Assert.Equal("String", tokens[8].Text);

        Assert.Equal(SchemaTokenType.Comma, tokens[9].Type);
        Assert.Equal(SchemaTokenType.RightBrace, tokens[10].Type);
        Assert.Equal(SchemaTokenType.Semicolon, tokens[11].Type);

        Assert.Equal(SchemaTokenType.Identifier, tokens[12].Type);
        Assert.Equal("type", tokens[12].Text);

        Assert.Equal(SchemaTokenType.EndOfFile, tokens[^1].Type);
    }

    // --- Ported from Go lex_test.go: TestLexerNoPanic ---

    [Theory]
    [InlineData("{}[]<>?=,;::")]
    [InlineData("action context entity type namespace principal resource tags")]
    [InlineData("abc ABC _123 A_b_C")]
    [InlineData("\"hello world\"")]
    [InlineData("  \t  \n\r\n")]
    [InlineData("____azxkljcqmoqiwerjqflkjazxklmzlkmdrfoiwqerjlakdsfsazljfdi")]
    [InlineData("\r\n")]
    [InlineData("\"simple string\"")]
    [InlineData("\"string with \\\\escaped\\\\ backslashes\"")]
    public void Tokenize_DoesNotThrowOrHangOnVariousInputs(string input)
    {
        // Verifies that the tokenizer does not panic or hang on various inputs
        // (ported from Go TestLexerNoPanic)
        IReadOnlyList<SchemaToken> tokens = SchemaTokenizer.Tokenize(input);

        Assert.NotNull(tokens);
        Assert.Equal(SchemaTokenType.EndOfFile, tokens[^1].Type);
    }

    [Theory]
    [InlineData("\"unterminated")]
    [InlineData("\"\\\"")]
    [InlineData("\"string with\nnewline\"")]
    public void Tokenize_MalformedStrings_Throws(string input)
    {
        Assert.ThrowsAny<SchemaParseException>(() => SchemaTokenizer.Tokenize(input));
    }

    [Fact]
    public void Tokenize_UnexpectedCharacterThrows()
    {
        Assert.Throws<SchemaParseException>(() => SchemaTokenizer.Tokenize("@entity Foo { ~ }"));
    }

    // --- Ported from Go lex_test.go: basic token coverage ---

    [Fact]
    public void Tokenize_AllBracketAndPunctuationTokenTypes()
    {
        IReadOnlyList<SchemaToken> tokens = SchemaTokenizer.Tokenize("{} [] <> () , ; : :: ? = @");

        SchemaTokenType[] expected =
        [
            SchemaTokenType.LeftBrace, SchemaTokenType.RightBrace,
            SchemaTokenType.LeftBracket, SchemaTokenType.RightBracket,
            SchemaTokenType.LeftAngle, SchemaTokenType.RightAngle,
            SchemaTokenType.LeftParen, SchemaTokenType.RightParen,
            SchemaTokenType.Comma, SchemaTokenType.Semicolon,
            SchemaTokenType.Colon, SchemaTokenType.DoubleColon,
            SchemaTokenType.Question, SchemaTokenType.Equals,
            SchemaTokenType.At,
            SchemaTokenType.EndOfFile
        ];

        Assert.Equal(expected, tokens.Select(static token => token.Type).ToArray());
    }

    // --- Ported from Go token_test.go: reserved keyword detection ---

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("if")]
    [InlineData("then")]
    [InlineData("else")]
    [InlineData("in")]
    [InlineData("like")]
    [InlineData("has")]
    [InlineData("is")]
    [InlineData("__cedar")]
    public void IsReservedKeyword_ReturnsTrueForReservedKeywords(string keyword)
    {
        Assert.True(SchemaTokenizer.IsReservedKeyword(keyword));
    }

    [Theory]
    [InlineData("entity")]
    [InlineData("action")]
    [InlineData("namespace")]
    [InlineData("type")]
    [InlineData("String")]
    [InlineData("foobar")]
    public void IsReservedKeyword_ReturnsFalseForNonReservedKeywords(string keyword)
    {
        Assert.False(SchemaTokenizer.IsReservedKeyword(keyword));
    }

    [Fact]
    public void Tokenize_ReservedKeywordsEmitReservedKeywordType()
    {
        IReadOnlyList<SchemaToken> tokens = SchemaTokenizer.Tokenize("true false if then else in like has is __cedar");

        IEnumerable<SchemaToken> keywordTokens = tokens.Where(static token => token.Type != SchemaTokenType.EndOfFile);
        Assert.All(keywordTokens, static token => Assert.Equal(SchemaTokenType.ReservedKeyword, token.Type));
    }

    // --- Ported from Go token_test.go: SchemaParseException formatting ---

    [Fact]
    public void SchemaParseException_IncludesFilenameAndPosition()
    {
        SchemaPosition position = new("testfile", 0, 1, 2);
        SchemaParseException exception = new(position, "test error");

        Assert.Equal("testfile:1:2: test error", exception.Message);
    }

    [Fact]
    public void SchemaParseException_UsesInputPlaceholderWhenFilenameIsEmpty()
    {
        SchemaPosition position = new("", 0, 1, 2);
        SchemaParseException exception = new(position, "test error");

        Assert.Equal("<input>:1:2: test error", exception.Message);
    }

    // --- Ported from Go lex_test.go: comment handling ---

    [Fact]
    public void Tokenize_SkipsLineComments()
    {
        IReadOnlyList<SchemaToken> tokens = SchemaTokenizer.Tokenize("entity // this is a comment\nUser");

        Assert.Equal("entity", tokens[0].Text);
        Assert.Equal("User", tokens[1].Text);
        Assert.Equal(SchemaTokenType.EndOfFile, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_SkipsBlockComments()
    {
        IReadOnlyList<SchemaToken> tokens = SchemaTokenizer.Tokenize("entity /* block comment */ User");

        Assert.Equal("entity", tokens[0].Text);
        Assert.Equal("User", tokens[1].Text);
        Assert.Equal(SchemaTokenType.EndOfFile, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_TracksLineNumbersAcrossNewlines()
    {
        IReadOnlyList<SchemaToken> tokens = SchemaTokenizer.Tokenize("entity\nUser");

        Assert.Equal(1, tokens[0].Position.Line);
        Assert.Equal(2, tokens[1].Position.Line);
    }

    [Fact]
    public void Tokenize_PreservesFilenameInPosition()
    {
        IReadOnlyList<SchemaToken> tokens = SchemaTokenizer.Tokenize("entity User", "test.cedarschema");

        Assert.Equal("test.cedarschema", tokens[0].Position.Filename);
    }

    [Fact]
    public void Tokenize_EmptyInputProducesOnlyEOF()
    {
        IReadOnlyList<SchemaToken> tokens = SchemaTokenizer.Tokenize("");

        SchemaToken eof = Assert.Single(tokens);
        Assert.Equal(SchemaTokenType.EndOfFile, eof.Type);
    }

    [Fact]
    public void Tokenize_WhitespaceOnlyInputProducesOnlyEOF()
    {
        IReadOnlyList<SchemaToken> tokens = SchemaTokenizer.Tokenize("   \t\n  \r\n  ");

        SchemaToken eof = Assert.Single(tokens);
        Assert.Equal(SchemaTokenType.EndOfFile, eof.Type);
    }
}

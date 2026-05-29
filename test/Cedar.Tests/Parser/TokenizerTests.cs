using System;
using System.Linq;
using System.Text;
using Cedar.Core;
using Cedar.Core.Internal.Parser;
using Xunit;

namespace Cedar.Tests.Parser;

public sealed class TokenizerTests
{
    [Fact]
    public void TokenizeKeywordsAndIdentifier()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("permit forbid when unless true false if then else in like has is ident")).ToArray();

        Assert.Equal(TokenType.Permit, tokens[0].Type);
        Assert.Equal(TokenType.Forbid, tokens[1].Type);
        Assert.Equal(TokenType.When, tokens[2].Type);
        Assert.Equal(TokenType.Unless, tokens[3].Type);
        Assert.Equal(TokenType.True, tokens[4].Type);
        Assert.Equal(TokenType.False, tokens[5].Type);
        Assert.Equal(TokenType.If, tokens[6].Type);
        Assert.Equal(TokenType.Then, tokens[7].Type);
        Assert.Equal(TokenType.Else, tokens[8].Type);
        Assert.Equal(TokenType.In, tokens[9].Type);
        Assert.Equal(TokenType.Like, tokens[10].Type);
        Assert.Equal(TokenType.Has, tokens[11].Type);
        Assert.Equal(TokenType.Is, tokens[12].Type);
        Assert.Equal(TokenType.Ident, tokens[13].Type);
        Assert.Equal(TokenType.EOF, tokens[14].Type);
    }

    [Fact]
    public void TokenizeOperatorsAndPunctuation()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("(){}[],;:.::+-*! != = == < <= > >= & && | || @")).ToArray();

        TokenType[] expected =
        [
            TokenType.LParen,
            TokenType.RParen,
            TokenType.LBrace,
            TokenType.RBrace,
            TokenType.LBracket,
            TokenType.RBracket,
            TokenType.Comma,
            TokenType.Semicolon,
            TokenType.Colon,
            TokenType.Dot,
            TokenType.ColonColon,
            TokenType.Plus,
            TokenType.Dash,
            TokenType.Star,
            TokenType.Bang,
            TokenType.BangEq,
            TokenType.Eq,
            TokenType.EqEq,
            TokenType.Lt,
            TokenType.LtEq,
            TokenType.Gt,
            TokenType.GtEq,
            TokenType.Amp,
            TokenType.AmpAmp,
            TokenType.Pipe,
            TokenType.PipePipe,
            TokenType.At,
            TokenType.EOF
        ];

        Assert.Equal(expected, tokens.Select(static token => token.Type));
    }

    [Fact]
    public void TokenizeStringEscapes()
    {
        Token token = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"a\\n\\t\\r\\\\\\\"\\'\\0\\u{41}\\*\"")).First();

        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("\"a\\n\\t\\r\\\\\\\"\\'\\0\\u{41}\\*\"", token.Text);
    }

    [Fact]
    public void TokenizeIntegers()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("0 42 9223372036854775807")).ToArray();

        Assert.Equal(TokenType.Int, tokens[0].Type);
        Assert.Equal("0", tokens[0].Text);
        Assert.Equal("42", tokens[1].Text);
        Assert.Equal("9223372036854775807", tokens[2].Text);
    }

    [Fact]
    public void TokenizeRejectsLeadingZeroInteger()
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("01")));

        Assert.Contains("leading zero", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TokenizeSkipsSingleLineComments()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("permit // ignore\nforbid")).ToArray();

        Assert.Equal([TokenType.Permit, TokenType.Forbid, TokenType.EOF], tokens.Select(static token => token.Type));
    }

    [Fact]
    public void TokenizeSkipsMultiLineComments()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("permit /* block\ncomment */ forbid")).ToArray();

        Assert.Equal([TokenType.Permit, TokenType.Forbid, TokenType.EOF], tokens.Select(static token => token.Type));
    }

    [Fact]
    public void TokenizeRejectsUnterminatedComment()
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("/* no end")));

        Assert.Contains("Comment not terminated", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TokenizeRejectsInvalidEscape()
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"\\q\"")));

        Assert.Contains("Invalid escape sequence", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TokenizeRejectsUnterminatedString()
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"unterminated")));

        Assert.Contains("not terminated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TokenizeTracksPositionAcrossLines()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("permit\nforbid")).ToArray();

        Assert.Equal(new Position(string.Empty, 0, 1, 1), tokens[0].Position);
        Assert.Equal(new Position(string.Empty, 7, 2, 1), tokens[1].Position);
    }

    [Fact]
    public void TokenizeCreatesCollapsedAnnotationToken()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("@id(\"abc\") permit")) .ToArray();

        Assert.Equal(TokenType.Annotation, tokens[0].Type);
        Assert.Equal("@id(\"abc\")", tokens[0].Text);
        Assert.Equal(TokenType.Permit, tokens[1].Type);
    }

    [Fact]
    public void TokenizeBareAnnotation()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("@bare permit")).ToArray();

        Assert.Equal(TokenType.Annotation, tokens[0].Type);
        Assert.Equal("@bare", tokens[0].Text);
        Assert.Equal(TokenType.Permit, tokens[1].Type);
    }

    [Fact]
    public void TokenizeAcceptsWhitespaceInsideAnnotation()
    {
        Token token = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("@id ( \"x\" )")).First();

        Assert.Equal(TokenType.Annotation, token.Type);
        Assert.Equal("@id ( \"x\" )", token.Text);
    }

    [Fact]
    public void TokenizeBareAnnotationWithWhitespace()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("@ bare\n")).ToArray();

        Assert.Equal(TokenType.Annotation, tokens[0].Type);
        Assert.Equal("@ bare", tokens[0].Text);
        Assert.Equal(TokenType.EOF, tokens[1].Type);
    }

    [Fact]
    public void TokenizeCreatesAtTokenWhenNotAnnotation()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("@\"x\"")).ToArray();

        Assert.Equal(TokenType.At, tokens[0].Type);
        Assert.Equal(TokenType.String, tokens[1].Type);
    }

    [Fact]
    public void TokenizeSupportsUtf8InsideStringLiteral()
    {
        Token token = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"héllo\"")).First();

        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("\"héllo\"", token.Text);
    }

    [Fact]
    public void TokenizeRejectsInvalidUtf8()
    {
        byte[] bytes = [0x80, 0x80];

        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(bytes));
        Assert.Contains("Unexpected character", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TokenizeReturnsOnlyEofForEmptyInput()
    {
        Token[] tokens = CedarTokenizer.Tokenize(ReadOnlySpan<byte>.Empty).ToArray();

        Assert.Single(tokens);
        Assert.Equal(TokenType.EOF, tokens[0].Type);
    }

    [Fact]
    public void TokenizeHandlesColonColonAtEndOfTypePath()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("Type::\"id\"")).ToArray();

        Assert.Equal([TokenType.Ident, TokenType.ColonColon, TokenType.String, TokenType.EOF], tokens.Select(static token => token.Type));
    }

    [Fact]
    public void TokenizeParsesDashAsOperatorNotPartOfInteger()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("-1")).ToArray();

        Assert.Equal(TokenType.Dash, tokens[0].Type);
        Assert.Equal(TokenType.Int, tokens[1].Type);
    }

    [Fact]
    public void TokenizeRejectsUnknownCharacter()
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("%")));

        Assert.Contains("Unexpected character", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"\\a\"")]
    [InlineData("\"\\b\"")]
    [InlineData("\"\\f\"")]
    [InlineData("\"\\v\"")]
    [InlineData("\"\\1\"")]
    public void TokenizeRejectsInvalidCharEscapes(string input)
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes(input)));

        Assert.Contains("Invalid escape sequence", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TokenizeRejectsInvalidUnicodeEscapeMissingBraces()
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"\\ubadf\"")));

        Assert.Contains("unicode escape sequence", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TokenizeRejectsEmptyUnicodeEscape()
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"\\u{}\"")));

        Assert.Contains("escape sequence", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TokenizeRejectsTooManyDigitsInUnicodeEscape()
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"\\u{0000000}\"")));

        Assert.Contains("escape sequence", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("'")]
    [InlineData("/")]
    public void TokenizeRejectsUnknownSingleCharacters(string input)
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes(input)));

        Assert.NotNull(ex);
    }

    [Fact]
    public void TokenizeHandlesWildcardInStringLiteral()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"*\"")).ToArray();

        Assert.Equal(TokenType.String, tokens[0].Type);
        Assert.Equal("\"*\"", tokens[0].Text);
    }

    [Fact]
    public void TokenizeHandlesEscapedWildcardInStringLiteral()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"\\*\"")).ToArray();

        Assert.Equal(TokenType.String, tokens[0].Type);
        Assert.Equal("\"\\*\"", tokens[0].Text);
    }

    [Fact]
    public void TokenizeRejectsHexEscapeInString()
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"\\x123\"")));

        Assert.Contains("Invalid escape sequence", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TokenizeHandlesUnicodeEscapeInString()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"\\u{0}\\u{10fFfF}\"")).ToArray();

        Assert.Equal(TokenType.String, tokens[0].Type);
    }

    [Fact]
    public void TokenizeRejectsUppercaseUUnicodeEscape()
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"\\U0000badf\"")));

        Assert.Contains("Invalid escape sequence", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TokenizeRejectsNonHexInUnicodeEscape()
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"\\u{z\"")));

        Assert.Contains("escape sequence", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TokenizeRejectsNulCharacter()
    {
        ParseException ex = Assert.Throws<ParseException>(() => CedarTokenizer.Tokenize(new byte[] { 0x00 }));

        Assert.NotNull(ex);
    }

    [Fact]
    public void TokenizeMultiCharOperatorsAndTokenTypes()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes(":::")).ToArray();

        Assert.Equal(TokenType.ColonColon, tokens[0].Type);
        Assert.Equal(TokenType.Colon, tokens[1].Type);
    }

    [Fact]
    public void TokenizeBangAndBangEquals()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("!!=")).ToArray();

        Assert.Equal(TokenType.Bang, tokens[0].Type);
        Assert.Equal(TokenType.BangEq, tokens[1].Type);
    }

    [Fact]
    public void TokenizeReservedKeywords()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("true false if then else in like has is")).ToArray();

        Assert.Equal(TokenType.True, tokens[0].Type);
        Assert.Equal(TokenType.False, tokens[1].Type);
        Assert.Equal(TokenType.If, tokens[2].Type);
        Assert.Equal(TokenType.Then, tokens[3].Type);
        Assert.Equal(TokenType.Else, tokens[4].Type);
        Assert.Equal(TokenType.In, tokens[5].Type);
        Assert.Equal(TokenType.Like, tokens[6].Type);
        Assert.Equal(TokenType.Has, tokens[7].Type);
        Assert.Equal(TokenType.Is, tokens[8].Type);
        Assert.Equal(TokenType.EOF, tokens[9].Type);
    }

    [Fact]
    public void TokenizeWildcardAndEscapedWildcardInPatternStrings()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"*\" \"\\*\" \"*\\**\"")).ToArray();

        Assert.Equal(TokenType.String, tokens[0].Type);
        Assert.Equal("\"*\"", tokens[0].Text);
        Assert.Equal(TokenType.String, tokens[1].Type);
        Assert.Equal("\"\\*\"", tokens[1].Text);
        Assert.Equal(TokenType.String, tokens[2].Type);
        Assert.Equal("\"*\\**\"", tokens[2].Text);
    }

    [Fact]
    public void TokenizeStringEscapeSequences()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("\"\\\"\\'\\'\\n\\r\\t\\\\\\0\"")).ToArray();

        Assert.Equal(TokenType.String, tokens[0].Type);
    }

    [Fact]
    public void TokenizeNegativeIntegers()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("-1 9223372036854775807 -9223372036854775808")).ToArray();

        Assert.Equal(TokenType.Dash, tokens[0].Type);
        Assert.Equal(TokenType.Int, tokens[1].Type);
        Assert.Equal("1", tokens[1].Text);
        Assert.Equal(TokenType.Int, tokens[2].Type);
        Assert.Equal("9223372036854775807", tokens[2].Text);
        Assert.Equal(TokenType.Dash, tokens[3].Type);
        Assert.Equal(TokenType.Int, tokens[4].Type);
        Assert.Equal("9223372036854775808", tokens[4].Text);
    }
}

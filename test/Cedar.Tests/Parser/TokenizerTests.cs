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
    public void TokenizeCreatesAtTokenWhenNotAnnotation()
    {
        Token[] tokens = CedarTokenizer.Tokenize(Encoding.UTF8.GetBytes("@ permit")).ToArray();

        Assert.Equal(TokenType.At, tokens[0].Type);
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
}

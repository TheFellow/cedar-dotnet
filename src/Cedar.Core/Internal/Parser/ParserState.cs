using System;
using System.Collections.Immutable;
using Cedar.Core.Internal.Rust;
using Cedar.Types;

namespace Cedar.Core.Internal.Parser;

internal sealed class ParserState
{
    private readonly ImmutableArray<Token> _tokens;
    private readonly int _maxDepth;
    private int _index;
    private int _depth;

    public ParserState(ImmutableArray<Token> tokens, int maxDepth)
    {
        _tokens = tokens;
        _maxDepth = maxDepth;
        _index = 0;
        _depth = 0;
    }

    public Token Current => _tokens[_index];

    public Token Previous => _index == 0 ? _tokens[0] : _tokens[_index - 1];

    public bool IsAtEnd => Current.Type == TokenType.EOF;

    public bool Check(TokenType type)
    {
        return Current.Type == type;
    }

    public Token Advance()
    {
        Token current = Current;
        if (!IsAtEnd)
        {
            _index++;
        }

        return current;
    }

    public bool Match(TokenType type)
    {
        if (!Check(type))
        {
            return false;
        }

        Advance();
        return true;
    }

    public Token Expect(TokenType type, string message)
    {
        if (!Check(type))
        {
            throw Error(Current, message);
        }

        return Advance();
    }

    public Token ExpectIdentifier(string message)
    {
        if (!Check(TokenType.Ident) && !CheckSoftKeyword())
        {
            throw Error(Current, message);
        }

        return Advance();
    }

    public bool CheckSoftKeyword()
    {
        return Current.Type is TokenType.Permit or TokenType.Forbid or TokenType.When or TokenType.Unless;
    }


    public string ParseStringToken(Token token)
    {
        if (token.Type != TokenType.String)
        {
            throw Error(token, "Expected string literal.");
        }

        try
        {
            return RustStringHelper.Unquote(token.Text);
        }
        catch (FormatException ex)
        {
            throw new ParseException(token.Position, ex.Message);
        }
    }

    public string ParseRawStringToken(Token token)
    {
        if (token.Type != TokenType.String)
        {
            throw Error(token, "Expected string literal.");
        }

        if (token.Text.Length < 2 || token.Text[0] != '"' || token.Text[^1] != '"')
        {
            throw Error(token, "Malformed string literal.");
        }

        return token.Text[1..^1];
    }

    public CedarPath ParseEntityTypePath()
    {
        Token first = ExpectIdentifier("Expected entity type identifier.");
        string value = first.Text;

        while (Match(TokenType.ColonColon))
        {
            Token segment = ExpectIdentifier("Expected entity type segment after '::'.");
            value = value + "::" + segment.Text;
        }

        return new CedarPath(value);
    }

    public EntityUid ParseEntityUid()
    {
        Token first = ExpectIdentifier("Expected entity type identifier.");
        return ParseEntityUidFromFirst(first);
    }

    public EntityUid ParseEntityUidFromFirst(Token first)
    {
        string type = first.Text;

        while (Match(TokenType.ColonColon))
        {
            if (Check(TokenType.Ident) || CheckSoftKeyword())
            {
                type = type + "::" + Advance().Text;
                continue;
            }

            if (Check(TokenType.String))
            {
                string id = ParseStringToken(Advance());
                return new EntityUid(new EntityType(type), new CedarString(id));
            }

            throw Error(Current, "Expected identifier or string literal after '::'.");
        }

        throw Error(Current, "Expected entity id string literal after '::'.");
    }

    public ParseException Error(Token token, string message)
    {
        return new ParseException(token.Position, message);
    }

    public void EnterDepth()
    {
        _depth++;
        if (_depth > _maxDepth)
        {
            throw new ParseException(Current.Position, $"Maximum parse depth of {_maxDepth} exceeded.");
        }
    }

    public void ExitDepth()
    {
        if (_depth > 0)
        {
            _depth--;
        }
    }

    public void SynchronizeToNextPolicy()
    {
        if (!IsAtEnd)
        {
            Advance();
        }

        while (!IsAtEnd)
        {
            if (Previous.Type == TokenType.Semicolon)
            {
                return;
            }

            if (Current.Type is TokenType.Permit or TokenType.Forbid or TokenType.Annotation or TokenType.At)
            {
                return;
            }

            Advance();
        }
    }
}

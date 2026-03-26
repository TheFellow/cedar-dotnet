using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Cedar.Ast.Internal;
using Cedar.Types;

namespace Cedar.Core.Internal.Parser;

internal sealed class ExpressionParser
{
    private readonly ParserState _state;

    private ExpressionParser(ParserState state)
    {
        _state = state;
    }

    public static INode ParseExpression(ParserState state)
    {
        return new ExpressionParser(state).ParseExpression();
    }

    private INode ParseExpression()
    {
        _state.EnterDepth();
        try
        {
            if (_state.Match(TokenType.If))
            {
                INode condition = ParseExpression();
                _state.Expect(TokenType.Then, "Expected 'then' in if-then-else expression.");
                INode thenNode = ParseExpression();
                _state.Expect(TokenType.Else, "Expected 'else' in if-then-else expression.");
                INode elseNode = ParseExpression();
                return new NodeIfThenElse(condition, thenNode, elseNode);
            }

            return ParseOr();
        }
        finally
        {
            _state.ExitDepth();
        }
    }

    private INode ParseOr()
    {
        INode lhs = ParseAnd();

        while (_state.Match(TokenType.PipePipe))
        {
            INode rhs = ParseAnd();
            lhs = new NodeOr(lhs, rhs);
        }

        return lhs;
    }

    private INode ParseAnd()
    {
        INode lhs = ParseRelation();

        while (_state.Match(TokenType.AmpAmp))
        {
            INode rhs = ParseRelation();
            lhs = new NodeAnd(lhs, rhs);
        }

        return lhs;
    }

    private INode ParseRelation()
    {
        INode lhs = ParseAdd();

        if (_state.Match(TokenType.Has))
        {
            return ParseHas(lhs);
        }

        if (_state.Match(TokenType.Like))
        {
            return ParseLike(lhs);
        }

        if (_state.Match(TokenType.Is))
        {
            return ParseIs(lhs);
        }

        if (_state.Match(TokenType.Lt))
        {
            return new NodeLessThan(lhs, ParseAdd());
        }

        if (_state.Match(TokenType.LtEq))
        {
            return new NodeLessThanOrEqual(lhs, ParseAdd());
        }

        if (_state.Match(TokenType.Gt))
        {
            return new NodeGreaterThan(lhs, ParseAdd());
        }

        if (_state.Match(TokenType.GtEq))
        {
            return new NodeGreaterThanOrEqual(lhs, ParseAdd());
        }

        if (_state.Match(TokenType.BangEq))
        {
            return new NodeNotEquals(lhs, ParseAdd());
        }

        if (_state.Match(TokenType.EqEq))
        {
            return new NodeEquals(lhs, ParseAdd());
        }

        if (_state.Match(TokenType.In))
        {
            return new NodeIn(lhs, ParseAdd());
        }

        return lhs;
    }

    private INode ParseHas(INode lhs)
    {
        Token token = _state.Advance();
        if (token.Type == TokenType.String)
        {
            string attribute = _state.ParseStringToken(token);
            return new NodeHas(lhs, new CedarString(attribute));
        }

        if (token.Type != TokenType.Ident)
        {
            throw _state.Error(token, "Expected identifier or string after 'has'.");
        }

        CedarString firstAttribute = new(token.Text);
        INode result = new NodeHas(lhs, firstAttribute);
        INode currentLhs = new NodeAccess(lhs, firstAttribute);

        while (_state.Match(TokenType.Dot))
        {
            Token attributeToken = _state.ExpectIdentifier("Expected identifier after '.'.");
            CedarString attribute = new(attributeToken.Text);
            INode hasNode = new NodeHas(currentLhs, attribute);
            result = new NodeAnd(result, hasNode);
            currentLhs = new NodeAccess(currentLhs, attribute);
        }

        return result;
    }

    private INode ParseLike(INode lhs)
    {
        Token token = _state.Expect(TokenType.String, "Expected string literal after 'like'.");
        CedarPattern pattern = PatternParser.ParseLikePattern(_state, token);
        return new NodeLike(lhs, pattern);
    }

    private INode ParseIs(INode lhs)
    {
        EntityType entityType = _state.ParseEntityTypePath();
        if (_state.Match(TokenType.In))
        {
            INode entity = ParseAdd();
            return new NodeIsIn(lhs, entityType, entity);
        }

        return new NodeIs(lhs, entityType);
    }

    private INode ParseAdd()
    {
        INode lhs = ParseMult();

        while (true)
        {
            if (_state.Match(TokenType.Plus))
            {
                lhs = new NodeAdd(lhs, ParseMult());
                continue;
            }

            if (_state.Match(TokenType.Dash))
            {
                lhs = new NodeSub(lhs, ParseMult());
                continue;
            }

            break;
        }

        return lhs;
    }

    private INode ParseMult()
    {
        INode lhs = ParseUnary();

        while (_state.Match(TokenType.Star))
        {
            lhs = new NodeMult(lhs, ParseUnary());
        }

        return lhs;
    }

    private INode ParseUnary()
    {
        List<bool> operations = [];

        while (true)
        {
            if (_state.Match(TokenType.Dash))
            {
                operations.Add(true);
                continue;
            }

            if (_state.Match(TokenType.Bang))
            {
                operations.Add(false);
                continue;
            }

            break;
        }

        INode node;
        if (operations.Count > 0 && operations[^1] && _state.Check(TokenType.Int))
        {
            Token token = _state.Advance();
            if (!long.TryParse("-" + token.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
            {
                throw _state.Error(token, "Integer literal is out of range.");
            }

            node = new NodeValue(new CedarLong(value));
            operations.RemoveAt(operations.Count - 1);
        }
        else
        {
            node = ParseMember();
        }

        for (int i = operations.Count - 1; i >= 0; i--)
        {
            node = operations[i] ? new NodeNegate(node) : new NodeNot(node);
        }

        return node;
    }

    private INode ParseMember()
    {
        INode lhs = ParsePrimary();

        while (true)
        {
            if (_state.Match(TokenType.Dot))
            {
                Token member = _state.ExpectIdentifier("Expected identifier after '.'.");
                if (_state.Match(TokenType.LParen))
                {
                    ImmutableArray<INode> args = ParseExpressionList(TokenType.RParen, "Expected ')' after method arguments.");
                    lhs = ParseMethodCall(lhs, member, args);
                }
                else
                {
                    lhs = new NodeAccess(lhs, new CedarString(member.Text));
                }

                continue;
            }

            if (_state.Match(TokenType.LBracket))
            {
                INode key = ParseExpression();
                _state.Expect(TokenType.RBracket, "Expected ']' after index expression.");
                lhs = new NodeGetTag(lhs, key);
                continue;
            }

            return lhs;
        }
    }

    private INode ParsePrimary()
    {
        Token token = _state.Advance();

        if (token.Type == TokenType.Int)
        {
            if (!long.TryParse(token.Text, NumberStyles.None, CultureInfo.InvariantCulture, out long value))
            {
                throw _state.Error(token, "Integer literal is out of range.");
            }

            return new NodeValue(new CedarLong(value));
        }

        if (token.Type == TokenType.String)
        {
            string value = _state.ParseStringToken(token);
            return new NodeValue(new CedarString(value));
        }

        if (token.Type == TokenType.True)
        {
            return new NodeValue(CedarBool.True);
        }

        if (token.Type == TokenType.False)
        {
            return new NodeValue(CedarBool.False);
        }

        if (token.Type == TokenType.Ident)
        {
            if (_state.Check(TokenType.ColonColon))
            {
                EntityUid uid = _state.ParseEntityUidFromFirst(token);
                return new NodeValue(uid);
            }

            if (_state.Match(TokenType.LParen))
            {
                ImmutableArray<INode> args = ParseExpressionList(TokenType.RParen, "Expected ')' after function arguments.");
                return new NodeExtensionCall(token.Text, args);
            }

            if (token.Text is "principal" or "action" or "resource" or "context")
            {
                return new NodeVariable(new CedarString(token.Text));
            }

            throw _state.Error(token, $"Unexpected identifier '{token.Text}'.");
        }

        if (token.Type == TokenType.LParen)
        {
            INode expression = ParseExpression();
            _state.Expect(TokenType.RParen, "Expected ')' after expression.");
            return expression;
        }

        if (token.Type == TokenType.LBracket)
        {
            return ParseSetLiteral();
        }

        if (token.Type == TokenType.LBrace)
        {
            return ParseRecordLiteral();
        }

        throw _state.Error(token, "Invalid expression.");
    }

    private INode ParseMethodCall(INode lhs, Token method, ImmutableArray<INode> args)
    {
        return method.Text switch
        {
            "contains" => RequireOneArg(method, args, static (node, arg) => new NodeContains(node, arg), lhs),
            "containsAll" => RequireOneArg(method, args, static (node, arg) => new NodeContainsAll(node, arg), lhs),
            "containsAny" => RequireOneArg(method, args, static (node, arg) => new NodeContainsAny(node, arg), lhs),
            "hasTag" => RequireOneArg(method, args, static (node, arg) => new NodeHasTag(node, arg), lhs),
            "getTag" => RequireOneArg(method, args, static (node, arg) => new NodeGetTag(node, arg), lhs),
            "isEmpty" => RequireZeroArgs(method, args, static node => new NodeIsEmpty(node), lhs),
            _ => ParseExtensionStyleMethodCall(lhs, method, args)
        };
    }

    private static INode ParseExtensionStyleMethodCall(INode lhs, Token method, ImmutableArray<INode> args)
    {
        ImmutableArray<INode>.Builder callArgs = ImmutableArray.CreateBuilder<INode>(args.Length + 1);
        callArgs.Add(lhs);
        foreach (INode arg in args)
        {
            callArgs.Add(arg);
        }

        return new NodeExtensionCall(method.Text, callArgs.ToImmutable());
    }

    private INode ParseSetLiteral()
    {
        List<INode> elements = [];

        if (_state.Match(TokenType.RBracket))
        {
            return new NodeSet(ImmutableArray<INode>.Empty);
        }

        while (true)
        {
            elements.Add(ParseExpression());

            if (_state.Match(TokenType.Comma))
            {
                if (_state.Match(TokenType.RBracket))
                {
                    break;
                }

                continue;
            }

            _state.Expect(TokenType.RBracket, "Expected ']' after set literal.");
            break;
        }

        return new NodeSet([.. elements]);
    }

    private INode ParseRecordLiteral()
    {
        Dictionary<string, int> keys = new(StringComparer.Ordinal);
        ImmutableArray<NodeRecordElement>.Builder elements = ImmutableArray.CreateBuilder<NodeRecordElement>();

        if (_state.Match(TokenType.RBrace))
        {
            return new NodeRecord(ImmutableArray<NodeRecordElement>.Empty);
        }

        while (true)
        {
            Token keyToken = _state.Advance();
            string key = keyToken.Type switch
            {
                TokenType.Ident => keyToken.Text,
                TokenType.String => _state.ParseStringToken(keyToken),
                _ => throw _state.Error(keyToken, "Expected identifier or string key in record literal.")
            };

            if (keys.ContainsKey(key))
            {
                throw _state.Error(keyToken, $"Duplicate record key '{key}'.");
            }

            _state.Expect(TokenType.Colon, "Expected ':' after record key.");
            INode value = ParseExpression();

            keys.Add(key, elements.Count);
            elements.Add(new NodeRecordElement(new CedarString(key), value));

            if (_state.Match(TokenType.Comma))
            {
                if (_state.Match(TokenType.RBrace))
                {
                    break;
                }

                continue;
            }

            _state.Expect(TokenType.RBrace, "Expected '}' after record literal.");
            break;
        }

        return new NodeRecord(elements.ToImmutable());
    }

    private ImmutableArray<INode> ParseExpressionList(TokenType endToken, string endMessage)
    {
        List<INode> expressions = [];

        if (_state.Match(endToken))
        {
            return ImmutableArray<INode>.Empty;
        }

        while (true)
        {
            expressions.Add(ParseExpression());

            if (_state.Match(TokenType.Comma))
            {
                if (_state.Match(endToken))
                {
                    break;
                }

                continue;
            }

            _state.Expect(endToken, endMessage);
            break;
        }

        return [.. expressions];
    }

    private INode RequireOneArg(Token method, ImmutableArray<INode> args, Func<INode, INode, INode> create, INode lhs)
    {
        if (args.Length != 1)
        {
            throw _state.Error(method, $"Method '{method.Text}' expects exactly one argument.");
        }

        return create(lhs, args[0]);
    }

    private INode RequireZeroArgs(Token method, ImmutableArray<INode> args, Func<INode, INode> create, INode lhs)
    {
        if (args.Length != 0)
        {
            throw _state.Error(method, $"Method '{method.Text}' expects no arguments.");
        }

        return create(lhs);
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Core.Internal.Rust;
using Cedar.Types;

namespace Cedar.Core.Internal.Parser;

public static class CedarParser
{
    private const int MaxDepth = 256;
    private const int MaxErrors = 10;

    public static PolicyAst[] ParsePolicies(ReadOnlySpan<byte> input)
    {
        ImmutableArray<Token> tokens;
        try
        {
            tokens = CedarTokenizer.Tokenize(input);
        }
        catch (ParseException ex)
        {
            throw new AggregateException([ex]);
        }

        ParserState state = new(tokens, MaxDepth);
        List<PolicyAst> policies = [];
        List<Exception> errors = [];

        while (!state.IsAtEnd)
        {
            try
            {
                policies.Add(ParsePolicy(state));
            }
            catch (ParseException ex)
            {
                errors.Add(ex);
                if (errors.Count >= MaxErrors)
                {
                    break;
                }

                state.SynchronizeToNextPolicy();
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(errors);
        }

        return [.. policies];
    }

    public static PolicyAst[] ParsePolicies(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return ParsePolicies(Encoding.UTF8.GetBytes(input));
    }

    private static PolicyAst ParsePolicy(ParserState state)
    {
        Position position = state.Current.Position;
        ImmutableArray<Annotation> annotations = ParseAnnotations(state);
        Effect effect = ParseEffect(state);

        state.Expect(TokenType.LParen, "Expected '(' after effect.");
        IScope principal = ScopeParser.ParseScopeConstraint(state, "principal");
        state.Expect(TokenType.Comma, "Expected ',' after principal scope.");
        IScope action = ScopeParser.ParseScopeConstraint(state, "action");
        state.Expect(TokenType.Comma, "Expected ',' after action scope.");
        IScope resource = ScopeParser.ParseScopeConstraint(state, "resource");
        state.Match(TokenType.Comma);
        state.Expect(TokenType.RParen, "Expected ')' after scope tuple.");

        List<INode> conditions = [];
        while (true)
        {
            bool unless;
            if (state.Match(TokenType.When))
            {
                unless = false;
            }
            else if (state.Match(TokenType.Unless))
            {
                unless = true;
            }
            else
            {
                break;
            }

            state.Expect(TokenType.LBrace, "Expected '{' before condition expression.");
            INode condition = ExpressionParser.ParseExpression(state);
            state.Expect(TokenType.RBrace, "Expected '}' after condition expression.");
            conditions.Add(unless ? new NodeNot(condition) : condition);
        }

        state.Expect(TokenType.Semicolon, "Expected ';' after policy.");

        return new PolicyAst(effect, principal, action, resource, [.. conditions], annotations, position);
    }

    private static ImmutableArray<Annotation> ParseAnnotations(ParserState state)
    {
        List<Annotation> annotations = [];
        HashSet<string> keys = new(StringComparer.Ordinal);

        while (true)
        {
            Token token = state.Current;
            Annotation annotation;

            if (state.Match(TokenType.Annotation))
            {
                annotation = ParseCollapsedAnnotation(token);
            }
            else if (state.Match(TokenType.At))
            {
                annotation = ParseInlineAnnotation(state);
            }
            else
            {
                break;
            }

            if (!keys.Add(annotation.Key.Value))
            {
                throw new ParseException(token.Position, $"Duplicate annotation key '{annotation.Key.Value}'.");
            }

            annotations.Add(annotation);
        }

        return [.. annotations];
    }

    private static Annotation ParseInlineAnnotation(ParserState state)
    {
        Token keyToken = state.ExpectAnnotationKey();
        state.Expect(TokenType.LParen, "Expected '(' after annotation key.");
        Token valueToken = state.Expect(TokenType.String, "Expected string annotation value.");
        string value = state.ParseStringToken(valueToken);
        state.Expect(TokenType.RParen, "Expected ')' after annotation value.");

        return new Annotation(new Ident(keyToken.Text), new CedarString(value));
    }

    private static Annotation ParseCollapsedAnnotation(Token token)
    {
        ReadOnlySpan<char> text = token.Text.AsSpan();
        int index = 0;

        if (!TryReadChar(text, ref index, '@'))
        {
            throw new ParseException(token.Position, "Malformed annotation.");
        }

        SkipWhitespace(text, ref index);
        string key = ReadIdentifier(text, ref index, token);

        SkipWhitespace(text, ref index);
        if (!TryReadChar(text, ref index, '('))
        {
            throw new ParseException(token.Position, "Malformed annotation.");
        }

        SkipWhitespace(text, ref index);
        string quoted = ReadQuotedLiteral(text, ref index, token);

        string value;
        try
        {
            value = RustStringHelper.Unquote(quoted);
        }
        catch (FormatException ex)
        {
            throw new ParseException(token.Position, ex.Message);
        }

        SkipWhitespace(text, ref index);
        if (!TryReadChar(text, ref index, ')'))
        {
            throw new ParseException(token.Position, "Malformed annotation.");
        }

        SkipWhitespace(text, ref index);
        if (index != text.Length)
        {
            throw new ParseException(token.Position, "Malformed annotation.");
        }

        return new Annotation(new Ident(key), new CedarString(value));
    }

    private static Effect ParseEffect(ParserState state)
    {
        if (state.Match(TokenType.Permit))
        {
            return Effect.Permit;
        }

        if (state.Match(TokenType.Forbid))
        {
            return Effect.Forbid;
        }

        throw state.Error(state.Current, "Expected 'permit' or 'forbid'.");
    }

    private static void SkipWhitespace(ReadOnlySpan<char> text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    private static bool TryReadChar(ReadOnlySpan<char> text, ref int index, char value)
    {
        if (index >= text.Length || text[index] != value)
        {
            return false;
        }

        index++;
        return true;
    }

    private static string ReadIdentifier(ReadOnlySpan<char> text, ref int index, Token token)
    {
        if (index >= text.Length || !IsIdentifierStart(text[index]))
        {
            throw new ParseException(token.Position, "Malformed annotation.");
        }

        int start = index;
        index++;

        while (index < text.Length && IsIdentifierPart(text[index]))
        {
            index++;
        }

        return text[start..index].ToString();
    }

    private static string ReadQuotedLiteral(ReadOnlySpan<char> text, ref int index, Token token)
    {
        if (index >= text.Length || text[index] != '"')
        {
            throw new ParseException(token.Position, "Malformed annotation.");
        }

        int start = index;
        index++;
        bool escaped = false;

        while (index < text.Length)
        {
            char current = text[index];
            if (escaped)
            {
                escaped = false;
                index++;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                index++;
                continue;
            }

            if (current == '"')
            {
                index++;
                return text[start..index].ToString();
            }

            index++;
        }

        throw new ParseException(token.Position, "Malformed annotation.");
    }

    private static bool IsIdentifierStart(char value)
    {
        return value == '_' || value is >= 'A' and <= 'Z' || value is >= 'a' and <= 'z';
    }

    private static bool IsIdentifierPart(char value)
    {
        return IsIdentifierStart(value) || value is >= '0' and <= '9';
    }
}

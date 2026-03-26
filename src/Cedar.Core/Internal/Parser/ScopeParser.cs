using System.Collections.Generic;
using Cedar.Ast.Internal;
using Cedar.Types;

namespace Cedar.Core.Internal.Parser;

internal static class ScopeParser
{
    public static IScope ParseScopeConstraint(ParserState state, string expectedName)
    {
        Token variable = state.ExpectIdentifier($"Expected '{expectedName}' scope variable.");
        if (!string.Equals(variable.Text, expectedName, System.StringComparison.Ordinal))
        {
            throw state.Error(variable, $"Expected '{expectedName}' scope variable.");
        }

        if (state.Match(TokenType.EqEq))
        {
            EntityUid entity = state.ParseEntityUid();
            return new ScopeEq(entity);
        }

        if (state.Match(TokenType.In))
        {
            if (state.Match(TokenType.LBracket))
            {
                List<EntityUid> entities = [];
                if (!state.Match(TokenType.RBracket))
                {
                    while (true)
                    {
                        entities.Add(state.ParseEntityUid());

                        if (state.Match(TokenType.Comma))
                        {
                            if (state.Match(TokenType.RBracket))
                            {
                                break;
                            }

                            continue;
                        }

                        state.Expect(TokenType.RBracket, "Expected ']' after entity set.");
                        break;
                    }
                }

                return new ScopeInSet([.. entities]);
            }

            EntityUid entity = state.ParseEntityUid();
            return new ScopeIn(entity);
        }

        if (state.Match(TokenType.Is))
        {
            EntityType type = state.ParseEntityTypePath();
            if (state.Match(TokenType.In))
            {
                EntityUid entity = state.ParseEntityUid();
                return new ScopeIsIn(type, entity);
            }

            return new ScopeIs(type);
        }

        return new ScopeAll();
    }
}

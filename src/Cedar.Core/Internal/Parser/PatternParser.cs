using System;
using System.Collections.Generic;
using Cedar.Core.Internal.Rust;
using Cedar.Types;

namespace Cedar.Core.Internal.Parser;

internal static class PatternParser
{
    public static CedarPattern ParseLikePattern(ParserState state, Token token)
    {
        string raw = state.ParseRawStringToken(token);
        ReadOnlySpan<char> span = raw.AsSpan();

        List<object> components = [];
        int index = 0;

        try
        {
            while (index < span.Length)
            {
                while (index < span.Length && span[index] == '*')
                {
                    components.Add(Wildcard.Instance);
                    index++;
                }

                if (index >= span.Length)
                {
                    break;
                }

                string literal = RustStringHelper.UnescapeUntilWildcard(span, ref index);
                components.Add(literal);
            }
        }
        catch (FormatException ex)
        {
            throw new ParseException(token.Position, ex.Message);
        }

        if (components.Count == 0)
        {
            components.Add(string.Empty);
        }

        return new CedarPattern([.. components]);
    }
}

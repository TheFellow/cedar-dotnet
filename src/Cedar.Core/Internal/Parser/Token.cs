namespace Cedar.Core.Internal.Parser;

internal readonly record struct Token(TokenType Type, string Text, Position Position);

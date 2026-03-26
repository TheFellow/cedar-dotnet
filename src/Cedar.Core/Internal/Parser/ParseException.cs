using System;

namespace Cedar.Core.Internal.Parser;

internal sealed class ParseException : Exception
{
    public ParseException(Position position, string message)
        : base(FormatMessage(position, message))
    {
        Position = position;
    }

    public Position Position { get; }

    private static string FormatMessage(Position position, string message)
    {
        string filename = string.IsNullOrEmpty(position.Filename) ? "<input>" : position.Filename;
        return $"{filename}:{position.Line}:{position.Column}: {message}";
    }
}

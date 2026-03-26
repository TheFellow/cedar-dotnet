namespace Cedar.Core;

public readonly record struct Position(string Filename, int Offset, int Line, int Column);

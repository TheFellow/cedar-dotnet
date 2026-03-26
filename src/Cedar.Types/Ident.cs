using System;

namespace Cedar.Types;

public readonly record struct Ident
{
    public Ident(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString()
    {
        return Value;
    }
}

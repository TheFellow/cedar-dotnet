using System;

namespace Cedar.Types;

public readonly record struct EntityType
{
    public EntityType(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString()
    {
        return Value;
    }
}

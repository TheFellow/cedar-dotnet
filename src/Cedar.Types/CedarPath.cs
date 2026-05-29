using System;
using System.Collections.Generic;

namespace Cedar.Types;

public readonly record struct CedarPath
{
    public CedarPath(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public string Value { get; }

    public static CedarPath FromSegments(IEnumerable<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        return new CedarPath(string.Join("::", segments));
    }

    public override string ToString()
    {
        return Value;
    }
}

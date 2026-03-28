using System;
using System.Diagnostics.CodeAnalysis;
using Cedar.Core.Internal.Rust;

namespace Cedar.Types;

public sealed record EntityUid : ICedarData
{
    public EntityUid(EntityType type, CedarString id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Type = type;
        Id = id;
    }

    public EntityType Type { get; }

    public CedarString Id { get; }

    public static bool TryParseCedar(string input, [NotNullWhen(true)] out EntityUid? result)
    {
        ArgumentNullException.ThrowIfNull(input);

        int index = input.IndexOf("::\"", StringComparison.Ordinal);
        if (index <= 0)
        {
            result = null;
            return false;
        }

        string type = input[..index];
        string quoted = input[(index + 2)..];
        if (quoted.Length < 2 || quoted[0] != '"' || quoted[^1] != '"')
        {
            result = null;
            return false;
        }

        try
        {
            string id = RustStringHelper.Unquote(quoted);
            result = new EntityUid(new EntityType(type), new CedarString(id));
            return true;
        }
        catch (FormatException)
        {
            result = null;
            return false;
        }
    }

    public static EntityUid ParseCedar(string input)
    {
        return TryParseCedar(input, out EntityUid? result)
            ? result
            : throw new FormatException($"Invalid Cedar EntityUid: '{input}'");
    }

    public string MarshalCedar()
    {
        return Type.Value + "::" + Id.MarshalCedar();
    }

    public override string ToString()
    {
        return MarshalCedar();
    }

    public override int GetHashCode()
    {
        return CedarHash.ForStringPair(nameof(EntityUid), Type.Value, Id.Value);
    }
}

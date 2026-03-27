using System;
using System.Diagnostics.CodeAnalysis;

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

        int index = input.LastIndexOf("::\"", StringComparison.Ordinal);
        if (index <= 0 || !input.EndsWith('"'))
        {
            result = null;
            return false;
        }

        string type = input[..index];
        string id = input[(index + 3)..^1];
        result = new EntityUid(new EntityType(type), new CedarString(id));
        return true;
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

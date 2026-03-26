using System;

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

namespace Cedar.Types;

public sealed record CedarBool(bool Value) : CedarValue
{
    public static CedarBool True { get; } = new(true);

    public static CedarBool False { get; } = new(false);

    public override string MarshalCedar()
    {
        return Value ? "true" : "false";
    }

    public override int GetHashCode()
    {
        return CedarHash.ForBoolean(nameof(CedarBool), Value);
    }
}

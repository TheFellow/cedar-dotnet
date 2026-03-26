using System.Globalization;

namespace Cedar.Types;

public sealed record CedarLong(long Value) : CedarValue
{
    public override string MarshalCedar()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }

    public override int GetHashCode()
    {
        return CedarHash.ForInt64(nameof(CedarLong), Value);
    }
}

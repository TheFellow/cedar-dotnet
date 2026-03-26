namespace Cedar.Types;

public abstract record CedarValue : ICedarData
{
    public abstract string MarshalCedar();

    public sealed override string ToString()
    {
        return MarshalCedar();
    }

    public abstract override int GetHashCode();
}

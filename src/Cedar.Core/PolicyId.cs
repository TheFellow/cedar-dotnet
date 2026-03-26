namespace Cedar.Core;

public readonly record struct PolicyId(string Value)
{
    public override string ToString()
    {
        return Value;
    }
}

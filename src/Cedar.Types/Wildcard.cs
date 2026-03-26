namespace Cedar.Types;

public sealed class Wildcard
{
    public static Wildcard Instance { get; } = new();

    private Wildcard()
    {
    }
}

namespace Cedar.Core;

public static class PolicyList
{
    public static Policy[] ParseCedar(string cedarText)
    {
        return Policy.UnmarshalCedarList(cedarText);
    }
}

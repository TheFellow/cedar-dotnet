using Cedar.Types;
using Xunit;

namespace Cedar.Tests.TestSupport;

internal static class CedarAssert
{
    public static void Equal<T>(T expected, T actual)
        where T : CedarValue
    {
        Assert.Equal(expected, actual);
        Assert.True(expected.Equals(actual));
        Assert.True(actual.Equals(expected));
        Assert.Equal(expected.GetHashCode(), actual.GetHashCode());
    }

    public static void NotEqual<TLeft, TRight>(TLeft left, TRight right)
        where TLeft : CedarValue
        where TRight : CedarValue
    {
        Assert.False(left.Equals(right));
        Assert.False(right.Equals(left));
        Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
    }

    public static void CedarText(CedarValue value, string expected)
    {
        Assert.Equal(expected, value.MarshalCedar());
        Assert.Equal(expected, value.ToString());
    }

    public static void HashStable(CedarValue value)
    {
        int first = value.GetHashCode();
        int second = value.GetHashCode();
        int third = value.GetHashCode();

        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }
}

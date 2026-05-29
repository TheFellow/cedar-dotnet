using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class CedarPathTests
{
    [Fact]
    public void FromSegments_JoinsWithDoubleColon()
    {
        CedarPath path = CedarPath.FromSegments(["X", "Y"]);

        Assert.Equal(new CedarPath("X::Y"), path);
    }
}

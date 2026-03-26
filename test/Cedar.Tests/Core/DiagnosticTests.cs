using Cedar.Core;
using Xunit;

namespace Cedar.Tests.Core;

public sealed class DiagnosticTests
{
    [Fact]
    public void DecisionEnumContainsAllowAndDeny()
    {
        Assert.NotEqual(Decision.Allow, Decision.Deny);
    }

    [Fact]
    public void EffectEnumContainsPermitAndForbid()
    {
        Assert.NotEqual(Effect.Permit, Effect.Forbid);
    }

    [Fact]
    public void PositionStoresSourceLocation()
    {
        Position position = new("policy.cedar", 12, 3, 5);

        Assert.Equal("policy.cedar", position.Filename);
        Assert.Equal(12, position.Offset);
        Assert.Equal(3, position.Line);
        Assert.Equal(5, position.Column);
    }

    [Fact]
    public void PolicyIdToStringReturnsUnderlyingValue()
    {
        Assert.Equal("policy42", new PolicyId("policy42").ToString());
    }

    [Fact]
    public void EmptyDiagnosticHasNoReasonsOrErrors()
    {
        Diagnostic diagnostic = new();

        Assert.Empty(diagnostic.Reasons);
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void DiagnosticStoresReasonsAndErrors()
    {
        DiagnosticReason reason = new(new PolicyId("permit"), new Position("a.cedar", 0, 1, 1));
        DiagnosticError error = new(new PolicyId("forbid"), new Position("b.cedar", 10, 2, 3), "boom");
        Diagnostic diagnostic = new([reason], [error]);

        Assert.Single(diagnostic.Reasons);
        Assert.Single(diagnostic.Errors);
        Assert.Equal(reason, diagnostic.Reasons[0]);
        Assert.Equal(error, diagnostic.Errors[0]);
    }

    [Fact]
    public void DiagnosticErrorToStringIncludesPolicyAndMessage()
    {
        DiagnosticError error = new(new PolicyId("policy42"), new Position("", 0, 0, 0), "bad error");

        Assert.Equal("while evaluating policy `policy42`: bad error", error.ToString());
    }
}

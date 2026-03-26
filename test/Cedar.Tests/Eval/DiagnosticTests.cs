using System.Collections.Immutable;
using Cedar.Core;
using Xunit;

namespace Cedar.Tests.Eval;

public sealed class DiagnosticTests
{
    private static readonly Position Pos = new("test.cedar", 0, 1, 1);

    [Fact]
    public void EmptyDiagnostic_HasNoReasonsOrErrors()
    {
        Diagnostic diagnostic = new();
        Assert.Empty(diagnostic.Reasons);
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void DiagnosticEmpty_IsSameAsDefaultConstructor()
    {
        Diagnostic diagnostic = Diagnostic.Empty;
        Assert.Empty(diagnostic.Reasons);
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void Diagnostic_WithReasons_StoresReasons()
    {
        DiagnosticReason reason1 = new(new PolicyId("p1"), Pos);
        DiagnosticReason reason2 = new(new PolicyId("p2"), Pos);
        Diagnostic diagnostic = new(ImmutableArray.Create(reason1, reason2), ImmutableArray<DiagnosticError>.Empty);
        Assert.Equal(2, diagnostic.Reasons.Length);
        Assert.Equal("p1", diagnostic.Reasons[0].PolicyId.Value);
        Assert.Equal("p2", diagnostic.Reasons[1].PolicyId.Value);
    }

    [Fact]
    public void Diagnostic_WithErrors_StoresErrors()
    {
        DiagnosticError error = new(new PolicyId("e1"), Pos, "something went wrong");
        Diagnostic diagnostic = new(ImmutableArray<DiagnosticReason>.Empty, ImmutableArray.Create(error));
        Assert.Single(diagnostic.Errors);
        Assert.Equal("something went wrong", diagnostic.Errors[0].Message);
    }

    [Fact]
    public void Diagnostic_WithBothReasonsAndErrors()
    {
        DiagnosticReason reason = new(new PolicyId("p1"), Pos);
        DiagnosticError error = new(new PolicyId("e1"), Pos, "error msg");
        Diagnostic diagnostic = new(ImmutableArray.Create(reason), ImmutableArray.Create(error));
        Assert.Single(diagnostic.Reasons);
        Assert.Single(diagnostic.Errors);
    }

    [Fact]
    public void DiagnosticReason_StoresPolicyIdAndPosition()
    {
        PolicyId id = new("permit1");
        Position position = new("policy.cedar", 10, 3, 5);
        DiagnosticReason reason = new(id, position);
        Assert.Equal("permit1", reason.PolicyId.Value);
        Assert.Equal("policy.cedar", reason.Position.Filename);
        Assert.Equal(10, reason.Position.Offset);
        Assert.Equal(3, reason.Position.Line);
        Assert.Equal(5, reason.Position.Column);
    }

    [Fact]
    public void DiagnosticError_StoresPolicyIdPositionAndMessage()
    {
        PolicyId id = new("forbid1");
        Position position = new("policy.cedar", 20, 5, 1);
        DiagnosticError error = new(id, position, "bad error");
        Assert.Equal("forbid1", error.PolicyId.Value);
        Assert.Equal(20, error.Position.Offset);
        Assert.Equal("bad error", error.Message);
    }

    [Fact]
    public void DiagnosticError_ToString_IncludesPolicyIdAndMessage()
    {
        DiagnosticError error = new(new PolicyId("policy42"), Pos, "something broke");
        Assert.Equal("while evaluating policy `policy42`: something broke", error.ToString());
    }

    [Fact]
    public void DiagnosticReason_Equality_SamePolicyAndPosition()
    {
        DiagnosticReason r1 = new(new PolicyId("p1"), Pos);
        DiagnosticReason r2 = new(new PolicyId("p1"), Pos);
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void DiagnosticError_Equality_SameValues()
    {
        DiagnosticError e1 = new(new PolicyId("e1"), Pos, "msg");
        DiagnosticError e2 = new(new PolicyId("e1"), Pos, "msg");
        Assert.Equal(e1, e2);
    }
}

using System;
using Cedar.Core;
using Xunit;

namespace Cedar.Tests.PolicyApi;

public sealed class PolicyListTests
{
    [Fact]
    public void ParseCedar_ReadsMultiplePolicies()
    {
        Policy[] policies = PolicyList.ParseCedar("permit(principal, action, resource); forbid(principal, action, resource);");

        Assert.Equal(2, policies.Length);
        Assert.Equal(Effect.Permit, policies[0].Effect);
        Assert.Equal(Effect.Forbid, policies[1].Effect);
    }

    [Fact]
    public void ParseCedar_PreservesOrder()
    {
        Policy[] policies = PolicyList.ParseCedar("forbid(principal, action, resource); permit(principal, action, resource);");

        Assert.Equal(Effect.Forbid, policies[0].Effect);
        Assert.Equal(Effect.Permit, policies[1].Effect);
    }

    [Fact]
    public void ParseCedar_ParsesConditions()
    {
        Policy[] policies = PolicyList.ParseCedar("permit(principal, action, resource) when { true };\nforbid(principal, action, resource) unless { false };");

        Assert.Contains("when", policies[0].MarshalCedar(), StringComparison.Ordinal);
        Assert.Contains("unless", policies[1].MarshalCedar(), StringComparison.Ordinal);
    }

    [Fact]
    public void ParseCedar_ParsesAnnotations()
    {
        Policy[] policies = PolicyList.ParseCedar("@id(\"p0\") permit(principal, action, resource);");

        Assert.True(policies[0].Annotations.ContainsKey(new Cedar.Types.Ident("id")));
    }

    [Fact]
    public void ParseCedar_InvalidDocumentThrows()
    {
        Assert.Throws<AggregateException>(() => PolicyList.ParseCedar("!@#$"));
    }

    [Fact]
    public void ParseCedar_EmptyDocumentThrows()
    {
        Assert.Throws<ArgumentException>(() => PolicyList.ParseCedar(string.Empty));
    }
}

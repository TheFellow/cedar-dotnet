using System;
using System.Text.Json;
using Cedar.Core;
using Xunit;

namespace Cedar.Tests.Json;

public sealed class PolicySetJsonTests
{
    [Fact]
    public void MarshalJson_WrapsPoliciesInStaticPoliciesObject()
    {
        PolicySet set = new();
        set.Add(new PolicyId("policy0"), Policy.UnmarshalCedar("permit(principal, action, resource);"));

        string json = set.MarshalJson();

        Assert.Contains("\"staticPolicies\":{", json, StringComparison.Ordinal);
        Assert.Contains("\"policy0\":{\"effect\":\"permit\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MarshalJson_SortsPolicyIdsDeterministically()
    {
        PolicySet set = new();
        set.Add(new PolicyId("z"), Policy.UnmarshalCedar("permit(principal, action, resource);"));
        set.Add(new PolicyId("a"), Policy.UnmarshalCedar("forbid(principal, action, resource);"));

        string json = set.MarshalJson();

        Assert.True(json.IndexOf("\"a\"", StringComparison.Ordinal) < json.IndexOf("\"z\"", StringComparison.Ordinal));
    }

    [Fact]
    public void MarshalJson_IsStableAcrossCalls()
    {
        PolicySet set = new();
        set.Add(new PolicyId("b"), Policy.UnmarshalCedar("permit(principal, action, resource);"));
        set.Add(new PolicyId("a"), Policy.UnmarshalCedar("forbid(principal, action, resource);"));

        string first = set.MarshalJson();
        string second = set.MarshalJson();

        Assert.Equal(first, second);
    }

    [Fact]
    public void UnmarshalJson_ReadsPolicySet()
    {
        const string json = "{\"staticPolicies\":{\"p0\":{\"effect\":\"permit\",\"principal\":{\"op\":\"All\"},\"action\":{\"op\":\"All\"},\"resource\":{\"op\":\"All\"}},\"p1\":{\"effect\":\"forbid\",\"principal\":{\"op\":\"All\"},\"action\":{\"op\":\"All\"},\"resource\":{\"op\":\"All\"}}}}";

        PolicySet set = PolicySet.UnmarshalJson(json);

        Assert.NotNull(set.Get(new PolicyId("p0")));
        Assert.NotNull(set.Get(new PolicyId("p1")));
        Assert.Equal(Effect.Permit, set.Get(new PolicyId("p0"))!.Effect);
        Assert.Equal(Effect.Forbid, set.Get(new PolicyId("p1"))!.Effect);
    }

    [Fact]
    public void UnmarshalJson_EmptySetIsAllowed()
    {
        PolicySet set = PolicySet.UnmarshalJson("{\"staticPolicies\":{}}\n");

        Assert.Empty(set.All());
    }

    [Fact]
    public void RoundTrip_JsonToPolicySetToJson()
    {
        PolicySet original = new();
        original.Add(new PolicyId("p0"), Policy.UnmarshalCedar("permit(principal, action, resource) when { true };"));
        original.Add(new PolicyId("p1"), Policy.UnmarshalCedar("forbid(principal, action, resource) unless { false };"));

        PolicySet roundTripped = PolicySet.UnmarshalJson(original.MarshalJson());

        Assert.Equal(original.MarshalJson(), roundTripped.MarshalJson());
    }

    [Fact]
    public void UnmarshalJson_InvalidPayloadThrows()
    {
        Assert.Throws<JsonException>(() => PolicySet.UnmarshalJson("!@#$"));
    }

    [Fact]
    public void MarshalJson_ContainsNestedPolicyJson()
    {
        PolicySet set = new();
        set.Add(new PolicyId("p0"), Policy.UnmarshalCedar("permit(principal == User::\"alice\", action, resource) when { context.attr == \"v\" };"));

        using JsonDocument document = JsonDocument.Parse(set.MarshalJson());
        JsonElement policy = document.RootElement.GetProperty("staticPolicies").GetProperty("p0");

        Assert.Equal("permit", policy.GetProperty("effect").GetString());
        Assert.Equal("==", policy.GetProperty("principal").GetProperty("op").GetString());
        Assert.Equal("when", policy.GetProperty("conditions")[0].GetProperty("kind").GetString());
    }
}

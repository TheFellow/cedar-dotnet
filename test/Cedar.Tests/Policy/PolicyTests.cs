using System;
using System.Text.Json;
using Cedar.Core;
using Xunit;

namespace Cedar.Tests.PolicyApi;

public sealed class PolicyTests
{
    [Fact]
    public void UnmarshalCedar_ReadsSinglePolicy()
    {
        Policy policy = Policy.UnmarshalCedar("permit(principal, action, resource);");

        Assert.Equal(Effect.Permit, policy.Effect);
    }

    [Fact]
    public void UnmarshalCedar_ThrowsWhenMultiplePoliciesProvided()
    {
        Assert.Throws<ArgumentException>(() => Policy.UnmarshalCedar("permit(principal, action, resource); forbid(principal, action, resource);"));
    }

    [Fact]
    public void UnmarshalCedarList_ReadsMultiplePolicies()
    {
        Policy[] policies = Policy.UnmarshalCedarList("permit(principal, action, resource); forbid(principal, action, resource);");

        Assert.Equal(2, policies.Length);
        Assert.Equal(Effect.Permit, policies[0].Effect);
        Assert.Equal(Effect.Forbid, policies[1].Effect);
    }

    [Fact]
    public void MarshalCedar_RoundTripsCanonicalText()
    {
        const string cedar = "permit(principal, action, resource) when { true } unless { false };";
        const string expected = "permit(principal, action, resource)\n  when { true }\n  unless { false };";

        Policy policy = Policy.UnmarshalCedar(cedar);

        Assert.Equal(expected, policy.MarshalCedar());
    }

    [Fact]
    public void MarshalJson_RoundTripsThroughUnmarshalJson()
    {
        Policy original = Policy.UnmarshalCedar("permit(principal, action, resource) when { context.attr == \"v\" };");

        Policy roundTripped = Policy.UnmarshalJson(original.MarshalJson());

        Assert.Equal(original.MarshalCedar(), roundTripped.MarshalCedar());
    }

    [Fact]
    public void CrossFormat_CedarToJsonToCedar()
    {
        const string cedar = "@id(\"p0\") forbid(principal in User::\"alice\", action, resource) unless { context.flag };";
        string expected = Policy.UnmarshalCedar(cedar).MarshalCedar();

        string cedarOut = Policy.UnmarshalJson(Policy.UnmarshalCedar(cedar).MarshalJson()).MarshalCedar();

        Assert.Equal(expected, cedarOut);
    }

    [Fact]
    public void Properties_ExposeEffectAnnotationsAndPosition()
    {
        const string cedar = "@env(\"prod\") permit(principal, action, resource);";

        Policy policy = Policy.UnmarshalCedar(cedar);

        Assert.Equal(Effect.Permit, policy.Effect);
        Assert.True(policy.Annotations.ContainsKey(new Cedar.Types.Ident("env")));
        Assert.Equal("prod", policy.Annotations[new Cedar.Types.Ident("env")].Value);
        Assert.Equal(1, policy.Position.Line);
    }

    [Fact]
    public void UnmarshalJson_InvalidEffectThrows()
    {
        const string json = "{\"effect\":\"allow\",\"principal\":{\"op\":\"All\"},\"action\":{\"op\":\"All\"},\"resource\":{\"op\":\"All\"}}";

        Assert.Throws<JsonException>(() => Policy.UnmarshalJson(json));
    }

    [Fact]
    public void UnmarshalJson_InvalidConditionKindThrows()
    {
        const string json = "{\"effect\":\"permit\",\"principal\":{\"op\":\"All\"},\"action\":{\"op\":\"All\"},\"resource\":{\"op\":\"All\"},\"conditions\":[{\"kind\":\"because\",\"body\":{\"Value\":true}}]}";

        Assert.Throws<JsonException>(() => Policy.UnmarshalJson(json));
    }

    [Fact]
    public void MarshalJson_SortsAnnotationsDeterministically()
    {
        Policy policy = Policy.UnmarshalCedar("@z(\"last\") @a(\"first\") permit(principal, action, resource);");

        string json = policy.MarshalJson();

        Assert.True(json.IndexOf("\"a\":\"first\"", StringComparison.Ordinal) < json.IndexOf("\"z\":\"last\"", StringComparison.Ordinal));
    }

    [Fact]
    public void UnmarshalJson_AssignsDefaultPosition()
    {
        Policy policy = Policy.UnmarshalJson("{\"effect\":\"permit\",\"principal\":{\"op\":\"All\"},\"action\":{\"op\":\"All\"},\"resource\":{\"op\":\"All\"}}\n");

        Assert.Equal(new Position(string.Empty, 0, 0, 0), policy.Position);
    }

    [Fact]
    public void MarshalJson_UsesUnlessForTopLevelNotCondition()
    {
        string json = Policy.UnmarshalCedar("permit(principal, action, resource) unless { principal };").MarshalJson();

        Assert.Contains("\"kind\":\"unless\"", json, StringComparison.Ordinal);
    }
}

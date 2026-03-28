using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cedar.Ast;
using Cedar.Core;
using Xunit;

namespace Cedar.Tests.PolicyApi;

public sealed class PolicySetTests
{
    [Fact]
    public void AddAndGet_ReturnInsertedPolicy()
    {
        PolicySet set = new();
        Policy policy = Policy.UnmarshalCedar("permit(principal, action, resource);");

        set.Add(new PolicyId("p0"), policy);

        Assert.Same(policy, set.Get(new PolicyId("p0")));
    }

    [Fact]
    public void Add_OverwritesExistingPolicy()
    {
        PolicySet set = new();
        Policy first = Policy.UnmarshalCedar("permit(principal, action, resource);");
        Policy second = Policy.UnmarshalCedar("forbid(principal, action, resource);");

        set.Add(new PolicyId("p0"), first);
        set.Add(new PolicyId("p0"), second);

        Assert.Same(second, set.Get(new PolicyId("p0")));
    }

    [Fact]
    public void Remove_ReturnsFalseWhenMissing()
    {
        PolicySet set = new();

        Assert.False(set.Remove(new PolicyId("missing")));
    }

    [Fact]
    public void Remove_RemovesExistingPolicy()
    {
        PolicySet set = new();
        set.Add(new PolicyId("p0"), Policy.UnmarshalCedar("permit(principal, action, resource);"));

        Assert.True(set.Remove(new PolicyId("p0")));
        Assert.Null(set.Get(new PolicyId("p0")));
    }

    [Fact]
    public void All_EnumeratesPolicies()
    {
        PolicySet set = new();
        set.Add(new PolicyId("p0"), Policy.UnmarshalCedar("permit(principal, action, resource);"));
        set.Add(new PolicyId("p1"), Policy.UnmarshalCedar("forbid(principal, action, resource);"));

        Dictionary<string, Policy> collected = set.All().ToDictionary(static entry => entry.Key.Value, static entry => entry.Value);

        Assert.Equal(2, collected.Count);
        Assert.Contains("p0", collected.Keys);
        Assert.Contains("p1", collected.Keys);
    }

    [Fact]
    public void MarshalCedar_SortsByPolicyId()
    {
        PolicySet set = new();
        set.Add(new PolicyId("z"), Policy.UnmarshalCedar("forbid(principal, action, resource);"));
        set.Add(new PolicyId("a"), Policy.UnmarshalCedar("permit(principal, action, resource);"));

        string cedar = set.MarshalCedar();

        Assert.StartsWith("permit(principal, action, resource);", cedar, StringComparison.Ordinal);
        Assert.Contains("\n\nforbid(principal, action, resource);", cedar, StringComparison.Ordinal);
    }

    [Fact]
    public void MarshalJson_SortsByPolicyId()
    {
        PolicySet set = new();
        set.Add(new PolicyId("z"), Policy.UnmarshalCedar("permit(principal, action, resource);"));
        set.Add(new PolicyId("a"), Policy.UnmarshalCedar("forbid(principal, action, resource);"));

        string json = set.MarshalJson();

        Assert.True(json.IndexOf("\"a\"", StringComparison.Ordinal) < json.IndexOf("\"z\"", StringComparison.Ordinal));
    }

    [Fact]
    public void UnmarshalCedarWithIds_UsesProvidedIds()
    {
        PolicySet set = PolicySet.UnmarshalCedarWithIds(
        [
            new KeyValuePair<string, string>("read", "permit(principal, action, resource);"),
            new KeyValuePair<string, string>("write", "forbid(principal, action, resource);")
        ]);

        Assert.NotNull(set.Get(new PolicyId("read")));
        Assert.NotNull(set.Get(new PolicyId("write")));
    }

    [Fact]
    public void PoliciesProperty_EnumeratesValues()
    {
        PolicySet set = new();
        set.Add(new PolicyId("p0"), Policy.UnmarshalCedar("permit(principal, action, resource);"));
        set.Add(new PolicyId("p1"), Policy.UnmarshalCedar("forbid(principal, action, resource);"));

        Assert.Equal(2, set.Policies.Count());
    }

    [Fact]
    public async Task ConcurrentAdd_IsThreadSafe()
    {
        PolicySet set = new();

        Task[] tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() =>
                set.Add(new PolicyId($"p{i}"), Policy.UnmarshalCedar("permit(principal, action, resource);"))))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(100, set.All().Count());
    }

    [Fact]
    public void ParseCedar_AssignsGeneratedIdsInOrder()
    {
        PolicySet set = PolicySet.ParseCedar("permit(principal, action, resource);\n\nforbid(principal, action, resource);");

        Policy? first = set.Get(new PolicyId("policy0"));
        Policy? second = set.Get(new PolicyId("policy1"));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(Effect.Permit, first.Effect);
        Assert.Equal(Effect.Forbid, second.Effect);
    }

    [Fact]
    public void ParseCedar_MarshalCedarRoundTripsWithGeneratedIds()
    {
        const string cedarText = "permit(principal, action, resource);\n\nforbid(principal, action, resource);";

        PolicySet set = PolicySet.ParseCedar(cedarText);

        Assert.Equal(cedarText, set.MarshalCedar());
    }

    [Fact]
    public void ParseCedarFile_AssignsGeneratedIdsAndUpdatesFilename()
    {
        PolicySet set = PolicySet.ParseCedarFile("example.cedar", "permit(principal, action, resource);\n\nforbid(principal, action, resource);");

        Policy? first = set.Get(new PolicyId("policy0"));
        Policy? second = set.Get(new PolicyId("policy1"));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("example.cedar", first.Position.Filename);
        Assert.Equal("example.cedar", second.Position.Filename);
        Assert.Equal(Effect.Permit, first.Effect);
        Assert.Equal(Effect.Forbid, second.Effect);
    }

    [Fact]
    public void UpsertPolicy_InsertsNewPolicy()
    {
        PolicySet set = new();
        Policy policy = Policy.UnmarshalCedar("forbid(principal, action, resource);");

        set.UpsertPolicy(new PolicyId("a very strict policy"), policy);

        Assert.Same(policy, set.Get(new PolicyId("a very strict policy")));
    }

    [Fact]
    public void UpsertPolicy_ReplacesExistingPolicy()
    {
        PolicySet set = new();
        Policy first = Policy.UnmarshalCedar("forbid(principal, action, resource);");
        Policy second = Policy.UnmarshalCedar("permit(principal, action, resource);");

        set.UpsertPolicy(new PolicyId("a wavering policy"), first);
        set.UpsertPolicy(new PolicyId("a wavering policy"), second);

        Assert.Same(second, set.Get(new PolicyId("a wavering policy")));
    }

    [Fact]
    public void UpsertPolicySet_IntoEmptySet_ContainsAllSourcePolicies()
    {
        Policy policy0 = Policy.FromAst(CedarAst.Forbid());
        Policy policy1 = Policy.UnmarshalJson(
            """
            {"effect":"permit","principal":{"op":"All"},"action":{"op":"All"},"resource":{"op":"All"}}
            """);

        PolicySet source = new();
        source.UpsertPolicy(new PolicyId("policy0"), policy0);
        source.UpsertPolicy(new PolicyId("policy1"), policy1);

        PolicySet destination = new();
        destination.UpsertPolicySet(source);

        Assert.Same(policy0, destination.Get(new PolicyId("policy0")));
        Assert.Same(policy1, destination.Get(new PolicyId("policy1")));
        Assert.Null(destination.Get(new PolicyId("policy2")));
    }

    [Fact]
    public void UpsertPolicySet_ClobbersExistingOnIdCollision()
    {
        Policy policyA = Policy.FromAst(CedarAst.Forbid());
        Policy policyB = Policy.UnmarshalJson(
            """
            {"effect":"permit","principal":{"op":"All"},"action":{"op":"All"},"resource":{"op":"All"}}
            """);
        Policy policyC = Policy.FromAst(CedarAst.Permit());

        PolicySet source = new();
        source.UpsertPolicy(new PolicyId("policy0"), policyA);
        source.UpsertPolicy(new PolicyId("policy1"), policyB);

        PolicySet destination = new();
        destination.UpsertPolicy(new PolicyId("policy0"), policyB);
        destination.UpsertPolicy(new PolicyId("policy2"), policyC);

        destination.UpsertPolicySet(source);

        Assert.Same(policyA, destination.Get(new PolicyId("policy0")));
        Assert.Same(policyB, destination.Get(new PolicyId("policy1")));
        Assert.Same(policyC, destination.Get(new PolicyId("policy2")));
    }

    [Fact]
    public void FromPolicies_EmptyCollection_ReturnsEmptySet()
    {
        PolicySet set = PolicySet.FromPolicies([]);

        Assert.Null(set.Get(new PolicyId("policy0")));
    }

    [Fact]
    public void FromPolicies_AssignsSequentialIds()
    {
        Policy first = Policy.FromAst(CedarAst.Forbid());
        Policy second = Policy.UnmarshalJson(
            """
            {"effect":"permit","principal":{"op":"All"},"action":{"op":"All"},"resource":{"op":"All"}}
            """);

        PolicySet set = PolicySet.FromPolicies([first, second]);

        Assert.Same(first, set.Get(new PolicyId("policy0")));
        Assert.Same(second, set.Get(new PolicyId("policy1")));
        Assert.Null(set.Get(new PolicyId("policy2")));
    }
}

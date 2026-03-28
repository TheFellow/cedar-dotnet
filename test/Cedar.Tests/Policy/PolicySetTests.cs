using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
}

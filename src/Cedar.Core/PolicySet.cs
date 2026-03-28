using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Cedar.Ast.Internal;
using Cedar.Core.Internal.Json;

namespace Cedar.Core;

public sealed class PolicySet : IPolicyIterator
{
    private readonly ConcurrentDictionary<PolicyId, Policy> _policies;

    public PolicySet()
    {
        _policies = new ConcurrentDictionary<PolicyId, Policy>();
    }

    public IEnumerable<Policy> Policies => _policies.Values;

    public void Add(PolicyId id, Policy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policies[id] = policy;
    }

    public Policy? Get(PolicyId id)
    {
        return _policies.TryGetValue(id, out Policy? policy) ? policy : null;
    }

    public bool Remove(PolicyId id)
    {
        return _policies.TryRemove(id, out _);
    }

    public IEnumerable<KeyValuePair<PolicyId, Policy>> All()
    {
        return _policies;
    }

    public string MarshalCedar()
    {
        List<PolicyId> ids = [.. _policies.Keys];
        ids.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value));

        StringBuilder builder = new();
        for (int i = 0; i < ids.Count; i++)
        {
            if (!_policies.TryGetValue(ids[i], out Policy? policy) || policy is null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(policy.MarshalCedar());
        }

        return builder.ToString();
    }

    public string MarshalJson()
    {
        SortedDictionary<string, PolicyJsonModel> models = new(StringComparer.Ordinal);
        foreach (KeyValuePair<PolicyId, Policy> entry in _policies)
        {
            models[entry.Key.Value] = PolicyJsonModel.FromAst(entry.Value.Ast);
        }

        PolicySetJsonModel payload = new()
        {
            StaticPolicies = models
        };

        return JsonSerializer.Serialize(payload, PolicyJsonSerializerOptions.Instance);
    }

    public static PolicySet UnmarshalCedarWithIds(IEnumerable<KeyValuePair<string, string>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        PolicySet result = new();
        foreach (KeyValuePair<string, string> entry in entries)
        {
            result.Add(new PolicyId(entry.Key), Policy.UnmarshalCedar(entry.Value));
        }

        return result;
    }

    public static PolicySet ParseCedar(string cedarText)
    {
        ArgumentException.ThrowIfNullOrEmpty(cedarText);

        Policy[] policies = PolicyList.ParseCedar(cedarText);
        PolicySet result = new();

        for (int i = 0; i < policies.Length; i++)
        {
            result.Add(new PolicyId($"policy{i}"), policies[i]);
        }

        return result;
    }

    public static PolicySet ParseCedarFile(string filename, string cedarText)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);
        ArgumentException.ThrowIfNullOrEmpty(cedarText);

        Policy[] policies = PolicyList.ParseCedar(cedarText);
        PolicySet result = new();

        for (int i = 0; i < policies.Length; i++)
        {
            Policy policy = policies[i];
            Position position = policy.Position;
            PolicyAst ast = policy.Ast with
            {
                Position = new Position(filename, position.Offset, position.Line, position.Column)
            };

            result.Add(new PolicyId($"policy{i}"), new Policy(ast));
        }

        return result;
    }

    public static PolicySet UnmarshalJson(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);

        PolicySetJsonModel payload = JsonSerializer.Deserialize<PolicySetJsonModel>(json, PolicyJsonSerializerOptions.Instance)
            ?? throw new JsonException("Policy set JSON deserialized to null.");

        PolicySet set = new();
        foreach (KeyValuePair<string, PolicyJsonModel> entry in payload.StaticPolicies)
        {
            set.Add(new PolicyId(entry.Key), new Policy(entry.Value.ToAst()));
        }

        return set;
    }
}

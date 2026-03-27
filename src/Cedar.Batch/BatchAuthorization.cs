using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Core.Internal.Eval;
using Cedar.Types;

namespace Cedar.Batch;

public static class BatchAuthorization
{
    private static readonly EntityType UnknownEntityType = new("__cedar::unknown");

    public static void Authorize(
        IPolicyIterator policies,
        IEntityGetter? entities,
        BatchRequest request,
        Action<BatchResult> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(callback);

        ValidateRequest(request);
        if (request.Variables.Values.Any(static values => values.Count == 0))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IEntityGetter effectiveEntities = entities ?? new EntityMap();
        IReadOnlyDictionary<PolicyId, Policy> policyMap = EnumeratePolicies(policies);
        EvalEnv env = new(
            effectiveEntities,
            request.Principal!,
            request.Action!,
            request.Resource!,
            request.Context!);

        VariableItem[] variables = request.Variables
            .Select(static entry => new VariableItem(entry.Key, [.. entry.Value]))
            .OrderBy(static entry => entry.Values.Length)
            .ToArray();

        if (variables.Length == 0)
        {
            IReadOnlyDictionary<PolicyId, Policy> partialPolicies = PartialPolicies(policyMap, env);
            EmitResult(partialPolicies, FixIgnores(env), new Dictionary<string, ICedarData>(StringComparer.Ordinal), callback, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        Execute(
            policyMap,
            env,
            variables,
            0,
            new Dictionary<string, ICedarData>(StringComparer.Ordinal),
            callback,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Execute(
        IReadOnlyDictionary<PolicyId, Policy> policies,
        EvalEnv env,
        VariableItem[] variables,
        int index,
        Dictionary<string, ICedarData> values,
        Action<BatchResult> callback,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (index == variables.Length)
        {
            EmitResult(policies, env, values, callback, cancellationToken);
            return;
        }

        IReadOnlyDictionary<PolicyId, Policy> partialPolicies = PartialPolicies(policies, env);
        EvalEnv loopEnv = variables.Length - index == 1 ? FixIgnores(env) : env;
        VariableItem variable = variables[index];

        foreach (ICedarData candidate in variable.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            values[variable.Key] = candidate;
            EvalEnv nextEnv = Substitute(loopEnv, variable.Key, candidate);
            Execute(partialPolicies, nextEnv, variables, index + 1, values, callback, cancellationToken);
        }

        values.Remove(variable.Key);
    }

    private static void EmitResult(
        IReadOnlyDictionary<PolicyId, Policy> policies,
        EvalEnv env,
        IReadOnlyDictionary<string, ICedarData> values,
        Action<BatchResult> callback,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Request request = new(
            ConvertEntityPart("principal", env.Principal),
            ConvertEntityPart("action", env.Action),
            ConvertEntityPart("resource", env.Resource),
            ConvertContextPart(env.Context));

        (Decision decision, Diagnostic diagnostic) = EvaluatePolicies(policies, env);
        BatchResult result = new(
            request,
            new Dictionary<string, ICedarData>(values, StringComparer.Ordinal),
            decision,
            diagnostic);

        callback(result);
    }

    private static (Decision Decision, Diagnostic Diagnostic) EvaluatePolicies(IReadOnlyDictionary<PolicyId, Policy> policies, EvalEnv env)
    {
        ImmutableArray<DiagnosticReason>.Builder permitReasons = ImmutableArray.CreateBuilder<DiagnosticReason>();
        ImmutableArray<DiagnosticReason>.Builder forbidReasons = ImmutableArray.CreateBuilder<DiagnosticReason>();
        ImmutableArray<DiagnosticError>.Builder errors = ImmutableArray.CreateBuilder<DiagnosticError>();

        foreach ((PolicyId policyId, Policy policy) in policies)
        {
            BoolEvaluator evaluator = Compiler.Compile(policy.Ast);

            try
            {
                if (!evaluator.Eval(env))
                {
                    continue;
                }
            }
            catch (EvalException exception)
            {
                errors.Add(new DiagnosticError(policyId, policy.Position, exception.Message));
                continue;
            }

            DiagnosticReason reason = new(policyId, policy.Position);
            if (policy.Effect == Effect.Forbid)
            {
                forbidReasons.Add(reason);
            }
            else
            {
                permitReasons.Add(reason);
            }
        }

        if (forbidReasons.Count > 0)
        {
            return (Decision.Deny, new Diagnostic(forbidReasons.ToImmutable(), errors.ToImmutable()));
        }

        if (permitReasons.Count > 0)
        {
            return (Decision.Allow, new Diagnostic(permitReasons.ToImmutable(), errors.ToImmutable()));
        }

        return (Decision.Deny, new Diagnostic(ImmutableArray<DiagnosticReason>.Empty, errors.ToImmutable()));
    }

    private static IReadOnlyDictionary<PolicyId, Policy> PartialPolicies(IReadOnlyDictionary<PolicyId, Policy> policies, EvalEnv env)
    {
        Dictionary<PolicyId, Policy> partialPolicies = new(policies.Count);
        foreach ((PolicyId policyId, Policy policy) in policies)
        {
            PolicyAst? partialPolicy = PartialEvaluator.PartialPolicy(env, policy.Ast, out bool keep);
            if (!keep || partialPolicy is null)
            {
                continue;
            }

            partialPolicies.Add(policyId, new Policy(partialPolicy));
        }

        return partialPolicies;
    }

    private static EvalEnv Substitute(EvalEnv env, string key, ICedarData value)
    {
        return new EvalEnv(
            env.Entities,
            CloneSubstitution(env.Principal, key, value),
            CloneSubstitution(env.Action, key, value),
            CloneSubstitution(env.Resource, key, value),
            CloneSubstitution(env.Context, key, value));
    }

    private static EvalEnv FixIgnores(EvalEnv env)
    {
        return new EvalEnv(
            env.Entities,
            PartialEvaluator.IsIgnore(env.Principal) ? UnknownEntity("principal") : env.Principal,
            PartialEvaluator.IsIgnore(env.Action) ? UnknownEntity("action") : env.Action,
            PartialEvaluator.IsIgnore(env.Resource) ? UnknownEntity("resource") : env.Resource,
            PartialEvaluator.IsIgnore(env.Context) ? new CedarRecord() : env.Context);
    }

    private static EntityUid UnknownEntity(string name)
    {
        return new EntityUid(UnknownEntityType, new CedarString(name));
    }

    private static EntityUid ConvertEntityPart(string partName, ICedarData value)
    {
        try
        {
            return TypeConversion.ValueToEntity(value);
        }
        catch (EvalException exception)
        {
            throw new InvalidOperationException($"invalid {partName}: {exception.Message}", exception);
        }
    }

    private static CedarRecord ConvertContextPart(ICedarData value)
    {
        try
        {
            return TypeConversion.ValueToRecord(value);
        }
        catch (EvalException exception)
        {
            throw new InvalidOperationException($"invalid context: {exception.Message}", exception);
        }
    }

    private static void ValidateRequest(BatchRequest request)
    {
        if (request.Principal is null)
        {
            throw new ArgumentException("missing part: principal", nameof(request));
        }

        if (request.Action is null)
        {
            throw new ArgumentException("missing part: action", nameof(request));
        }

        if (request.Resource is null)
        {
            throw new ArgumentException("missing part: resource", nameof(request));
        }

        if (request.Context is null)
        {
            throw new ArgumentException("missing part: context", nameof(request));
        }

        HashSet<string> found = new(StringComparer.Ordinal);
        FindVariables(found, request.Principal);
        FindVariables(found, request.Action);
        FindVariables(found, request.Resource);
        FindVariables(found, request.Context);

        foreach (string variableName in found)
        {
            if (!request.Variables.ContainsKey(variableName))
            {
                throw new ArgumentException($"unbound variable: {variableName}", nameof(request));
            }
        }

        foreach ((string variableName, IReadOnlyList<ICedarData> values) in request.Variables)
        {
            if (!found.Contains(variableName))
            {
                throw new ArgumentException($"unused variable: {variableName}", nameof(request));
            }
        }

    }

    private static IReadOnlyDictionary<PolicyId, Policy> EnumeratePolicies(IPolicyIterator policies)
    {
        Dictionary<PolicyId, Policy> result = new();
        if (policies is PolicySet policySet)
        {
            foreach ((PolicyId policyId, Policy policy) in policySet.All())
            {
                result.Add(policyId, policy);
            }

            return result;
        }

        int index = 0;
        foreach (Policy policy in policies.Policies)
        {
            result.Add(new PolicyId($"policy{index}"), policy);
            index++;
        }

        return result;
    }

    private static ICedarData CloneSubstitution(ICedarData value, string key, ICedarData replacement)
    {
        if (PartialEvaluator.TryGetVariableName(value, out CedarString variableName) && variableName.Value == key)
        {
            return replacement;
        }

        if (value is CedarRecord record)
        {
            Dictionary<CedarString, ICedarData>? updated = null;
            foreach ((CedarString entryKey, ICedarData entryValue) in record)
            {
                ICedarData cloned = CloneSubstitution(entryValue, key, replacement);
                if (ReferenceEquals(cloned, entryValue) || cloned.Equals(entryValue))
                {
                    continue;
                }

                updated ??= record.ToDictionary(static entry => entry.Key, static entry => entry.Value);
                updated[entryKey] = cloned;
            }

            return updated is null ? record : new CedarRecord(updated);
        }

        if (value is CedarSet set)
        {
            bool changed = false;
            List<ICedarData> items = new(set.Count);
            foreach (ICedarData item in set)
            {
                ICedarData cloned = CloneSubstitution(item, key, replacement);
                if (!ReferenceEquals(cloned, item) && !cloned.Equals(item))
                {
                    changed = true;
                }

                items.Add(cloned);
            }

            return changed ? new CedarSet(items) : set;
        }

        return value;
    }

    private static void FindVariables(HashSet<string> variables, ICedarData value)
    {
        if (PartialEvaluator.TryGetVariableName(value, out CedarString variableName))
        {
            variables.Add(variableName.Value);
            return;
        }

        if (value is CedarRecord record)
        {
            foreach (ICedarData recordValue in record.Values)
            {
                FindVariables(variables, recordValue);
            }

            return;
        }

        if (value is CedarSet set)
        {
            foreach (ICedarData item in set)
            {
                FindVariables(variables, item);
            }
        }
    }

    private readonly record struct VariableItem(string Key, ICedarData[] Values);
}

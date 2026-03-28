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

    private readonly record struct AuthorizationOptions(Action<BatchResult> Callback, Effect IgnoreBias, bool IncludeDiagnostics);

    public static void Authorize(
        PolicySet policies,
        IEntityGetter? entities,
        BatchRequest request,
        params BatchOption[] options)
    {
        Authorize(policies, entities, request, (IReadOnlyList<BatchOption>)options, CancellationToken.None);
    }

    public static void Authorize(
        PolicySet policies,
        IEntityGetter? entities,
        BatchRequest request,
        IReadOnlyList<BatchOption> options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        AuthorizationOptions authorizationOptions = ParseOptionCallback(options);
        AuthorizeCore(policies, entities, request, authorizationOptions, cancellationToken);
    }

    /// <exception cref="Exception">Any exception thrown by <paramref name="callback"/> propagates directly to the caller.</exception>
    public static void Authorize(
        PolicySet policies,
        IEntityGetter? entities,
        BatchRequest request,
        Action<BatchResult> callback,
        CancellationToken cancellationToken = default)
    {
        Authorize(policies, entities, request, callback, options: null, cancellationToken);
    }

    /// <exception cref="Exception">Any exception thrown by <paramref name="callback"/> propagates directly to the caller.</exception>
    public static void Authorize(
        PolicySet policies,
        IEntityGetter? entities,
        BatchRequest request,
        Action<BatchResult> callback,
        IReadOnlyList<BatchOption>? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(callback);

        AuthorizationOptions authorizationOptions = ParseExplicitCallbackOptions(callback, options);
        AuthorizeCore(policies, entities, request, authorizationOptions, cancellationToken);
    }

    private static void AuthorizeCore(
        PolicySet policies,
        IEntityGetter? entities,
        BatchRequest request,
        AuthorizationOptions options,
        CancellationToken cancellationToken)
    {
        Effect ignoreBias = Effect.Permit;
        ignoreBias = options.IgnoreBias;

        ValidateRequest(request);
        if (request.Variables.Values.Any(static values => values.Count == 0))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IEntityGetter effectiveEntities = entities ?? new EntityMap();
        IReadOnlyDictionary<PolicyId, Policy> policyMap = policies.Map();
        PolicyBatch policyBatch = new(policyMap);
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
            IReadOnlyDictionary<PolicyId, Policy> partialPolicies = PartialPolicies(policyMap, env, ignoreBias);
            EmitResult(new PolicyBatch(partialPolicies), FixIgnores(env), new Dictionary<string, ICedarData>(StringComparer.Ordinal), options, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        Execute(
            policyBatch,
            env,
            variables,
            0,
            ignoreBias,
            new Dictionary<string, ICedarData>(StringComparer.Ordinal),
            options,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Execute(
        PolicyBatch policies,
        EvalEnv env,
        VariableItem[] variables,
        int index,
        Effect ignoreBias,
        Dictionary<string, ICedarData> values,
        AuthorizationOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (index == variables.Length)
        {
            EmitResult(policies, env, values, options, cancellationToken);
            return;
        }

        PolicyBatch partialPolicies = new(PartialPolicies(policies.Policies, env, ignoreBias));
        EvalEnv loopEnv = variables.Length - index == 1 ? FixIgnores(env) : env;
        VariableItem variable = variables[index];

        foreach (ICedarData candidate in variable.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            values[variable.Key] = candidate;
            EvalEnv nextEnv = Substitute(loopEnv, variable.Key, candidate);
            Execute(partialPolicies, nextEnv, variables, index + 1, ignoreBias, values, options, cancellationToken);
        }

        values.Remove(variable.Key);
    }

    private static void EmitResult(
        PolicyBatch policies,
        EvalEnv env,
        IReadOnlyDictionary<string, ICedarData> values,
        AuthorizationOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Request request = new(
            ConvertEntityPart("principal", env.Principal),
            ConvertEntityPart("action", env.Action),
            ConvertEntityPart("resource", env.Resource),
            env.Context is null ? null : ConvertContextPart(env.Context));

        (Decision decision, Diagnostic diagnostic) = EvaluatePolicies(policies, env);
        if (!options.IncludeDiagnostics)
        {
            diagnostic = Diagnostic.Empty;
        }

        BatchResult result = new(
            request,
            new Dictionary<string, ICedarData>(values, StringComparer.Ordinal),
            decision,
            diagnostic);

        options.Callback(result);
    }

    private static AuthorizationOptions ParseOptionCallback(IReadOnlyList<BatchOption> options)
    {
        Effect ignoreBias = Effect.Permit;
        Action<BatchResult>? callback = null;
        bool includeDiagnostics = true;

        foreach (BatchOption option in options)
        {
            ArgumentNullException.ThrowIfNull(option);

            if (option.IgnoreBias is Effect optionIgnoreBias)
            {
                ignoreBias = optionIgnoreBias;
            }

            if (option.Callback is null)
            {
                continue;
            }

            if (callback is not null)
            {
                throw new ArgumentException("multiple callback options are not supported", nameof(options));
            }

            callback = option.Callback;
            includeDiagnostics = option.IncludeDiagnostics;
        }

        if (callback is null)
        {
            throw new ArgumentException("a callback option is required", nameof(options));
        }

        return new AuthorizationOptions(callback, ignoreBias, includeDiagnostics);
    }

    private static AuthorizationOptions ParseExplicitCallbackOptions(Action<BatchResult> callback, IReadOnlyList<BatchOption>? options)
    {
        Effect ignoreBias = Effect.Permit;

        if (options is not null)
        {
            foreach (BatchOption option in options)
            {
                ArgumentNullException.ThrowIfNull(option);

                if (option.Callback is not null)
                {
                    throw new ArgumentException("callback options cannot be combined with an explicit callback parameter", nameof(options));
                }

                if (option.IgnoreBias is Effect optionIgnoreBias)
                {
                    ignoreBias = optionIgnoreBias;
                }
            }
        }

        return new AuthorizationOptions(callback, ignoreBias, IncludeDiagnostics: true);
    }

    private static (Decision Decision, Diagnostic Diagnostic) EvaluatePolicies(PolicyBatch policies, EvalEnv env)
    {
        CompiledPolicySet compiledPolicies = policies.EnsureCompiled();
        return EvaluateCompiledPolicies(compiledPolicies, env);
    }

    private static (Decision Decision, Diagnostic Diagnostic) EvaluateCompiledPolicies(CompiledPolicySet policies, EvalEnv env)
    {
        ImmutableArray<DiagnosticReason>.Builder permitReasons = ImmutableArray.CreateBuilder<DiagnosticReason>();
        ImmutableArray<DiagnosticReason>.Builder forbidReasons = ImmutableArray.CreateBuilder<DiagnosticReason>();
        ImmutableArray<DiagnosticError>.Builder errors = ImmutableArray.CreateBuilder<DiagnosticError>();

        foreach (CompiledPolicy policy in policies.Forbids)
        {
            try
            {
                if (!policy.Evaluator.Eval(env))
                {
                    continue;
                }
            }
            catch (EvalException exception)
            {
                errors.Add(new DiagnosticError(policy.PolicyId, policy.Position, exception.Message));
                continue;
            }

            forbidReasons.Add(new DiagnosticReason(policy.PolicyId, policy.Position));
        }

        foreach (CompiledPolicy policy in policies.Permits)
        {
            try
            {
                if (!policy.Evaluator.Eval(env))
                {
                    continue;
                }
            }
            catch (EvalException exception)
            {
                errors.Add(new DiagnosticError(policy.PolicyId, policy.Position, exception.Message));
                continue;
            }

            permitReasons.Add(new DiagnosticReason(policy.PolicyId, policy.Position));
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

    private static IReadOnlyDictionary<PolicyId, Policy> PartialPolicies(IReadOnlyDictionary<PolicyId, Policy> policies, EvalEnv env, Effect ignoreBias)
    {
        Dictionary<PolicyId, Policy> partialPolicies = new(policies.Count);
        foreach ((PolicyId policyId, Policy policy) in policies)
        {
            PolicyAst? partialPolicy = PartialEvaluator.PartialPolicy(env, policy.Ast, out bool keep, ignoreBias);
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
            env.Context is null ? null : CloneSubstitution(env.Context, key, value));
    }

    private static EvalEnv FixIgnores(EvalEnv env)
    {
        return new EvalEnv(
            env.Entities,
            PartialEvaluator.IsIgnore(env.Principal) ? UnknownEntity("principal") : env.Principal,
            PartialEvaluator.IsIgnore(env.Action) ? UnknownEntity("action") : env.Action,
            PartialEvaluator.IsIgnore(env.Resource) ? UnknownEntity("resource") : env.Resource,
            env.Context is not null && PartialEvaluator.IsIgnore(env.Context) ? null : env.Context);
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
            throw new BatchInvalidPartException(partName, exception);
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
            throw new BatchInvalidPartException("context", exception);
        }
    }

    private static void ValidateRequest(BatchRequest request)
    {
        if (request.Principal is null)
        {
            throw new BatchMissingPartException("principal");
        }

        if (request.Action is null)
        {
            throw new BatchMissingPartException("action");
        }

        if (request.Resource is null)
        {
            throw new BatchMissingPartException("resource");
        }

        if (request.Context is null)
        {
            throw new BatchMissingPartException("context");
        }

        HashSet<string> found = new(StringComparer.Ordinal);
        FindVariables(found, request.Principal);
        FindVariables(found, request.Action);
        FindVariables(found, request.Resource);
        if (request.Context is not null)
        {
            FindVariables(found, request.Context);
        }

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

    private sealed class PolicyBatch
    {
        private CompiledPolicySet? compiledPolicies;

        public PolicyBatch(IReadOnlyDictionary<PolicyId, Policy> policies)
        {
            Policies = policies;
        }

        public IReadOnlyDictionary<PolicyId, Policy> Policies { get; }

        public CompiledPolicySet EnsureCompiled()
        {
            compiledPolicies ??= CompiledPolicySet.Compile(Policies);
            return compiledPolicies;
        }
    }

    private readonly record struct CompiledPolicy(PolicyId PolicyId, Position Position, BoolEvaluator Evaluator);

    private sealed record CompiledPolicySet(ImmutableArray<CompiledPolicy> Forbids, ImmutableArray<CompiledPolicy> Permits)
    {
        public static CompiledPolicySet Compile(IReadOnlyDictionary<PolicyId, Policy> policies)
        {
            ImmutableArray<CompiledPolicy>.Builder forbids = ImmutableArray.CreateBuilder<CompiledPolicy>(policies.Count);
            ImmutableArray<CompiledPolicy>.Builder permits = ImmutableArray.CreateBuilder<CompiledPolicy>(policies.Count);

            foreach ((PolicyId policyId, Policy policy) in policies)
            {
                CompiledPolicy compiledPolicy = new(policyId, policy.Position, Compiler.Compile(policy.Ast));
                if (policy.Effect == Effect.Forbid)
                {
                    forbids.Add(compiledPolicy);
                }
                else
                {
                    permits.Add(compiledPolicy);
                }
            }

            return new CompiledPolicySet(forbids.ToImmutable(), permits.ToImmutable());
        }
    }
}

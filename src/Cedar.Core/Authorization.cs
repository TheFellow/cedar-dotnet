using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Cedar.Core.Internal.Eval;
using Cedar.Types;

namespace Cedar.Core;

public static class Authorization
{
    public static (Decision Decision, Diagnostic Diagnostic) Authorize(IPolicyIterator policies, IEntityGetter entities, Request request)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(request);

        IEntityGetter effectiveEntities = entities ?? new EntityMap();
        EvalEnv env = EvalEnv.FromRequest(effectiveEntities, request);

        ImmutableArray<DiagnosticReason>.Builder permitReasons = ImmutableArray.CreateBuilder<DiagnosticReason>();
        ImmutableArray<DiagnosticReason>.Builder forbidReasons = ImmutableArray.CreateBuilder<DiagnosticReason>();
        ImmutableArray<DiagnosticError>.Builder errors = ImmutableArray.CreateBuilder<DiagnosticError>();

        foreach ((PolicyId policyId, Policy policy) in EnumeratePolicies(policies))
        {
            BoolEvaluator evaluator = policy.CompiledEvaluator;

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

    private static IEnumerable<(PolicyId PolicyId, Policy Policy)> EnumeratePolicies(IPolicyIterator policies)
    {
        if (policies is PolicySet policySet)
        {
            foreach (KeyValuePair<PolicyId, Policy> entry in policySet.All())
            {
                yield return (entry.Key, entry.Value);
            }

            yield break;
        }

        int index = 0;
        foreach (Policy policy in policies.Policies)
        {
            yield return (new PolicyId($"policy{index}"), policy);
            index++;
        }
    }
}

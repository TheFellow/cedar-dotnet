using System;
using System.Collections.Generic;
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

        DiagnosticAccumulator<DiagnosticReason> permitReasons = new();
        DiagnosticAccumulator<DiagnosticReason> forbidReasons = new();
        DiagnosticAccumulator<DiagnosticError> errors = new();

        if (policies is PolicySet policySet)
        {
            foreach (KeyValuePair<PolicyId, Policy> entry in policySet.All())
            {
                Policy policy = entry.Value;
                try
                {
                    if (!policy.CompiledEvaluator.Eval(env))
                    {
                        continue;
                    }
                }
                catch (EvalException exception)
                {
                    errors.Add(new DiagnosticError(entry.Key, policy.Position, exception.Message));
                    continue;
                }

                DiagnosticReason reason = new(entry.Key, policy.Position);
                if (policy.Effect == Effect.Forbid)
                {
                    forbidReasons.Add(reason);
                }
                else
                {
                    permitReasons.Add(reason);
                }
            }
        }
        else
        {
            int index = 0;
            foreach (Policy policy in policies.Policies)
            {
                PolicyId policyId = new($"policy{index}");
                index++;

                try
                {
                    if (!policy.CompiledEvaluator.Eval(env))
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
        }

        if (forbidReasons.Count > 0)
        {
            return (Decision.Deny, new Diagnostic(forbidReasons.ToImmutableArray(), errors.ToImmutableArray()));
        }

        if (permitReasons.Count > 0)
        {
            return (Decision.Allow, new Diagnostic(permitReasons.ToImmutableArray(), errors.ToImmutableArray()));
        }

        return (Decision.Deny, new Diagnostic([], errors.ToImmutableArray()));
    }
}

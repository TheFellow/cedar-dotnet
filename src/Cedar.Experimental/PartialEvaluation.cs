using System;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Core.Internal.Eval;
using Cedar.Types;

namespace Cedar.Experimental;

public static class PartialEvaluation
{
    public static EntityUid Variable(string name)
    {
        return PartialEvaluator.Variable(name);
    }

    public static EntityUid Ignore()
    {
        return PartialEvaluator.Ignore();
    }

    public static Node PartialError(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        return new Node(PartialEvaluator.PartialError(message));
    }

    public static Node PartialError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return PartialError(exception.Message);
    }

    public static bool TryGetPartialError(Node node, out Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (PartialEvaluator.TryGetPartialError(node.Inner, out string message))
        {
            exception = new InvalidOperationException(message);
            return true;
        }

        exception = null;
        return false;
    }

    public static bool IsPartialError(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return PartialEvaluator.TryGetPartialError(node.Inner, out _);
    }

    public static PartialPolicyResult Evaluate(Policy policy, EvalEnv env)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(env);

        PolicyAst? partialPolicy = PartialEvaluator.PartialPolicy(env.ToInternal(), policy.Ast, out bool keep);
        if (!keep || partialPolicy is null)
        {
            return new PartialPolicyResult(null, false);
        }

        return new PartialPolicyResult(new Policy(partialPolicy), true);
    }

    public static Node ToNode(Policy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return new Node(PartialEvaluator.PolicyToNode(policy.Ast));
    }

    public static PartialNodeResult EvaluateToNode(Policy policy, EvalEnv env)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(env);

        PolicyAst? partialPolicy = PartialEvaluator.PartialPolicy(env.ToInternal(), policy.Ast, out bool keep);
        if (!keep || partialPolicy is null)
        {
            return new PartialNodeResult(new Node(new NodeValue(CedarBool.False)), false);
        }

        return new PartialNodeResult(new Node(PartialEvaluator.PolicyToNode(partialPolicy)), keep);
    }
}

public sealed record PartialPolicyResult(Policy? Policy, bool Keep);

public sealed record PartialNodeResult(Node Node, bool Keep);

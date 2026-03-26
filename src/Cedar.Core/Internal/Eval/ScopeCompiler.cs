using System.Collections.Generic;
using System.Collections.Immutable;
using Cedar.Ast.Internal;
using Cedar.Core.Internal.Consts;
using Cedar.Types;

namespace Cedar.Core.Internal.Eval;

internal static class ScopeCompiler
{
    public static INode CompilePolicy(PolicyAst policy)
    {
        List<INode> nodes = [];

        AddScope(nodes, CedarConsts.Principal, policy.PrincipalScope);
        AddScope(nodes, CedarConsts.Action, policy.ActionScope);
        AddScope(nodes, CedarConsts.Resource, policy.ResourceScope);

        foreach (INode condition in policy.Conditions)
        {
            nodes.Add(condition);
        }

        if (nodes.Count == 0)
        {
            return new NodeValue(CedarBool.True);
        }

        INode result = nodes[^1];
        for (int index = nodes.Count - 2; index >= 0; index--)
        {
            result = new NodeAnd(nodes[index], result);
        }

        return result;
    }

    public static INode Compile(CedarString variableName, IScope scope)
    {
        NodeVariable variable = new(variableName);

        return scope switch
        {
            ScopeAll => new NodeValue(CedarBool.True),
            ScopeEq equals => new NodeEquals(variable, new NodeValue(equals.Entity)),
            ScopeIn contains => new NodeIn(variable, new NodeValue(contains.Entity)),
            ScopeInSet set => new NodeIn(variable, new NodeSet(ToEntityNodes(set.Entities))),
            ScopeIs isScope => new NodeIs(variable, isScope.Type),
            ScopeIsIn isIn => new NodeIsIn(variable, isIn.Type, new NodeValue(isIn.Entity)),
            _ => throw new EvalException($"unsupported scope type `{scope.GetType().Name}`")
        };
    }

    private static void AddScope(List<INode> nodes, string variableName, IScope scope)
    {
        if (scope is ScopeAll)
        {
            return;
        }

        nodes.Add(Compile(new CedarString(variableName), scope));
    }

    private static ImmutableArray<INode> ToEntityNodes(EntityUid[] entities)
    {
        ImmutableArray<INode>.Builder builder = ImmutableArray.CreateBuilder<INode>(entities.Length);
        foreach (EntityUid entity in entities)
        {
            builder.Add(new NodeValue(entity));
        }

        return builder.ToImmutable();
    }
}

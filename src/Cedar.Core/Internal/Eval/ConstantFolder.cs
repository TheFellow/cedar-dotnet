using System;
using System.Collections.Immutable;
using Cedar.Ast.Internal;
using Cedar.Core.Internal.Extensions;
using Cedar.Types;

namespace Cedar.Core.Internal.Eval;

internal static class ConstantFolder
{
    private static readonly EntityUid ConstantEntity = new(new EntityType("__constant"), new CedarString("__constant"));
    private static readonly EvalEnv ConstantEnv = new(new EntityMap(), ConstantEntity, ConstantEntity, ConstantEntity, new CedarRecord());

    public static PolicyAst FoldPolicy(PolicyAst policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.Conditions.IsDefaultOrEmpty)
        {
            return policy;
        }

        ImmutableArray<INode>.Builder conditions = ImmutableArray.CreateBuilder<INode>(policy.Conditions.Length);
        foreach (INode condition in policy.Conditions)
        {
            conditions.Add(FoldNode(condition));
        }

        return policy with { Conditions = conditions.ToImmutable() };
    }

    private static INode FoldNode(INode node)
    {
        INode folded = node switch
        {
            NodeEquals equals => new NodeEquals(FoldNode(equals.Left), FoldNode(equals.Right)),
            NodeNotEquals notEquals => new NodeNotEquals(FoldNode(notEquals.Left), FoldNode(notEquals.Right)),
            NodeLessThan lessThan => new NodeLessThan(FoldNode(lessThan.Left), FoldNode(lessThan.Right)),
            NodeLessThanOrEqual lessThanOrEqual => new NodeLessThanOrEqual(FoldNode(lessThanOrEqual.Left), FoldNode(lessThanOrEqual.Right)),
            NodeGreaterThan greaterThan => new NodeGreaterThan(FoldNode(greaterThan.Left), FoldNode(greaterThan.Right)),
            NodeGreaterThanOrEqual greaterThanOrEqual => new NodeGreaterThanOrEqual(FoldNode(greaterThanOrEqual.Left), FoldNode(greaterThanOrEqual.Right)),
            NodeAnd andNode => new NodeAnd(FoldNode(andNode.Left), FoldNode(andNode.Right)),
            NodeOr orNode => new NodeOr(FoldNode(orNode.Left), FoldNode(orNode.Right)),
            NodeNot not => new NodeNot(FoldNode(not.Arg)),
            NodeNegate negate => new NodeNegate(FoldNode(negate.Arg)),
            NodeAdd add => new NodeAdd(FoldNode(add.Left), FoldNode(add.Right)),
            NodeSub sub => new NodeSub(FoldNode(sub.Left), FoldNode(sub.Right)),
            NodeMult mult => new NodeMult(FoldNode(mult.Left), FoldNode(mult.Right)),
            NodeIn contains => new NodeIn(FoldNode(contains.Left), FoldNode(contains.Right)),
            NodeIs isNode => new NodeIs(FoldNode(isNode.Left), isNode.EntityType),
            NodeIsIn isIn => new NodeIsIn(FoldNode(isIn.Left), isIn.EntityType, FoldNode(isIn.Entity)),
            NodeHas has => new NodeHas(FoldNode(has.Arg), has.Attribute),
            NodeHasTag hasTag => new NodeHasTag(FoldNode(hasTag.Left), FoldNode(hasTag.Right)),
            NodeLike like => new NodeLike(FoldNode(like.Arg), like.Pattern),
            NodeIfThenElse conditional => new NodeIfThenElse(FoldNode(conditional.If), FoldNode(conditional.Then), FoldNode(conditional.Else)),
            NodeAccess access => new NodeAccess(FoldNode(access.Arg), FoldNode(access.Attribute)),
            NodeGetTag getTag => new NodeGetTag(FoldNode(getTag.Left), FoldNode(getTag.Right)),
            NodeContains contains => new NodeContains(FoldNode(contains.Left), FoldNode(contains.Right)),
            NodeContainsAll containsAll => new NodeContainsAll(FoldNode(containsAll.Left), FoldNode(containsAll.Right)),
            NodeContainsAny containsAny => new NodeContainsAny(FoldNode(containsAny.Left), FoldNode(containsAny.Right)),
            NodeIsEmpty isEmpty => new NodeIsEmpty(FoldNode(isEmpty.Arg)),
            NodeExtensionCall call => new NodeExtensionCall(call.Name, FoldNodes(call.Args)),
            NodeRecord record => new NodeRecord(FoldRecordElements(record.Elements)),
            NodeSet set => new NodeSet(FoldNodes(set.Elements)),
            _ => node
        };

        return TryFold(folded);
    }

    private static ImmutableArray<INode> FoldNodes(ImmutableArray<INode> nodes)
    {
        if (nodes.IsDefaultOrEmpty)
        {
            return nodes;
        }

        ImmutableArray<INode>.Builder builder = ImmutableArray.CreateBuilder<INode>(nodes.Length);
        foreach (INode node in nodes)
        {
            builder.Add(FoldNode(node));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<NodeRecordElement> FoldRecordElements(ImmutableArray<NodeRecordElement> elements)
    {
        if (elements.IsDefaultOrEmpty)
        {
            return elements;
        }

        ImmutableArray<NodeRecordElement>.Builder builder = ImmutableArray.CreateBuilder<NodeRecordElement>(elements.Length);
        foreach (NodeRecordElement element in elements)
        {
            builder.Add(new NodeRecordElement(element.Key, FoldNode(element.Value)));
        }

        return builder.ToImmutable();
    }

    private static INode TryFold(INode node)
    {
        if (node is NodeValue)
        {
            return node;
        }

        if (!CanEvaluate(node))
        {
            return node;
        }

        try
        {
            ICedarData value = Compiler.ToEval(node).Eval(ConstantEnv);
            return new NodeValue(value);
        }
        catch (EvalException)
        {
            return node;
        }
    }

    private static bool CanEvaluate(INode node)
    {
        return node switch
        {
            NodeValue => true,
            NodeVariable => false,

            // PARC/entity-dependent nodes must not fold.
            NodeAccess => false,
            NodeIn => false,
            NodeIs => false,
            NodeIsIn => false,
            NodeHas => false,
            NodeHasTag => false,
            NodeGetTag => false,

            NodeNot not => CanEvaluate(not.Arg),
            NodeNegate negate => CanEvaluate(negate.Arg),
            NodeIsEmpty isEmpty => CanEvaluate(isEmpty.Arg),
            NodeLike like => CanEvaluate(like.Arg),

            NodeEquals equals => CanEvaluate(equals.Left) && CanEvaluate(equals.Right),
            NodeNotEquals notEquals => CanEvaluate(notEquals.Left) && CanEvaluate(notEquals.Right),
            NodeLessThan lessThan => CanEvaluate(lessThan.Left) && CanEvaluate(lessThan.Right),
            NodeLessThanOrEqual lessThanOrEqual => CanEvaluate(lessThanOrEqual.Left) && CanEvaluate(lessThanOrEqual.Right),
            NodeGreaterThan greaterThan => CanEvaluate(greaterThan.Left) && CanEvaluate(greaterThan.Right),
            NodeGreaterThanOrEqual greaterThanOrEqual => CanEvaluate(greaterThanOrEqual.Left) && CanEvaluate(greaterThanOrEqual.Right),
            NodeAnd andNode => CanEvaluate(andNode.Left) && CanEvaluate(andNode.Right),
            NodeOr orNode => CanEvaluate(orNode.Left) && CanEvaluate(orNode.Right),
            NodeAdd add => CanEvaluate(add.Left) && CanEvaluate(add.Right),
            NodeSub sub => CanEvaluate(sub.Left) && CanEvaluate(sub.Right),
            NodeMult mult => CanEvaluate(mult.Left) && CanEvaluate(mult.Right),
            NodeContains contains => CanEvaluate(contains.Left) && CanEvaluate(contains.Right),
            NodeContainsAll containsAll => CanEvaluate(containsAll.Left) && CanEvaluate(containsAll.Right),
            NodeContainsAny containsAny => CanEvaluate(containsAny.Left) && CanEvaluate(containsAny.Right),
            NodeIfThenElse conditional => CanEvaluate(conditional.If) && CanEvaluate(conditional.Then) && CanEvaluate(conditional.Else),

            NodeExtensionCall call => CanEvaluateExtension(call),

            NodeRecord record => CanEvaluateRecord(record),
            NodeSet set => CanEvaluateSet(set),

            _ => false
        };
    }

    private static bool CanEvaluateExtension(NodeExtensionCall call)
    {
        if (!ExtensionRegistry.TryGet(call.Name, out ExtensionDefinition definition) || definition.IsMethod)
        {
            return false;
        }

        foreach (INode arg in call.Args)
        {
            if (!CanEvaluate(arg))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanEvaluateRecord(NodeRecord record)
    {
        foreach (NodeRecordElement element in record.Elements)
        {
            if (!CanEvaluate(element.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanEvaluateSet(NodeSet set)
    {
        foreach (INode element in set.Elements)
        {
            if (!CanEvaluate(element))
            {
                return false;
            }
        }

        return true;
    }
}

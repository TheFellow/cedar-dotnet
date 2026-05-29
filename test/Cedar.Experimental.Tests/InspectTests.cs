using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Experimental;
using Cedar.Types;
using Xunit;

namespace Cedar.Experimental.Tests;

public sealed class InspectTests
{
    public static IEnumerable<object[]> CountCases()
    {
        INode leaf1 = Values.Long(1).Inner;
        INode leaf2 = Values.Long(2).Inner;

        yield return ["IfThenElse", Wrap(new NodeIfThenElse(leaf1, leaf1, leaf1)), 4];
        yield return ["Or", Wrap(new NodeOr(leaf1, leaf2)), 3];
        yield return ["And", Wrap(new NodeAnd(leaf1, leaf2)), 3];
        yield return ["LessThan", Wrap(new NodeLessThan(leaf1, leaf2)), 3];
        yield return ["LessThanOrEqual", Wrap(new NodeLessThanOrEqual(leaf1, leaf2)), 3];
        yield return ["GreaterThan", Wrap(new NodeGreaterThan(leaf1, leaf2)), 3];
        yield return ["GreaterThanOrEqual", Wrap(new NodeGreaterThanOrEqual(leaf1, leaf2)), 3];
        yield return ["NotEquals", Wrap(new NodeNotEquals(leaf1, leaf2)), 3];
        yield return ["Equals", Wrap(new NodeEquals(leaf1, leaf2)), 3];
        yield return ["In", Wrap(new NodeIn(leaf1, leaf2)), 3];
        yield return ["HasTag", Wrap(new NodeHasTag(leaf1, leaf2)), 3];
        yield return ["GetTag", Wrap(new NodeGetTag(leaf1, leaf2)), 3];
        yield return ["Contains", Wrap(new NodeContains(leaf1, leaf2)), 3];
        yield return ["ContainsAll", Wrap(new NodeContainsAll(leaf1, leaf2)), 3];
        yield return ["ContainsAny", Wrap(new NodeContainsAny(leaf1, leaf2)), 3];
        yield return ["Add", Wrap(new NodeAdd(leaf1, leaf2)), 3];
        yield return ["Sub", Wrap(new NodeSub(leaf1, leaf2)), 3];
        yield return ["Mult", Wrap(new NodeMult(leaf1, leaf2)), 3];
        yield return ["Has", Wrap(new NodeHas(leaf1, new CedarString("a"))), 2];
        yield return ["Access", Wrap(new NodeAccess(leaf1, leaf2)), 3];
        yield return ["Like", Wrap(new NodeLike(leaf1, new CedarPattern(Wildcard.Instance))), 2];
        yield return ["Is", Wrap(new NodeIs(leaf1, new CedarPath("T"))), 2];
        yield return ["IsIn", Wrap(new NodeIsIn(leaf1, new CedarPath("T"), leaf2)), 3];
        yield return ["Negate", Wrap(new NodeNegate(leaf1)), 2];
        yield return ["Not", Wrap(new NodeNot(leaf1)), 2];
        yield return ["IsEmpty", Wrap(new NodeIsEmpty(leaf1)), 2];
        yield return ["ExtensionCall", Wrap(new NodeExtensionCall(new CedarPath("f"), ImmutableArray.Create(leaf1, leaf2))), 3];
        yield return ["Record", Wrap(new NodeRecord(ImmutableArray.Create(new NodeRecordElement(new CedarString("k"), leaf1)))), 2];
        yield return ["Set", Wrap(new NodeSet(ImmutableArray.Create(leaf1, leaf2))), 3];
        yield return ["Variable", Wrap(new NodeVariable(new CedarString("v"))), 1];
        yield return ["Value", Wrap(new NodeValue(new CedarLong(1))), 1];
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void Inspect_CountsNodesForEachNodeKind(string _, Node node, int expected)
    {
        Assert.Equal(expected, CountNodes(node));
    }

    [Fact]
    public void Inspect_SkipsChildren_WhenCallbackReturnsFalse()
    {
        Node leaf = Values.Long(1);
        Node root = leaf.And(leaf);
        int count = 0;

        AstInspect.Inspect(root, node =>
        {
            count++;
            return node is not NodeAnd;
        });

        Assert.Equal(1, count);
    }

    private static int CountNodes(Node node)
    {
        int count = 0;

        AstInspect.Inspect(node, _ =>
        {
            count++;
            return true;
        });

        return count;
    }

    [Fact]
    public void Inspect_NullNode_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AstInspect.Inspect(null!, _ => true));
    }

    private static Node Wrap(INode node)
    {
        return new Node(node);
    }
}

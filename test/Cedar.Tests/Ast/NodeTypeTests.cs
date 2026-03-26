using System.Collections.Immutable;
using Cedar.Ast.Internal;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Ast;

public sealed class NodeTypeTests
{
    [Fact]
    public void NodeEqualsStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeEquals node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeNotEqualsStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeNotEquals node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeLessThanStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeLessThan node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeLessThanOrEqualStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeLessThanOrEqual node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeGreaterThanStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeGreaterThan node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeGreaterThanOrEqualStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeGreaterThanOrEqual node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeAndStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeAnd node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeOrStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeOr node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeNotStoresOperand()
    {
        INode arg = ValueNode(1);

        NodeNot node = new(arg);

        Assert.Same(arg, node.Arg);
    }

    [Fact]
    public void NodeNegateStoresOperand()
    {
        INode arg = ValueNode(1);

        NodeNegate node = new(arg);

        Assert.Same(arg, node.Arg);
    }

    [Fact]
    public void NodeAddStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeAdd node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeSubStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeSub node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeMultStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeMult node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeInStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeIn node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeIsStoresEntityType()
    {
        INode left = ValueNode(1);

        NodeIs node = new(left, new EntityType("User"));

        Assert.Same(left, node.Left);
        Assert.Equal("User", node.EntityType.Value);
    }

    [Fact]
    public void NodeIsInStoresEntityTypeAndEntity()
    {
        INode left = ValueNode(1);
        INode entity = ValueNode(2);

        NodeIsIn node = new(left, new EntityType("User"), entity);

        Assert.Same(left, node.Left);
        Assert.Same(entity, node.Entity);
        Assert.Equal("User", node.EntityType.Value);
    }

    [Fact]
    public void NodeHasStoresAttribute()
    {
        INode arg = ValueNode(1);

        NodeHas node = new(arg, new CedarString("owner"));

        Assert.Same(arg, node.Arg);
        Assert.Equal("owner", node.Attribute.Value);
    }

    [Fact]
    public void NodeHasTagStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeHasTag node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeLikeStoresPattern()
    {
        INode arg = ValueNode(1);
        CedarPattern pattern = CedarPattern.Parse("ab*");

        NodeLike node = new(arg, pattern);

        Assert.Same(arg, node.Arg);
        Assert.Equal(pattern, node.Pattern);
    }

    [Fact]
    public void NodeIfThenElseStoresBranches()
    {
        INode ifNode = ValueNode(1);
        INode thenNode = ValueNode(2);
        INode elseNode = ValueNode(3);

        NodeIfThenElse node = new(ifNode, thenNode, elseNode);

        Assert.Same(ifNode, node.If);
        Assert.Same(thenNode, node.Then);
        Assert.Same(elseNode, node.Else);
    }

    [Fact]
    public void NodeAccessStoresAttribute()
    {
        INode arg = ValueNode(1);

        NodeAccess node = new(arg, new CedarString("owner"));

        Assert.Same(arg, node.Arg);
        Assert.Equal("owner", node.Attribute.Value);
    }

    [Fact]
    public void NodeGetTagStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeGetTag node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeContainsStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeContains node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeContainsAllStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeContainsAll node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeContainsAnyStoresOperands()
    {
        INode left = ValueNode(1);
        INode right = ValueNode(2);

        NodeContainsAny node = new(left, right);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void NodeIsEmptyStoresOperand()
    {
        INode arg = ValueNode(1);

        NodeIsEmpty node = new(arg);

        Assert.Same(arg, node.Arg);
    }

    [Fact]
    public void NodeExtensionCallStoresNameAndArguments()
    {
        ImmutableArray<INode> args = [ValueNode(1), ValueNode(2)];

        NodeExtensionCall node = new("isInRange", args);

        Assert.Equal("isInRange", node.Name);
        Assert.Equal(args, node.Args);
    }

    [Fact]
    public void NodeValueStoresCedarData()
    {
        CedarLong value = new(42);

        NodeValue node = new(value);

        Assert.Same(value, node.Value);
    }

    [Fact]
    public void NodeVariableStoresName()
    {
        NodeVariable node = new(new CedarString("principal"));

        Assert.Equal("principal", node.Name.Value);
    }

    [Fact]
    public void NodeRecordStoresElements()
    {
        ImmutableArray<NodeRecordElement> elements =
        [
            new NodeRecordElement(new CedarString("x"), ValueNode(1)),
            new NodeRecordElement(new CedarString("y"), ValueNode(2))
        ];

        NodeRecord node = new(elements);

        Assert.Equal(2, node.Elements.Length);
        Assert.Equal("x", node.Elements[0].Key.Value);
        Assert.Equal("y", node.Elements[1].Key.Value);
    }

    [Fact]
    public void NodeSetStoresElements()
    {
        ImmutableArray<INode> elements = [ValueNode(1), ValueNode(2)];

        NodeSet node = new(elements);

        Assert.Equal(2, node.Elements.Length);
    }

    private static INode ValueNode(long value)
    {
        return new NodeValue(new CedarLong(value));
    }
}

using System;
using System.Collections.Immutable;
using Cedar.Ast.Internal;
using Cedar.Types;

namespace Cedar.Ast;

public static class Operators
{
    public static Node Equal(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeEquals(left, right));
    }

    public static Node NotEqual(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeNotEquals(left, right));
    }

    public static Node LessThan(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeLessThan(left, right));
    }

    public static Node LessThanOrEqual(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeLessThanOrEqual(left, right));
    }

    public static Node GreaterThan(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeGreaterThan(left, right));
    }

    public static Node GreaterThanOrEqual(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeGreaterThanOrEqual(left, right));
    }

    public static Node And(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeAnd(left, right));
    }

    public static Node Or(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeOr(left, right));
    }

    public static Node Not(this Node value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Node(new NodeNot(value.Inner));
    }

    public static Node Negate(this Node value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Node(new NodeNegate(value.Inner));
    }

    public static Node Add(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeAdd(left, right));
    }

    public static Node Sub(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeSub(left, right));
    }

    public static Node Mult(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeMult(left, right));
    }

    public static Node In(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeIn(left, right));
    }

    public static Node Is(this Node lhs, string entityType)
    {
        return Is(lhs, new EntityType(entityType));
    }

    public static Node Is(this Node lhs, EntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(lhs);
        return new Node(new NodeIs(lhs.Inner, entityType));
    }

    public static Node IsIn(this Node lhs, string entityType, Node rhs)
    {
        return IsIn(lhs, new EntityType(entityType), rhs);
    }

    public static Node IsIn(this Node lhs, EntityType entityType, Node rhs)
    {
        ArgumentNullException.ThrowIfNull(lhs);
        ArgumentNullException.ThrowIfNull(rhs);

        return new Node(new NodeIsIn(lhs.Inner, entityType, rhs.Inner));
    }

    public static Node Has(this Node lhs, string attribute)
    {
        ArgumentNullException.ThrowIfNull(lhs);

        return new Node(new NodeHas(lhs.Inner, new CedarString(attribute)));
    }

    public static Node HasTag(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeHasTag(left, right));
    }

    public static Node Like(this Node lhs, CedarPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(lhs);
        ArgumentNullException.ThrowIfNull(pattern);

        return new Node(new NodeLike(lhs.Inner, pattern));
    }

    public static Node IfThenElse(this Node condition, Node thenNode, Node elseNode)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(thenNode);
        ArgumentNullException.ThrowIfNull(elseNode);

        return new Node(new NodeIfThenElse(condition.Inner, thenNode.Inner, elseNode.Inner));
    }

    public static Node Access(this Node lhs, string attribute)
    {
        ArgumentNullException.ThrowIfNull(lhs);

        return new Node(new NodeAccess(lhs.Inner, new CedarString(attribute)));
    }

    public static Node GetTag(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeGetTag(left, right));
    }

    public static Node Contains(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeContains(left, right));
    }

    public static Node ContainsAll(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeContainsAll(left, right));
    }

    public static Node ContainsAny(this Node lhs, Node rhs)
    {
        return Binary(lhs, rhs, static (left, right) => new NodeContainsAny(left, right));
    }

    public static Node IsEmpty(this Node value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Node(new NodeIsEmpty(value.Inner));
    }

    internal static Node ExtensionCall(string name, params Node[] args)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(args);

        ImmutableArray<INode>.Builder builder = ImmutableArray.CreateBuilder<INode>(args.Length);
        foreach (Node arg in args)
        {
            ArgumentNullException.ThrowIfNull(arg);
            builder.Add(arg.Inner);
        }

        return new Node(new NodeExtensionCall(name, builder.ToImmutable()));
    }

    private static Node Binary(Node lhs, Node rhs, Func<INode, INode, INode> create)
    {
        ArgumentNullException.ThrowIfNull(lhs);
        ArgumentNullException.ThrowIfNull(rhs);

        return new Node(create(lhs.Inner, rhs.Inner));
    }
}

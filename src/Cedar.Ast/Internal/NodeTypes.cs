using System.Collections.Immutable;
using Cedar.Types;

namespace Cedar.Ast.Internal;

internal sealed record NodeEquals(INode Left, INode Right) : INode;

internal sealed record NodeNotEquals(INode Left, INode Right) : INode;

internal sealed record NodeLessThan(INode Left, INode Right) : INode;

internal sealed record NodeLessThanOrEqual(INode Left, INode Right) : INode;

internal sealed record NodeGreaterThan(INode Left, INode Right) : INode;

internal sealed record NodeGreaterThanOrEqual(INode Left, INode Right) : INode;

internal sealed record NodeAnd(INode Left, INode Right) : INode;

internal sealed record NodeOr(INode Left, INode Right) : INode;

internal sealed record NodeNot(INode Arg) : INode;

internal sealed record NodeNegate(INode Arg) : INode;

internal sealed record NodeAdd(INode Left, INode Right) : INode;

internal sealed record NodeSub(INode Left, INode Right) : INode;

internal sealed record NodeMult(INode Left, INode Right) : INode;

internal sealed record NodeIn(INode Left, INode Right) : INode;

internal sealed record NodeIs(INode Left, EntityType EntityType) : INode;

internal sealed record NodeIsIn(INode Left, EntityType EntityType, INode Entity) : INode;

internal sealed record NodeHas(INode Arg, CedarString Attribute) : INode;

internal sealed record NodeHasTag(INode Left, INode Right) : INode;

internal sealed record NodeLike(INode Arg, CedarPattern Pattern) : INode;

internal sealed record NodeIfThenElse(INode If, INode Then, INode Else) : INode;

internal sealed record NodeAccess(INode Arg, CedarString Attribute) : INode;

internal sealed record NodeGetTag(INode Left, INode Right) : INode;

internal sealed record NodeContains(INode Left, INode Right) : INode;

internal sealed record NodeContainsAll(INode Left, INode Right) : INode;

internal sealed record NodeContainsAny(INode Left, INode Right) : INode;

internal sealed record NodeIsEmpty(INode Arg) : INode;

internal sealed record NodeExtensionCall(string Name, ImmutableArray<INode> Args) : INode;

internal sealed record NodeValue(ICedarData Value) : INode;

internal sealed record NodeVariable(CedarString Name) : INode;

internal sealed record NodeRecord(ImmutableArray<NodeRecordElement> Elements) : INode;

internal sealed record NodeSet(ImmutableArray<INode> Elements) : INode;

internal sealed record NodeRecordElement(CedarString Key, INode Value);

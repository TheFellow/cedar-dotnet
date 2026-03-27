using System.Collections.Immutable;
using Cedar.Types;

namespace Cedar.Ast.Internal;

public sealed record NodeEquals(INode Left, INode Right) : INode;

public sealed record NodeNotEquals(INode Left, INode Right) : INode;

public sealed record NodeLessThan(INode Left, INode Right) : INode;

public sealed record NodeLessThanOrEqual(INode Left, INode Right) : INode;

public sealed record NodeGreaterThan(INode Left, INode Right) : INode;

public sealed record NodeGreaterThanOrEqual(INode Left, INode Right) : INode;

public sealed record NodeAnd(INode Left, INode Right) : INode;

public sealed record NodeOr(INode Left, INode Right) : INode;

public sealed record NodeNot(INode Arg) : INode;

public sealed record NodeNegate(INode Arg) : INode;

public sealed record NodeAdd(INode Left, INode Right) : INode;

public sealed record NodeSub(INode Left, INode Right) : INode;

public sealed record NodeMult(INode Left, INode Right) : INode;

public sealed record NodeIn(INode Left, INode Right) : INode;

public sealed record NodeIs(INode Left, EntityType EntityType) : INode;

public sealed record NodeIsIn(INode Left, EntityType EntityType, INode Entity) : INode;

public sealed record NodeHas(INode Arg, CedarString Attribute) : INode;

public sealed record NodeHasTag(INode Left, INode Right) : INode;

public sealed record NodeLike(INode Arg, CedarPattern Pattern) : INode;

public sealed record NodeIfThenElse(INode If, INode Then, INode Else) : INode;

public sealed record NodeAccess(INode Arg, INode Attribute) : INode;

public sealed record NodeGetTag(INode Left, INode Right) : INode;

public sealed record NodeContains(INode Left, INode Right) : INode;

public sealed record NodeContainsAll(INode Left, INode Right) : INode;

public sealed record NodeContainsAny(INode Left, INode Right) : INode;

public sealed record NodeIsEmpty(INode Arg) : INode;

public sealed record NodeExtensionCall(string Name, ImmutableArray<INode> Args) : INode;

public sealed record NodeValue(ICedarData Value) : INode;

public sealed record NodeVariable(CedarString Name) : INode;

public sealed record NodeRecord(ImmutableArray<NodeRecordElement> Elements) : INode;

public sealed record NodeSet(ImmutableArray<INode> Elements) : INode;

public sealed record NodeRecordElement(CedarString Key, INode Value);

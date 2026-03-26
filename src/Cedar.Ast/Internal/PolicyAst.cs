using System.Collections.Immutable;
using Cedar.Core;

namespace Cedar.Ast.Internal;

public sealed record PolicyAst(
    Effect Effect,
    IScope PrincipalScope,
    IScope ActionScope,
    IScope ResourceScope,
    ImmutableArray<INode> Conditions,
    ImmutableArray<Annotation> Annotations,
    Position Position);

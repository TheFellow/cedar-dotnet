using Cedar.Types;

namespace Cedar.Ast.Internal;

internal abstract record IScope;

internal sealed record ScopeAll : IScope;

internal sealed record ScopeEq(EntityUid Entity) : IScope;

internal sealed record ScopeIn(EntityUid Entity) : IScope;

internal sealed record ScopeInSet(EntityUid[] Entities) : IScope;

internal sealed record ScopeIs(EntityType Type) : IScope;

internal sealed record ScopeIsIn(EntityType Type, EntityUid Entity) : IScope;

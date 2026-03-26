using Cedar.Types;

namespace Cedar.Ast.Internal;

public abstract record IScope;

public sealed record ScopeAll : IScope;

public sealed record ScopeEq(EntityUid Entity) : IScope;

public sealed record ScopeIn(EntityUid Entity) : IScope;

public sealed record ScopeInSet(EntityUid[] Entities) : IScope;

public sealed record ScopeIs(EntityType Type) : IScope;

public sealed record ScopeIsIn(EntityType Type, EntityUid Entity) : IScope;

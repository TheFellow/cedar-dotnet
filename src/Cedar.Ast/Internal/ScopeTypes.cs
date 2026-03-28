using Cedar.Types;

namespace Cedar.Ast.Internal;

public abstract record IScope;

public interface IPrincipalScopeNode
{
}

public interface IActionScopeNode
{
}

public interface IResourceScopeNode
{
}

public sealed record ScopeAll : IScope, IPrincipalScopeNode, IActionScopeNode, IResourceScopeNode;

public sealed record ScopeEq(EntityUid Entity) : IScope, IPrincipalScopeNode, IActionScopeNode, IResourceScopeNode;

public sealed record ScopeIn(EntityUid Entity) : IScope, IPrincipalScopeNode, IActionScopeNode, IResourceScopeNode;

public sealed record ScopeInSet(EntityUid[] Entities) : IScope, IActionScopeNode;

public sealed record ScopeIs(CedarPath Type) : IScope, IPrincipalScopeNode, IResourceScopeNode;

public sealed record ScopeIsIn(CedarPath Type, EntityUid Entity) : IScope, IPrincipalScopeNode, IResourceScopeNode;

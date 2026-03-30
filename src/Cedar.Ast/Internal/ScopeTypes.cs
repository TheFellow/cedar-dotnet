using Cedar.Types;

namespace Cedar.Ast.Internal;

internal abstract record IScope;

internal interface IPrincipalScopeNode
{
}

internal interface IActionScopeNode
{
}

internal interface IResourceScopeNode
{
}

internal sealed record ScopeAll : IScope, IPrincipalScopeNode, IActionScopeNode, IResourceScopeNode;

internal sealed record ScopeEq(EntityUid Entity) : IScope, IPrincipalScopeNode, IActionScopeNode, IResourceScopeNode;

internal sealed record ScopeIn(EntityUid Entity) : IScope, IPrincipalScopeNode, IActionScopeNode, IResourceScopeNode;

internal sealed record ScopeInSet(EntityUid[] Entities) : IScope, IActionScopeNode;

internal sealed record ScopeIs(CedarPath Type) : IScope, IPrincipalScopeNode, IResourceScopeNode;

internal sealed record ScopeIsIn(CedarPath Type, EntityUid Entity) : IScope, IPrincipalScopeNode, IResourceScopeNode;

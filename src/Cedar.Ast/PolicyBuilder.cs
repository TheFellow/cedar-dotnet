using System;
using System.Collections.Immutable;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Types;

namespace Cedar.Ast;

public sealed class PolicyBuilder
{
    internal PolicyBuilder(PolicyAst ast)
    {
        Ast = ast;
    }

    internal PolicyAst Ast { get; }

    public PolicyBuilder PrincipalIs(string entityType)
    {
        return PrincipalIs(new CedarPath(entityType));
    }

    public PolicyBuilder PrincipalIs(CedarPath entityType)
    {
        return WithPrincipalScope(new ScopeIs(entityType));
    }

    public PolicyBuilder PrincipalIn(EntityUid entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return WithPrincipalScope(new ScopeIn(entity));
    }

    public PolicyBuilder PrincipalIsIn(string entityType, EntityUid entity)
    {
        return PrincipalIsIn(new CedarPath(entityType), entity);
    }

    public PolicyBuilder PrincipalIsIn(CedarPath entityType, EntityUid entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return WithPrincipalScope(new ScopeIsIn(entityType, entity));
    }

    public PolicyBuilder ActionEq(EntityUid entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return WithActionScope(new ScopeEq(entity));
    }

    public PolicyBuilder ActionIn(EntityUid entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return WithActionScope(new ScopeIn(entity));
    }

    public PolicyBuilder ActionInSet(params ReadOnlySpan<EntityUid> entities)
    {
        return WithActionScope(new ScopeInSet([.. entities]));
    }

    public PolicyBuilder ResourceEq(EntityUid entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return WithResourceScope(new ScopeEq(entity));
    }

    public PolicyBuilder ResourceIs(string entityType)
    {
        return ResourceIs(new CedarPath(entityType));
    }

    public PolicyBuilder ResourceIs(CedarPath entityType)
    {
        return WithResourceScope(new ScopeIs(entityType));
    }

    public PolicyBuilder ResourceIn(EntityUid entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return WithResourceScope(new ScopeIn(entity));
    }

    public PolicyBuilder ResourceIsIn(string entityType, EntityUid entity)
    {
        return ResourceIsIn(new CedarPath(entityType), entity);
    }

    public PolicyBuilder ResourceIsIn(CedarPath entityType, EntityUid entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return WithResourceScope(new ScopeIsIn(entityType, entity));
    }

    public PolicyBuilder When(Node condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        return new PolicyBuilder(Ast with
        {
            Conditions = Ast.Conditions.Add(condition.Inner)
        });
    }

    public PolicyBuilder Unless(Node condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        return new PolicyBuilder(Ast with
        {
            Conditions = Ast.Conditions.Add(new NodeNot(condition.Inner))
        });
    }

    public PolicyBuilder Position(Position position)
    {
        return new PolicyBuilder(Ast with { Position = position });
    }

    internal PolicyBuilder WithAnnotations(ImmutableArray<Annotation> annotations)
    {
        return new PolicyBuilder(Ast with { Annotations = annotations });
    }

    private PolicyBuilder WithPrincipalScope(IPrincipalScopeNode scope)
    {
        return new PolicyBuilder(Ast with { PrincipalScope = (IScope)scope });
    }

    private PolicyBuilder WithActionScope(IActionScopeNode scope)
    {
        return new PolicyBuilder(Ast with { ActionScope = (IScope)scope });
    }

    private PolicyBuilder WithResourceScope(IResourceScopeNode scope)
    {
        return new PolicyBuilder(Ast with { ResourceScope = (IScope)scope });
    }
}

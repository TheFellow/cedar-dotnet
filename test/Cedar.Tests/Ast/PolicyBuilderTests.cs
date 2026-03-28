using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Types;
using Xunit;
using static Cedar.Ast.Values;
using static Cedar.Ast.Variables;

namespace Cedar.Tests.Ast;

public sealed class PolicyBuilderTests
{
    [Fact]
    public void PermitStartsWithAllScopesAndNoConditions()
    {
        PolicyBuilder policy = CedarAst.Permit();

        Assert.Equal(Effect.Permit, policy.Ast.Effect);
        Assert.IsType<ScopeAll>(policy.Ast.PrincipalScope);
        Assert.IsType<ScopeAll>(policy.Ast.ActionScope);
        Assert.IsType<ScopeAll>(policy.Ast.ResourceScope);
        Assert.Empty(policy.Ast.Conditions);
        Assert.Empty(policy.Ast.Annotations);
    }

    [Fact]
    public void ForbidSetsForbidEffect()
    {
        PolicyBuilder policy = CedarAst.Forbid();

        Assert.Equal(Effect.Forbid, policy.Ast.Effect);
    }

    [Fact]
    public void PrincipalIsSetsPrincipalScope()
    {
        PolicyBuilder policy = CedarAst.Permit().PrincipalIs("User");

        ScopeIs scope = Assert.IsType<ScopeIs>(policy.Ast.PrincipalScope);
        Assert.Equal("User", scope.Type.Value);
    }

    [Fact]
    public void PrincipalInSetsPrincipalScope()
    {
        EntityUid team = new(new EntityType("Team"), new CedarString("eng"));

        PolicyBuilder policy = CedarAst.Permit().PrincipalIn(team);

        ScopeIn scope = Assert.IsType<ScopeIn>(policy.Ast.PrincipalScope);
        Assert.Equal(team, scope.Entity);
    }

    [Fact]
    public void PrincipalIsInSetsPrincipalScope()
    {
        EntityUid org = new(new EntityType("Org"), new CedarString("acme"));

        PolicyBuilder policy = CedarAst.Permit().PrincipalIsIn("User", org);

        ScopeIsIn scope = Assert.IsType<ScopeIsIn>(policy.Ast.PrincipalScope);
        Assert.Equal("User", scope.Type.Value);
        Assert.Equal(org, scope.Entity);
    }

    [Fact]
    public void ActionEqSetsActionScope()
    {
        EntityUid action = new(new EntityType("Action"), new CedarString("view"));

        PolicyBuilder policy = CedarAst.Permit().ActionEq(action);

        ScopeEq scope = Assert.IsType<ScopeEq>(policy.Ast.ActionScope);
        Assert.Equal(action, scope.Entity);
    }

    [Fact]
    public void ActionInSetsActionScope()
    {
        EntityUid action = new(new EntityType("Action"), new CedarString("edit"));

        PolicyBuilder policy = CedarAst.Permit().ActionIn(action);

        ScopeIn scope = Assert.IsType<ScopeIn>(policy.Ast.ActionScope);
        Assert.Equal(action, scope.Entity);
    }

    [Fact]
    public void ActionInSetSetsActionScope()
    {
        EntityUid read = new(new EntityType("Action"), new CedarString("read"));
        EntityUid write = new(new EntityType("Action"), new CedarString("write"));

        PolicyBuilder policy = CedarAst.Permit().ActionInSet(read, write);

        ScopeInSet scope = Assert.IsType<ScopeInSet>(policy.Ast.ActionScope);
        Assert.Equal(2, scope.Entities.Length);
        Assert.Equal(read, scope.Entities[0]);
        Assert.Equal(write, scope.Entities[1]);
    }

    [Fact]
    public void ResourceIsSetsResourceScope()
    {
        PolicyBuilder policy = CedarAst.Permit().ResourceIs("Document");

        ScopeIs scope = Assert.IsType<ScopeIs>(policy.Ast.ResourceScope);
        Assert.Equal("Document", scope.Type.Value);
    }

    [Fact]
    public void ResourceEqSetsResourceScope()
    {
        EntityUid document = new(new EntityType("Document"), new CedarString("report"));

        PolicyBuilder policy = CedarAst.Permit().ResourceEq(document);

        ScopeEq scope = Assert.IsType<ScopeEq>(policy.Ast.ResourceScope);
        Assert.Equal(document, scope.Entity);
        Assert.IsType<ScopeAll>(policy.Ast.PrincipalScope);
    }

    [Fact]
    public void ResourceInSetsResourceScope()
    {
        EntityUid folder = new(new EntityType("Folder"), new CedarString("finance"));

        PolicyBuilder policy = CedarAst.Permit().ResourceIn(folder);

        ScopeIn scope = Assert.IsType<ScopeIn>(policy.Ast.ResourceScope);
        Assert.Equal(folder, scope.Entity);
    }

    [Fact]
    public void ResourceIsInSetsResourceScope()
    {
        EntityUid folder = new(new EntityType("Folder"), new CedarString("finance"));

        PolicyBuilder policy = CedarAst.Permit().ResourceIsIn("Document", folder);

        ScopeIsIn scope = Assert.IsType<ScopeIsIn>(policy.Ast.ResourceScope);
        Assert.Equal("Document", scope.Type.Value);
        Assert.Equal(folder, scope.Entity);
    }

    [Fact]
    public void WhenAppendsConditionAsIs()
    {
        Node condition = Long(1).Equal(Long(1));

        PolicyBuilder policy = CedarAst.Permit().When(condition);

        NodeEquals stored = Assert.IsType<NodeEquals>(Assert.Single(policy.Ast.Conditions));
        Assert.IsType<NodeValue>(stored.Left);
        Assert.IsType<NodeValue>(stored.Right);
    }

    [Fact]
    public void UnlessWrapsConditionInNodeNot()
    {
        Node condition = Long(1).Equal(Long(2));

        PolicyBuilder policy = CedarAst.Permit().Unless(condition);

        NodeNot stored = Assert.IsType<NodeNot>(Assert.Single(policy.Ast.Conditions));
        Assert.IsType<NodeEquals>(stored.Arg);
    }

    [Fact]
    public void WhenAndUnlessPreserveConditionOrder()
    {
        PolicyBuilder policy = CedarAst.Permit()
            .When(Boolean(true))
            .Unless(Boolean(false));

        Assert.IsType<NodeValue>(policy.Ast.Conditions[0]);
        Assert.IsType<NodeNot>(policy.Ast.Conditions[1]);
    }

    [Fact]
    public void AnnotationBuilderCreatesAnnotatedPolicy()
    {
        PolicyBuilder policy = CedarAst.Annotation("env", "prod").Permit();

        Annotation annotation = Assert.Single(policy.Ast.Annotations);
        Assert.Equal("env", annotation.Key.Value);
        Assert.Equal("prod", annotation.Value.Value);
    }

    [Fact]
    public void AnnotationBuilderReplacesDuplicateKeys()
    {
        PolicyBuilder policy = CedarAst.Annotation("env", "prod")
            .Annotation("env", "stage")
            .Forbid();

        Annotation annotation = Assert.Single(policy.Ast.Annotations);
        Assert.Equal("env", annotation.Key.Value);
        Assert.Equal("stage", annotation.Value.Value);
        Assert.Equal(Effect.Forbid, policy.Ast.Effect);
    }

    [Fact]
    public void FluentExampleBuildsExpectedShape()
    {
        PolicyBuilder policy = CedarAst.Permit()
            .PrincipalIs("User")
            .When(Resource().Access("owner").Equal(Principal()));

        Assert.IsType<ScopeIs>(policy.Ast.PrincipalScope);
        Assert.Single(policy.Ast.Conditions);
        NodeEquals equals = Assert.IsType<NodeEquals>(policy.Ast.Conditions[0]);
        Assert.IsType<NodeAccess>(equals.Left);
        Assert.IsType<NodeVariable>(equals.Right);
    }

    [Fact]
    public void PositionOverridesDefaultPosition()
    {
        Position position = new("policy.cedar", 8, 2, 3);

        PolicyBuilder policy = CedarAst.Permit().Position(position);

        Assert.Equal(position, policy.Ast.Position);
    }

    [Fact]
    public void FromAstConstructsPolicy()
    {
        PolicyBuilder builder = CedarAst.Permit()
            .ActionEq(new EntityUid(new EntityType("Action"), new CedarString("editPhoto")))
            .When(Resource().Access("owner").Equal(Principal()));

        Policy policy = Policy.FromAst(builder);

        Assert.NotNull(policy);
        Assert.Equal(Effect.Permit, policy.Effect);
        Assert.Equal(builder.Ast, policy.Ast);
    }
}

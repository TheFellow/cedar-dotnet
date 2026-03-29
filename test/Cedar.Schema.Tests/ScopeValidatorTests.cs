using System.Collections.Generic;
using Cedar.Ast.Internal;
using Cedar.Schema.Internal.Validate;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class ScopeValidatorTests
{
    private static readonly ResolvedSchema Schema = SchemaDocument.UnmarshalCedar(
        """
        entity Actor;
        entity User in [Actor];
        entity Admin in [User];
        entity Team;
        entity Folder;
        entity Document in [Folder];
        entity Photo in [Folder];

        action read appliesTo {
            principal: User,
            resource: Folder,
            context: {}
        };

        action view in [read] appliesTo {
            principal: [User, Admin],
            resource: [Document, Photo],
            context: {}
        };

        action edit in [view] appliesTo {
            principal: Admin,
            resource: Document,
            context: {}
        };

        action archive appliesTo {
            principal: User,
            resource: Folder,
            context: {}
        };
        """).Resolve();

    private static readonly SchemaValidator Validator = new(Schema);

    [Fact]
    public void ValidatePrincipalScope_AllReturnsNullConstraint()
    {
        (EntityType[]? entityTypes, List<ValidationIssue> errors) = ScopeValidator.ValidatePrincipalScope(new ScopeAll(), Validator);

        Assert.Null(entityTypes);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidatePrincipalScope_EqAcceptsKnownEntity()
    {
        (EntityType[]? entityTypes, List<ValidationIssue> errors) = ScopeValidator.ValidatePrincipalScope(
            new ScopeEq(new EntityUid(new EntityType("User"), new Cedar.Types.CedarString("alice"))),
            Validator);

        Assert.Equal([new EntityType("User")], Assert.IsType<EntityType[]>(entityTypes));
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidatePrincipalScope_EqRejectsUnknownEntityType()
    {
        (EntityType[]? entityTypes, List<ValidationIssue> errors) = ScopeValidator.ValidatePrincipalScope(
            new ScopeEq(new EntityUid(new EntityType("Ghost"), new Cedar.Types.CedarString("alice"))),
            Validator);

        Assert.Null(entityTypes);
        Assert.Contains(errors, static error => error.Message.Contains("unrecognized entity type `Ghost`", System.StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatePrincipalScope_InExpandsDescendantEntityTypes()
    {
        (EntityType[]? entityTypes, List<ValidationIssue> errors) = ScopeValidator.ValidatePrincipalScope(
            new ScopeIn(new EntityUid(new EntityType("Actor"), new Cedar.Types.CedarString("any"))),
            Validator);

        Assert.Equal([new EntityType("Actor"), new EntityType("User"), new EntityType("Admin")], Assert.IsType<EntityType[]>(entityTypes));
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidatePrincipalScope_IsAcceptsKnownType()
    {
        (EntityType[]? entityTypes, List<ValidationIssue> errors) = ScopeValidator.ValidatePrincipalScope(
            new ScopeIs(new CedarPath("Admin")),
            Validator);

        Assert.Equal([new EntityType("Admin")], Assert.IsType<EntityType[]>(entityTypes));
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidatePrincipalScope_IsRejectsUnknownType()
    {
        (EntityType[]? entityTypes, List<ValidationIssue> errors) = ScopeValidator.ValidatePrincipalScope(
            new ScopeIs(new CedarPath("Ghost")),
            Validator);

        Assert.Null(entityTypes);
        Assert.Contains(errors, static error => error.Message.Contains("unrecognized entity type `Ghost`", System.StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatePrincipalScope_IsInReturnsTypeWhenContained()
    {
        (EntityType[]? entityTypes, List<ValidationIssue> errors) = ScopeValidator.ValidatePrincipalScope(
            new ScopeIsIn(new CedarPath("Admin"), new EntityUid(new EntityType("Actor"), new Cedar.Types.CedarString("any"))),
            Validator);

        Assert.Equal([new EntityType("Admin")], Assert.IsType<EntityType[]>(entityTypes));
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidatePrincipalScope_IsInReturnsEmptyWhenTypeNotContained()
    {
        (EntityType[]? entityTypes, List<ValidationIssue> errors) = ScopeValidator.ValidatePrincipalScope(
            new ScopeIsIn(new CedarPath("Team"), new EntityUid(new EntityType("Actor"), new Cedar.Types.CedarString("any"))),
            Validator);

        Assert.Empty(Assert.IsType<EntityType[]>(entityTypes));
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateAndGetActionUids_AllReturnsNullConstraint()
    {
        (EntityUid[]? actionUids, List<ValidationIssue> errors) = ScopeValidator.ValidateAndGetActionUids(new ScopeAll(), Validator);

        Assert.Null(actionUids);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateAndGetActionUids_EqAcceptsKnownAction()
    {
        EntityUid view = Action("view");

        (EntityUid[]? actionUids, List<ValidationIssue> errors) = ScopeValidator.ValidateAndGetActionUids(new ScopeEq(view), Validator);

        Assert.Equal([view], Assert.IsType<EntityUid[]>(actionUids));
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateAndGetActionUids_EqRejectsUnknownAction()
    {
        EntityUid ghost = Action("ghost");

        (EntityUid[]? actionUids, List<ValidationIssue> errors) = ScopeValidator.ValidateAndGetActionUids(new ScopeEq(ghost), Validator);

        Assert.Equal([ghost], Assert.IsType<EntityUid[]>(actionUids));
        Assert.Contains(errors, static error => error.Message.Contains("unrecognized action", System.StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateAndGetActionUids_InExpandsDescendantActions()
    {
        (EntityUid[]? actionUids, List<ValidationIssue> errors) = ScopeValidator.ValidateAndGetActionUids(new ScopeIn(Action("read")), Validator);

        Assert.Equal([Action("read"), Action("view"), Action("edit")], Assert.IsType<EntityUid[]>(actionUids));
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateAndGetActionUids_InSetExpandsEachActionAndDescendants()
    {
        (EntityUid[]? actionUids, List<ValidationIssue> errors) = ScopeValidator.ValidateAndGetActionUids(
            new ScopeInSet([Action("archive"), Action("view")]),
            Validator);

        Assert.Equal([Action("archive"), Action("view"), Action("edit")], Assert.IsType<EntityUid[]>(actionUids));
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateResourceScope_InExpandsDescendantEntityTypes()
    {
        (EntityType[]? entityTypes, List<ValidationIssue> errors) = ScopeValidator.ValidateResourceScope(
            new ScopeIn(new EntityUid(new EntityType("Folder"), new Cedar.Types.CedarString("root"))),
            Validator);

        Assert.Equal([new EntityType("Folder"), new EntityType("Document"), new EntityType("Photo")], Assert.IsType<EntityType[]>(entityTypes));
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateResourceScope_IsRejectsUnknownType()
    {
        (EntityType[]? entityTypes, List<ValidationIssue> errors) = ScopeValidator.ValidateResourceScope(
            new ScopeIs(new CedarPath("Ghost")),
            Validator);

        Assert.Null(entityTypes);
        Assert.Contains(errors, static error => error.Message.Contains("unrecognized entity type `Ghost`", System.StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateActionApplication_AllowsCompatiblePrincipalResourceActionConstraints()
    {
        ValidationIssue? error = ScopeValidator.ValidateActionApplication(
            [new EntityType("Admin")],
            [new EntityType("Document")],
            [Action("edit")],
            Validator);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateActionApplication_RejectsImpossibleConstraintCombination()
    {
        ValidationIssue? error = ScopeValidator.ValidateActionApplication(
            [new EntityType("Admin")],
            [new EntityType("Photo")],
            [Action("edit")],
            Validator);

        Assert.NotNull(error);
        Assert.Contains("unable to find an applicable action", error.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void GetEntityTypesIn_ExpandsTransitiveDescendants()
    {
        EntityType[] entityTypes = ScopeValidator.GetEntityTypesIn(new EntityType("Actor"), Validator);

        Assert.Equal([new EntityType("Actor"), new EntityType("User"), new EntityType("Admin")], entityTypes);
    }

    private static EntityUid Action(string id)
    {
        return new EntityUid(new EntityType("Action"), new Cedar.Types.CedarString(id));
    }
}

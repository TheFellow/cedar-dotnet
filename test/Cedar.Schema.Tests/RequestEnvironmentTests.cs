using System.Linq;
using Cedar.Schema.Internal.Validate;
using Cedar.Types;
using Xunit;
using SchemaLongType = Cedar.Schema.Internal.Validate.CedarLong;
using SchemaStringType = Cedar.Schema.Internal.Validate.CedarString;

namespace Cedar.Schema.Tests;

public sealed class RequestEnvironmentTests
{
    private static readonly ResolvedSchema Schema = SchemaDocument.UnmarshalCedar(
        """
        entity User;
        entity Admin;
        entity Photo;
        entity Document;

        action view appliesTo {
            principal: [User, Admin],
            resource: [Photo, Document],
            context: {
                token?: String,
                level: Long,
            }
        };

        action edit appliesTo {
            principal: Admin,
            resource: Document,
            context: {
                approved: Bool,
            }
        };

        action audit;
        """).Resolve();

    [Fact]
    public void Generate_CreatesEnvironmentForEachPrincipalResourcePair()
    {
        var environments = RequestEnvironment.Generate(Schema);

        Assert.Equal(5, environments.Count);
    }

    [Fact]
    public void Generate_SkipsActionsWithoutAppliesTo()
    {
        var environments = RequestEnvironment.Generate(Schema);
        EntityUid audit = new(new EntityType("Action"), new Cedar.Types.CedarString("audit"));

        Assert.DoesNotContain(environments, environment => environment.ActionUid == audit);
    }

    [Fact]
    public void Generate_ConvertsContextTypesToCedarTypes()
    {
        RequestEnvironment viewUserPhoto = FindEnvironment("User", "view", "Photo");

        Assert.Equal(new SchemaStringType(), viewUserPhoto.ContextType.Attrs["token"].Type);
        Assert.False(viewUserPhoto.ContextType.Attrs["token"].Required);
        Assert.Equal(new SchemaLongType(), viewUserPhoto.ContextType.Attrs["level"].Type);
        Assert.True(viewUserPhoto.ContextType.Attrs["level"].Required);
    }

    [Fact]
    public void FilterForPolicy_WithoutConstraintsReturnsAllEnvironments()
    {
        var environments = RequestEnvironment.Generate(Schema);

        var filtered = RequestEnvironment.FilterForPolicy(environments, null, null, null);

        Assert.Equal(environments.Count, filtered.Count);
    }

    [Fact]
    public void FilterForPolicy_FiltersByPrincipalType()
    {
        var filtered = RequestEnvironment.FilterForPolicy(
            RequestEnvironment.Generate(Schema),
            [new EntityType("Admin")],
            null,
            null);

        Assert.Equal(3, filtered.Count);
        Assert.All(filtered, static environment => Assert.Equal(new EntityType("Admin"), environment.PrincipalType));
    }

    [Fact]
    public void FilterForPolicy_FiltersByResourceType()
    {
        var filtered = RequestEnvironment.FilterForPolicy(
            RequestEnvironment.Generate(Schema),
            null,
            [new EntityType("Document")],
            null);

        Assert.Equal(3, filtered.Count);
        Assert.All(filtered, static environment => Assert.Equal(new EntityType("Document"), environment.ResourceType));
    }

    [Fact]
    public void FilterForPolicy_FiltersByActionUid()
    {
        EntityUid edit = new(new EntityType("Action"), new Cedar.Types.CedarString("edit"));

        var filtered = RequestEnvironment.FilterForPolicy(
            RequestEnvironment.Generate(Schema),
            null,
            null,
            [edit]);

        RequestEnvironment environment = Assert.Single(filtered);
        Assert.Equal(edit, environment.ActionUid);
        Assert.Equal(new EntityType("Admin"), environment.PrincipalType);
        Assert.Equal(new EntityType("Document"), environment.ResourceType);
    }

    [Fact]
    public void FilterForPolicy_CombinesAllConstraints()
    {
        EntityUid view = new(new EntityType("Action"), new Cedar.Types.CedarString("view"));

        var filtered = RequestEnvironment.FilterForPolicy(
            RequestEnvironment.Generate(Schema),
            [new EntityType("Admin")],
            [new EntityType("Photo")],
            [view]);

        RequestEnvironment environment = Assert.Single(filtered);
        Assert.Equal(new EntityType("Admin"), environment.PrincipalType);
        Assert.Equal(new EntityType("Photo"), environment.ResourceType);
        Assert.Equal(view, environment.ActionUid);
    }

    [Fact]
    public void FilterForPolicy_ReturnsEmptyWhenConstraintsDoNotMatch()
    {
        EntityUid edit = new(new EntityType("Action"), new Cedar.Types.CedarString("edit"));

        var filtered = RequestEnvironment.FilterForPolicy(
            RequestEnvironment.Generate(Schema),
            [new EntityType("User")],
            [new EntityType("Photo")],
            [edit]);

        Assert.Empty(filtered);
    }

    private static RequestEnvironment FindEnvironment(string principalType, string actionName, string resourceType)
    {
        EntityUid actionUid = new(new EntityType("Action"), new Cedar.Types.CedarString(actionName));

        return RequestEnvironment.Generate(Schema).Single(environment =>
            environment.PrincipalType == new EntityType(principalType)
            && environment.ActionUid == actionUid
            && environment.ResourceType == new EntityType(resourceType));
    }
}

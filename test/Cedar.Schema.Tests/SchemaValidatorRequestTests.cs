using System.Collections.Generic;
using Cedar.Core;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class SchemaValidatorRequestTests
{
    [Fact]
    public void ValidateRequest_AcceptsMatchingRequest()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User;
            entity Photo;

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: {
                    ok: Bool,
                }
            };
            """);

        Request request = new(
            new EntityUid(new EntityType("User"), new CedarString("alice")),
            new EntityUid(new EntityType("Action"), new CedarString("view")),
            new EntityUid(new EntityType("Photo"), new CedarString("p1")),
            new CedarRecord(new Dictionary<CedarString, ICedarData> { [new CedarString("ok")] = CedarBool.True }));

        ValidationResult result = validator.ValidateRequest(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRequest_SkipsApplicabilityChecksWhenActionHasNoAppliesTo()
    {
        SchemaValidator validator = CreateValidator("action view;");

        Request request = new(
            new EntityUid(new EntityType("UnknownPrincipal"), new CedarString("alice")),
            new EntityUid(new EntityType("Action"), new CedarString("view")),
            new EntityUid(new EntityType("UnknownResource"), new CedarString("p1")),
            null);

        ValidationResult result = validator.ValidateRequest(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRequest_RejectsUnknownAction()
    {
        SchemaValidator validator = CreateValidator("action view;");

        Request request = new(
            new EntityUid(new EntityType("User"), new CedarString("alice")),
            new EntityUid(new EntityType("Action"), new CedarString("missing")),
            new EntityUid(new EntityType("Photo"), new CedarString("p1")),
            null);

        ValidationResult result = validator.ValidateRequest(request);

        Assert.False(result.IsValid);
        Assert.Contains("does not exist", result.Errors[0]);
    }

    [Fact]
    public void ValidateRequest_RejectsUnknownPrincipalType()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User;
            entity Photo;

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: {}
            };
            """);

        Request request = CreateRequest("UnknownPrincipal", "view", "Photo", "p1");

        ValidationResult result = validator.ValidateRequest(request);

        Assert.False(result.IsValid);
        Assert.Equal("principal type `UnknownPrincipal` is not declared in the schema", result.Errors[0]);
    }

    [Fact]
    public void ValidateRequest_RejectsInvalidPrincipalTypeForAction()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User;
            entity Admin;
            entity Photo;

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: {}
            };
            """);

        Request request = CreateRequest("Admin", "view", "Photo", "p1");

        ValidationResult result = validator.ValidateRequest(request);

        Assert.False(result.IsValid);
        Assert.Equal("principal type `Admin` is not valid for `Action::\"view\"`", result.Errors[0]);
    }

    [Fact]
    public void ValidateRequest_RejectsUnknownResourceType()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User;
            entity Photo;

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: {}
            };
            """);

        Request request = CreateRequest("User", "view", "UnknownResource", "p1");

        ValidationResult result = validator.ValidateRequest(request);

        Assert.False(result.IsValid);
        Assert.Equal("resource type `UnknownResource` is not declared in the schema", result.Errors[0]);
    }

    [Fact]
    public void ValidateRequest_RejectsInvalidResourceTypeForAction()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User;
            entity Photo;
            entity Document;

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: {}
            };
            """);

        Request request = CreateRequest("User", "view", "Document", "d1");

        ValidationResult result = validator.ValidateRequest(request);

        Assert.False(result.IsValid);
        Assert.Equal("resource type `Document` is not valid for `Action::\"view\"`", result.Errors[0]);
    }

    [Fact]
    public void ValidateRequest_RejectsContextWithUnexpectedAttribute()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User;
            entity Photo;

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: {
                    ok: Bool,
                }
            };
            """);

        Request request = CreateRequest(
            "User",
            "view",
            "Photo",
            "p1",
            Record(("ok", CedarBool.True), ("extra", new CedarLong(1))));

        ValidationResult result = validator.ValidateRequest(request);

        Assert.False(result.IsValid);
        Assert.Contains("context `", result.Errors[0]);
        Assert.Contains("is not valid for `Action::\"view\"`", result.Errors[0]);
    }

    [Fact]
    public void ValidateRequest_RejectsContextWithWrongAttributeType()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User;
            entity Photo;

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: {
                    ok: Bool,
                }
            };
            """);

        Request request = CreateRequest(
            "User",
            "view",
            "Photo",
            "p1",
            Record(("ok", new CedarLong(1))));

        ValidationResult result = validator.ValidateRequest(request);

        Assert.False(result.IsValid);
        Assert.Contains("context `", result.Errors[0]);
        Assert.Contains("is not valid for `Action::\"view\"`", result.Errors[0]);
    }

    [Fact]
    public void ValidateRequest_AcceptsNullContextWhenSchemaHasEmptyContext()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User;
            entity Photo;

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: {}
            };
            """);

        Request request = CreateRequest("User", "view", "Photo", "p1");

        ValidationResult result = validator.ValidateRequest(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRequest_RejectsContextWithMissingRequiredAttribute()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User;
            entity Photo;

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: {
                    ok: Bool,
                }
            };
            """);

        Request request = CreateRequest("User", "view", "Photo", "p1", new CedarRecord());

        ValidationResult result = validator.ValidateRequest(request);

        Assert.False(result.IsValid);
        Assert.Contains("context `{}` is not valid for `Action::\"view\"`", result.Errors[0]);
    }

    private static SchemaValidator CreateValidator(string schemaText)
    {
        return new SchemaValidator(SchemaDocument.UnmarshalCedar(schemaText).Resolve());
    }

    private static Request CreateRequest(
        string principalType,
        string actionId,
        string resourceType,
        string resourceId,
        CedarRecord? context = null,
        string principalId = "alice")
    {
        return new Request(
            new EntityUid(new EntityType(principalType), new CedarString(principalId)),
            new EntityUid(new EntityType("Action"), new CedarString(actionId)),
            new EntityUid(new EntityType(resourceType), new CedarString(resourceId)),
            context);
    }

    private static CedarRecord Record(params (string Key, ICedarData Value)[] entries)
    {
        Dictionary<CedarString, ICedarData> values = [];
        foreach ((string key, ICedarData value) in entries)
        {
            values[new CedarString(key)] = value;
        }

        return new CedarRecord(values);
    }
}

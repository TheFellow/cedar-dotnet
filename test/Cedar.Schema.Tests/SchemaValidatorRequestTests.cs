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

    private static SchemaValidator CreateValidator(string schemaText)
    {
        return new SchemaValidator(SchemaDocument.UnmarshalCedar(schemaText).Resolve());
    }
}

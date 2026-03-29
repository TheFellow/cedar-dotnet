using System.Collections.Generic;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class SchemaValidatorEntityTests
{
    [Fact]
    public void ValidateEntity_AcceptsRegularEntity()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User {
                name: String,
            };
            """);

        Entity entity = new(
            new EntityUid(new EntityType("User"), new CedarString("alice")),
            new EntityUidSet(),
            new CedarRecord(new Dictionary<CedarString, ICedarData> { [new CedarString("name")] = new CedarString("Alice") }),
            new CedarRecord());

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateEntity_RejectsMissingRequiredAttribute()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User {
                name: String,
            };
            """);

        Entity entity = new(
            new EntityUid(new EntityType("User"), new CedarString("alice")),
            new EntityUidSet(),
            new CedarRecord(),
            new CedarRecord());

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.False(result.IsValid);
        Assert.Contains("missing required attribute", result.Errors[0]);
    }

    [Fact]
    public void ValidateEntity_ValidatesActionParentClosure()
    {
        SchemaValidator validator = CreateValidator(
            """
            action view;
            action edit in [view];
            action admin in [edit];
            """);

        Entity entity = new(
            new EntityUid(new EntityType("Action"), new CedarString("admin")),
            new EntityUidSet(
            [
                new EntityUid(new EntityType("Action"), new CedarString("edit")),
                new EntityUid(new EntityType("Action"), new CedarString("view"))
            ]),
            new CedarRecord(),
            new CedarRecord());

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.True(result.IsValid);
    }

    private static SchemaValidator CreateValidator(string schemaText)
    {
        return new SchemaValidator(SchemaDocument.UnmarshalCedar(schemaText).Resolve());
    }
}

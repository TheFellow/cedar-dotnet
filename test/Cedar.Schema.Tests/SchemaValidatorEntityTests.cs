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

    [Fact]
    public void ValidateEntity_RejectsActionWithUnexpectedParent()
    {
        SchemaValidator validator = CreateValidator(
            """
            action view;
            action edit in [view];
            """);

        Entity entity = CreateEntity(
            "Action",
            "edit",
            parents: [Uid("Action", "ghost")]);

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.False(result.IsValid);
        Assert.Contains("has unexpected parent", result.Errors[0]);
        Assert.Contains("ghost", result.Errors[0]);
    }

    [Fact]
    public void ValidateEntity_RejectsActionMissingExpectedParent()
    {
        SchemaValidator validator = CreateValidator(
            """
            action view;
            action edit in [view];
            """);

        Entity entity = CreateEntity("Action", "edit");

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.False(result.IsValid);
        Assert.Contains("missing expected parent", result.Errors[0]);
        Assert.Contains("view", result.Errors[0]);
    }

    [Fact]
    public void ValidateEntity_RejectsActionEntityWithAttributes()
    {
        SchemaValidator validator = CreateValidator("action view;");

        Entity entity = CreateEntity(
            "Action",
            "view",
            attributes: Record(("name", new CedarString("value"))));

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.False(result.IsValid);
        Assert.Contains("should not have attributes", result.Errors[0]);
    }

    [Fact]
    public void ValidateEntity_RejectsActionEntityWithTags()
    {
        SchemaValidator validator = CreateValidator("action view;");

        Entity entity = CreateEntity(
            "Action",
            "view",
            tags: Record(("env", new CedarString("prod"))));

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.False(result.IsValid);
        Assert.Contains("should not have tags", result.Errors[0]);
    }

    [Fact]
    public void ValidateEntity_RejectsEntityWithTagsWhenSchemaForbidsTags()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User {
                name: String,
            };
            """);

        Entity entity = CreateEntity(
            "User",
            "alice",
            attributes: Record(("name", new CedarString("Alice"))),
            tags: Record(("env", new CedarString("prod"))));

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.False(result.IsValid);
        Assert.Equal("[deser] entity type \"User\" does not allow tags", result.Errors[0]);
    }

    [Fact]
    public void ValidateEntity_AcceptsEntityWithValidTags()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User {
                name: String,
            } tags Long;
            """);

        Entity entity = CreateEntity(
            "User",
            "alice",
            attributes: Record(("name", new CedarString("Alice"))),
            tags: Record(("level", new CedarLong(3))));

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateEntity_RejectsEntityWithWrongTagValueType()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User {
                name: String,
            } tags Long;
            """);

        Entity entity = CreateEntity(
            "User",
            "alice",
            attributes: Record(("name", new CedarString("Alice"))),
            tags: Record(("level", new CedarString("three"))));

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.False(result.IsValid);
        Assert.Equal("expected Long, got CedarString", result.Errors[0]);
    }

    [Fact]
    public void ValidateEntity_RejectsInvalidParentType()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity Group;
            entity User in [Group] {
                name: String,
            };
            """);

        Entity entity = CreateEntity(
            "User",
            "alice",
            parents: [Uid("Photo", "p1")],
            attributes: Record(("name", new CedarString("Alice"))));

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.False(result.IsValid);
        Assert.Contains("invalid parent type \"Photo\"", result.Errors[0]);
    }

    [Fact]
    public void ValidateEntity_AcceptsEnumEntityType()
    {
        SchemaValidator validator = CreateValidator("""entity Role enum ["admin", "user"];""");

        Entity entity = CreateEntity("Role", "admin");

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateEntity_RejectsUnknownEntityType()
    {
        SchemaValidator validator = CreateValidator("entity User;");

        Entity entity = CreateEntity("Ghost", "g1");

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.False(result.IsValid);
        Assert.Equal("entity type \"Ghost\" not found in schema", result.Errors[0]);
    }

    [Fact]
    public void ValidateEntity_RejectsActionNotFoundInSchema()
    {
        SchemaValidator validator = CreateValidator("action view;");

        Entity entity = CreateEntity("Action", "missing");

        ValidationResult result = validator.ValidateEntity(entity);

        Assert.False(result.IsValid);
        Assert.Contains("not found in schema", result.Errors[0]);
    }

    [Fact]
    public void ValidateEntities_ReturnsDeserializationErrorMessage()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User {
                name: String,
            };
            """);

        EntityMap entities = new(
        [
            CreateEntity(
                "User",
                "alice",
                attributes: Record(
                    ("name", new CedarString("Alice")),
                    ("extra", new CedarLong(1))))
        ]);

        ValidationResult result = validator.ValidateEntities(entities);

        Assert.False(result.IsValid);
        Assert.Equal("error during entity deserialization", result.Errors[0]);
    }

    [Fact]
    public void ValidateEntities_ReturnsSchemaConformanceErrorMessage()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User {
                name: String,
            };
            """);

        EntityMap entities = new(
        [
            CreateEntity(
                "User",
                "alice",
                attributes: Record(("name", new CedarLong(1))))
        ]);

        ValidationResult result = validator.ValidateEntities(entities);

        Assert.False(result.IsValid);
        Assert.Equal("entity does not conform to the schema", result.Errors[0]);
    }

    private static SchemaValidator CreateValidator(string schemaText)
    {
        return new SchemaValidator(SchemaDocument.UnmarshalCedar(schemaText).Resolve());
    }

    private static Entity CreateEntity(
        string type,
        string id,
        IEnumerable<EntityUid>? parents = null,
        CedarRecord? attributes = null,
        CedarRecord? tags = null)
    {
        return new Entity(
            Uid(type, id),
            new EntityUidSet(parents ?? new List<EntityUid>()),
            attributes ?? new CedarRecord(),
            tags ?? new CedarRecord());
    }

    private static EntityUid Uid(string type, string id)
    {
        return new EntityUid(new EntityType(type), new CedarString(id));
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

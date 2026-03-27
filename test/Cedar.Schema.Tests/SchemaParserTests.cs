using System;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class SchemaParserTests
{
    public static TheoryData<string, string> DuplicateDeclarationCases =>
        new()
        {
            { "duplicate entity", "entity User; entity User;" },
            { "duplicate entity in namespace", "namespace Foo { entity User; entity User; }" },
            { "duplicate enum", "entity Status enum [\"a\"]; entity Status enum [\"b\"];" },
            { "duplicate action", "action view; action view;" },
            { "duplicate action in namespace", "namespace Foo { action view; action view; }" },
            { "duplicate common type", "type Ctx = { x: Long }; type Ctx = { y: Long };" },
            { "duplicate namespace", "namespace Foo { entity A; }\nnamespace Foo { entity B; }" },
            { "entity conflicts with enum", "entity User; entity User enum [\"a\"];" },
            { "enum conflicts with entity", "entity User enum [\"a\"]; entity User;" },
            { "duplicate multi-name entity", "entity A, A { };" },
            { "duplicate multi-name action", "action read, read;" }
        };

    [Fact]
    public void UnmarshalCedar_ParsesEmptyDocument()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar(string.Empty);

        Assert.Empty(document.GlobalNamespace.Entities);
        Assert.Empty(document.Namespaces);
    }

    [Fact]
    public void UnmarshalCedar_ParsesEntityWithParentsShapeAndTags()
    {
        const string cedar =
            """
            entity User in [Admin, Employee] {
            	name: String
            } tags String;
            """;

        SchemaDocument document = SchemaDocument.UnmarshalCedar(cedar);
        EntityDecl entity = Assert.Single(document.GlobalNamespace.Entities).Value;

        Assert.Equal("Admin", SchemaAssert.RequireEntityType(entity.ParentTypes).Value);
        Assert.Equal("Employee", SchemaAssert.RequireEntityType(entity.ParentTypes, 1).Value);
        SchemaType tags = entity.Tags!;
        Assert.NotNull(tags);
        Assert.Equal("String", SchemaAssert.RequireTypeRef(tags).Name);
        RecordType shape = entity.Shape!;
        Assert.NotNull(shape);
        Assert.Equal("String", SchemaAssert.RequireTypeRef(shape.Attributes["name"].Type).Name);
    }

    [Fact]
    public void UnmarshalCedar_ParsesEnumEntity()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar("""entity Role enum ["admin", "user"];""");

        EnumDecl enumDecl = Assert.Single(document.GlobalNamespace.Enums).Value;
        Assert.Equal(["admin", "user"], enumDecl.Values);
    }

    [Fact]
    public void UnmarshalCedar_ParsesActionWithParentsAndAppliesTo()
    {
        const string cedar =
            """
            action edit in [view, Admin::"manage"] appliesTo {
            	principal: User,
            	resource: [Document, Photo],
            	context: {
            		ip: ipaddr
            	}
            };
            """;

        SchemaDocument document = SchemaDocument.UnmarshalCedar(cedar);
        ActionDecl action = Assert.Single(document.GlobalNamespace.Actions).Value;

        Assert.Equal("view", action.Parents[0].Id);
        Assert.Null(action.Parents[0].Type);
        EntityType? parentType = action.Parents[1].Type;
        Assert.True(parentType.HasValue);
        Assert.Equal("Admin", parentType.Value.Value);
        Assert.Equal("manage", action.Parents[1].Id);
        Assert.Equal("User", Assert.Single(action.AppliesTo!.Principals).Value);
        Assert.Equal("Document", action.AppliesTo.Resources[0].Value);
        Assert.Equal("Photo", action.AppliesTo.Resources[1].Value);
        SchemaType contextType = action.AppliesTo.Context!;
        Assert.NotNull(contextType);
        RecordType context = SchemaAssert.RequireRecordType(contextType);
        Assert.Equal("ipaddr", SchemaAssert.RequireTypeRef(context.Attributes["ip"].Type).Name);
    }

    [Fact]
    public void UnmarshalCedar_ParsesCommonTypeWithNestedAttributesAndAnnotations()
    {
        const string cedar =
            """
            @doc("Address")
            type Address = {
            	@also("town")
            	city: String,
            	zipcode?: String,
            	meta: {
            		created: datetime
            	}
            };
            """;

        SchemaDocument document = SchemaDocument.UnmarshalCedar(cedar);
        CommonTypeDecl commonType = Assert.Single(document.GlobalNamespace.CommonTypes).Value;
        RecordType record = Assert.IsType<RecordType>(commonType.Type);

        Assert.Equal("doc", commonType.Annotations[0].Key.Value);
        Assert.Equal("town", record.Attributes["city"].Annotations[0].Value);
        Assert.True(record.Attributes["zipcode"].Optional);
        RecordType nested = SchemaAssert.RequireRecordType(record.Attributes["meta"].Type);
        Assert.Equal("datetime", SchemaAssert.RequireTypeRef(nested.Attributes["created"].Type).Name);
    }

    [Fact]
    public void UnmarshalCedar_ParsesNamespacedSchema()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar(SchemaTestData.LoadFixture("basic.cedarschema"));
        NamespaceDecl photoApp = SchemaAssert.SingleNamespace(document, "PhotoApp");

        Assert.Equal(3, photoApp.Entities.Count);
        Assert.Equal(2, photoApp.Actions.Count);
        Assert.Single(photoApp.CommonTypes);
        Assert.Contains(new Ident("Photo"), photoApp.Entities.Keys);
    }

    [Fact]
    public void UnmarshalCedar_ParsesStringNamesAndReservedKeywordName()
    {
        const string cedar =
            """
            entity User {
            	"first-name": String,
            	__cedar: String
            };

            action "view-doc";
            action __cedar;
            """;

        SchemaDocument document = SchemaDocument.UnmarshalCedar(cedar);
        EntityDecl entity = Assert.Single(document.GlobalNamespace.Entities).Value;

        Assert.Contains("first-name", entity.Shape!.Attributes.Keys);
        Assert.Contains("__cedar", entity.Shape.Attributes.Keys);
        Assert.Contains("view-doc", document.GlobalNamespace.Actions.Keys);
        Assert.Contains("__cedar", document.GlobalNamespace.Actions.Keys);
    }

    [Fact]
    public void UnmarshalCedar_UsesTypeRefForBuiltinAndQualifiedTypes()
    {
        const string cedar =
            """
            entity User {
            	name: String,
            	internal: __cedar::String
            };
            """;

        SchemaDocument document = SchemaDocument.UnmarshalCedar(cedar);
        RecordType shape = Assert.Single(document.GlobalNamespace.Entities).Value.Shape!;

        Assert.Equal("String", SchemaAssert.RequireTypeRef(shape.Attributes["name"].Type).Name);
        Assert.Equal("__cedar::String", SchemaAssert.RequireTypeRef(shape.Attributes["internal"].Type).Name);
    }

    [Fact]
    public void UnmarshalCedar_AccumulatesErrorsAcrossDeclarations()
    {
        const string cedar =
            """
            entity User { name String };
            type Broken = ;
            entity Group;
            """;

        AggregateException exception = Assert.Throws<AggregateException>(() => SchemaDocument.UnmarshalCedar(cedar));

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Contains("expected ':'", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
        Assert.Contains("expected identifier", exception.InnerExceptions[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalCedar_ParsesGoBasicFixture()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar(SchemaTestData.LoadFixture("basic.cedarschema"));

        NamespaceDecl photoApp = SchemaAssert.SingleNamespace(document, "PhotoApp");
        Assert.Contains(new Ident("Context"), photoApp.CommonTypes.Keys);
        Assert.Contains("createPhoto", photoApp.Actions.Keys);
    }

    [Fact]
    public void UnmarshalCedar_ParsesRichFixture()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar(SchemaTestData.LoadFixture("rich.cedarschema"));

        Assert.Equal(2, document.GlobalNamespace.Entities.Count);
        EntityDecl user = document.GlobalNamespace.Entities[new Ident("User")];
        Assert.Equal("Manager", user.ParentTypes[0].Value);
        Assert.NotNull(user.Shape);
    }

    [Fact]
    public void UnmarshalCedar_PreservesFilenameInErrors()
    {
        AggregateException exception = Assert.Throws<AggregateException>(() => SchemaDocument.UnmarshalCedar("entity User { name String };", "broken.cedarschema"));

        Assert.Contains("broken.cedarschema:1:", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalCedar_RejectsReservedNamespaceComponent()
    {
        AggregateException exception = Assert.Throws<AggregateException>(() => SchemaDocument.UnmarshalCedar("namespace Foo::__cedar {}"));

        Assert.Contains("expected identifier after '::'", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(DuplicateDeclarationCases))]
    public void UnmarshalCedar_RejectsDuplicateDeclarations(string _, string cedar)
    {
        AggregateException exception = Assert.Throws<AggregateException>(() => SchemaDocument.UnmarshalCedar(cedar));

        Assert.Contains("declared twice", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
    }
}

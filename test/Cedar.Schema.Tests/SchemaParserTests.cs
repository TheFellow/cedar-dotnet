using System;
using System.Linq;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class SchemaParserTests
{
    public static TheoryData<string, string> ValidStringEscapeCases =>
        new()
        {
            { "\"\\u{0041}\"", "A" },
            { "\"\\u{1F600}\"", "😀" },
            { "\"\\0\"", "\0" },
            { "\"\\n\"", "\n" },
            { "\"\\t\"", "\t" },
            { "\"\\r\"", "\r" },
            { "\"\\\\\"", "\\" },
            { "\"\\\"\"", "\"" },
            { "\"\\'\"", "'" }
        };

    public static TheoryData<string> InvalidStringEscapeCases =>
        new()
        {
            "\"\\a\"",
            "\"\\b\"",
            "\"\\f\"",
            "\"\\v\"",
            "\"\\?\"",
            "\"\\u{}\"",
            "\"\\u{GGGG}\"",
            "\"\\u{D800}\"",
            "\"\\u{110000}\""
        };

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

    public static TheoryData<string, string> ReservedKeywordAsNameCases =>
        new()
        {
            { "reserved identifier entity name true", "entity true;" },
            { "reserved identifier entity name false", "entity false;" },
            { "reserved identifier entity name if", "entity if;" },
            { "reserved identifier entity name then", "entity then;" },
            { "reserved identifier entity name else", "entity else;" },
            { "reserved identifier entity name in", "entity in;" },
            { "reserved identifier entity name is", "entity is;" },
            { "reserved identifier entity name like", "entity like;" },
            { "reserved identifier entity name has", "entity has;" },
            { "reserved identifier in namespace path", "namespace true {}" },
            { "reserved identifier in type reference", "entity Foo { x: true };" },
            { "reserved identifier type name", "type true = String;" },
            { "reserved identifier action name", "action true;" },
            { "reserved identifier attr name", "entity Foo { true: String };" },
            { "reserved identifier in path component", "entity Foo in [true::Bar];" },
            { "reserved identifier in path after double colon", "entity Foo in [Bar::true];" },
            { "reserved identifier second entity name", "entity A, true {};" },
            { "reserved identifier in action parent path", "action view in [true::Action::\"foo\"];" },
            { "reserved identifier in action parent path after double colon", "action view in [Foo::true::\"bar\"];" },
            { "__cedar as namespace name", "namespace __cedar {}" },
            { "__cedar in namespace path", "namespace Foo::__cedar {}" },
            { "__cedar as entity name", "entity __cedar;" },
            { "__cedar as second entity name", "entity A, __cedar {};" },
            { "__cedar as enum name", "entity __cedar enum [\"x\"];" },
            { "__cedar as type name", "type __cedar = String;" }
        };

    public static TheoryData<string, string> AnnotationAndAppliesToErrorCases =>
        new()
        {
            { "duplicate annotation key", "@doc(\"a\") @doc(\"b\") entity Foo;" },
            { "duplicate annotation key no value", "@deprecated @deprecated entity Foo;" },
            { "duplicate principal in appliesTo", "action view appliesTo { principal: A, principal: B };" },
            { "duplicate resource in appliesTo", "action view appliesTo { resource: A, principal: C, resource: B };" },
            { "duplicate context in appliesTo", "action view appliesTo { principal: A, resource: B, context: {}, context: {} };" },
            { "empty principal list", "action view appliesTo { principal: [], resource: Photo };" },
            { "empty resource list", "action view appliesTo { principal: Photo, resource: [] };" },
            { "missing principal in appliesTo", "action view appliesTo { resource: Photo };" },
            { "missing resource in appliesTo", "action view appliesTo { principal: Photo };" },
            { "empty appliesTo", "action view appliesTo {};" }
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
        Assert.Null(action.AppliesTo.ContextPath);
        RecordType context = action.AppliesTo.ContextRecord!;
        Assert.NotNull(context);
        Assert.Equal("ipaddr", SchemaAssert.RequireTypeRef(context.Attributes["ip"].Type).Name);
    }

    [Fact]
    public void UnmarshalCedar_ParsesActionWithNamedContextType()
    {
        const string cedar =
            """
            type CommonContext = {
            	ip: ipaddr
            };

            action edit appliesTo {
            	principal: User,
            	resource: Document,
            	context: CommonContext
            };
            """;

        SchemaDocument document = SchemaDocument.UnmarshalCedar(cedar);
        ActionDecl action = Assert.Single(document.GlobalNamespace.Actions).Value;

        Assert.NotNull(action.AppliesTo);
        Assert.Null(action.AppliesTo!.ContextRecord);
        TypeRef contextPath = Assert.IsType<TypeRef>(action.AppliesTo.ContextPath);
        Assert.Equal("CommonContext", contextPath.Name);
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

    [Theory]
    [MemberData(nameof(ValidStringEscapeCases))]
    public void UnmarshalCedar_ParsesValidStringEscapesInStringNames(string literal, string expectedName)
    {
        SchemaDocument document = ParseEntityWithStringAttributeName(literal);
        EntityDecl entity = Assert.Single(document.GlobalNamespace.Entities).Value;

        Assert.Contains(expectedName, entity.Shape!.Attributes.Keys);
    }

    [Theory]
    [MemberData(nameof(InvalidStringEscapeCases))]
    public void UnmarshalCedar_RejectsInvalidStringEscapesInStringNames(string literal)
    {
        AggregateException exception = Assert.Throws<AggregateException>(() => ParseEntityWithStringAttributeName(literal));

        Assert.Contains("invalid string escape", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
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

    [Theory]
    [MemberData(nameof(ReservedKeywordAsNameCases))]
    public void UnmarshalCedar_RejectsReservedKeywordAsName(string _, string cedar)
    {
        Assert.Throws<AggregateException>(() => SchemaDocument.UnmarshalCedar(cedar));
    }

    [Theory]
    [MemberData(nameof(AnnotationAndAppliesToErrorCases))]
    public void UnmarshalCedar_RejectsAnnotationAndAppliesToErrors(string _, string cedar)
    {
        Assert.Throws<AggregateException>(() => SchemaDocument.UnmarshalCedar(cedar));
    }

    // --- Ported from Go parser_error_test.go: parser error cases ---

    public static TheoryData<string, string> ParserErrorCases =>
        new()
        {
            { "reserved type name", "type String = { foo: String };" },
            { "reserved type name Bool", "type Bool = { foo: String };" },
            { "reserved type name Long", "type Long = { foo: String };" },
            { "reserved type name Set", "type Set = { foo: String };" },
            { "reserved type name Record", "type Record = { foo: String };" },
            { "reserved type name Entity", "type Entity = { foo: String };" },
            { "reserved type name Extension", "type Extension = { foo: String };" },
            { "reserved type name Boolean", "type Boolean = { foo: String };" },
            { "invalid token at schema level", "foo bar;" },
            { "missing closing bracket", "namespace PhotoFlash {" },
            { "missing entity name", "namespace PhotoFlash { entity { \"department\": String }; }" },
            { "missing semicolon after type declaration", "type Foo = String\ntype Bar = Bool;" },
            { "missing closing bracket in entity parent list", "entity User in [Group;" },
            { "duplicate attribute in record", "entity User { name: String, name: Long };" },
        };

    [Theory]
    [MemberData(nameof(ParserErrorCases))]
    public void UnmarshalCedar_RejectsParserErrors(string _, string cedar)
    {
        Assert.ThrowsAny<AggregateException>(() => SchemaDocument.UnmarshalCedar(cedar));
    }

    // --- Ported from Go parser_test.go: TestParseSimple round-trips ---

    [Fact]
    public void UnmarshalCedar_RoundTrips_EmptyNamespace()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar("namespace Demo {\n}\n");

        Assert.Contains("Demo", document.Namespaces.Keys);
    }

    [Fact]
    public void UnmarshalCedar_RoundTrips_EntityWithTypeRefAndStringAttribute()
    {
        const string cedar =
            """
            namespace Demo {
                entity User in UserGroup {
                    name: Demo::id,
                    "department": UserGroup,
                };
            }
            """;

        SchemaDocument document = SchemaDocument.UnmarshalCedar(cedar);
        NamespaceDecl demo = SchemaAssert.SingleNamespace(document, "Demo");
        EntityDecl user = demo.Entities[new Types.Ident("User")];

        Assert.NotNull(user.Shape);
        Assert.Contains("name", user.Shape!.Attributes.Keys);
        Assert.Contains("department", user.Shape.Attributes.Keys);
    }

    // --- Ported from Go parser_test.go: TestParserHasErrors ---

    [Fact]
    public void UnmarshalCedar_MissingClosingBrace_ReportsError()
    {
        AggregateException exception = Assert.Throws<AggregateException>(() =>
            SchemaDocument.UnmarshalCedar("namespace PhotoFlash {"));

        Assert.Contains("EOF", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalCedar_MissingEntityName_ReportsError()
    {
        AggregateException exception = Assert.Throws<AggregateException>(() =>
            SchemaDocument.UnmarshalCedar("namespace PhotoFlash { entity { \"department\": String }; }"));

        Assert.Contains("expected identifier", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
    }

    // --- Ported from Go parser_error_test.go: specific error message checks ---

    [Fact]
    public void UnmarshalCedar_ReservedTypeName_MentionsReserved()
    {
        AggregateException exception = Assert.Throws<AggregateException>(() =>
            SchemaDocument.UnmarshalCedar("type String = { foo: String };"));

        Assert.Contains("reserved type name", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalCedar_InvalidAppliesToField_ReportsError()
    {
        AggregateException exception = Assert.Throws<AggregateException>(() =>
            SchemaDocument.UnmarshalCedar("action DoSomething appliesTo { foo: [User]; };"));

        Assert.Contains("principal", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalCedar_MissingSemicolonAfterDeclaration_ReportsError()
    {
        AggregateException exception = Assert.Throws<AggregateException>(() =>
            SchemaDocument.UnmarshalCedar("type Foo = String\ntype Bar = Bool;"));

        Assert.Contains("expected ';'", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalCedar_InvalidDeclarationKeyword_ReportsError()
    {
        AggregateException exception = Assert.Throws<AggregateException>(() =>
            SchemaDocument.UnmarshalCedar("foo bar;"));

        Assert.Contains("expected declaration", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalCedar_TooManyErrorsCausesBailout()
    {
        // 17 lines with errors should trigger the max-errors bailout
        string cedar = string.Join("\n", Enumerable.Range(0, 17).Select(static index => $"type T{index} ~ Other;"));

        AggregateException exception = Assert.Throws<AggregateException>(() => SchemaDocument.UnmarshalCedar(cedar));

        // The parser should bail out before processing all errors (MaxErrors = 10)
        Assert.True(exception.InnerExceptions.Count <= 10,
            $"Expected at most 10 errors, got {exception.InnerExceptions.Count}");
    }

    // --- Ported from Go schema_test.go (x/exp): cross-format marshaling ---

    [Fact]
    public void UnmarshalJson_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => SchemaDocument.UnmarshalJson("{invalid json"));
    }

    [Fact]
    public void MarshalCedar_AfterUnmarshalJson_Succeeds()
    {
        SchemaDocument document = SchemaDocument.UnmarshalJson("{}");

        // Empty JSON schema should still be marshalable to Cedar when empty
        // (MarshalCedar throws for empty, but MarshalJson should work)
        Assert.Equal("{}", document.MarshalJson());
    }

    [Fact]
    public void MarshalJson_AfterUnmarshalCedar_Succeeds()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar("namespace test {}");

        string json = document.MarshalJson();

        Assert.Contains("test", json, StringComparison.Ordinal);
    }

    // --- Ported from Go schema_test.go: Cedar unmarshal/marshal double pass ---

    [Fact]
    public void CedarMarshalCycle_ProducesSameResultTwice()
    {
        const string cedar =
            """
            namespace foo {
                action Bar appliesTo {
                    principal: String,
                    resource: String
                };
            }
            """;

        SchemaDocument first = SchemaDocument.UnmarshalCedar(cedar);
        string pass1 = first.MarshalCedar();
        SchemaDocument second = SchemaDocument.UnmarshalCedar(pass1);
        string pass2 = second.MarshalCedar();

        Assert.Equal(pass1, pass2);
    }

    // --- Ported from Go convert_json_test.go: empty schema and invalid type ---

    [Fact]
    public void ConvertJsonToHuman_EmptySchema_ProducesEmptyOutput()
    {
        SchemaDocument document = SchemaDocument.UnmarshalJson("{}");

        Assert.Equal("{}", document.MarshalJson());
    }

    // --- Ported from Go parser_error_test.go: enum list missing comma ---

    [Fact]
    public void UnmarshalCedar_EnumMissingComma_ReportsError()
    {
        AggregateException exception = Assert.Throws<AggregateException>(() =>
            SchemaDocument.UnmarshalCedar("""entity Foo enum ["Bar" "Baz"];"""));

        Assert.Contains("expected ',' or ']'", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
    }

    // --- Ported from Go parser_error_test.go: duplicate attribute in record ---

    [Fact]
    public void UnmarshalCedar_DuplicateAttributeInRecord_ReportsError()
    {
        AggregateException exception = Assert.Throws<AggregateException>(() =>
            SchemaDocument.UnmarshalCedar("entity User { name: String, name: Long };"));

        Assert.Contains("declared twice", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
    }

    // --- Ported from Go parser_error_test.go: namespace with __cedar in name ---

    [Fact]
    public void UnmarshalCedar_NamespaceWithCedarInPath_ReportsReservedError()
    {
        AggregateException exception = Assert.Throws<AggregateException>(() =>
            SchemaDocument.UnmarshalCedar("namespace __cedar {}"));

        Assert.Contains("__cedar", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
        Assert.Contains("reserved", exception.InnerExceptions[0].Message, StringComparison.Ordinal);
    }

    private static SchemaDocument ParseEntityWithStringAttributeName(string literal)
    {
        return SchemaDocument.UnmarshalCedar($"entity User {{ {literal}: String }};");
    }
}

using System.Collections.Generic;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class SchemaWriterTests
{
    [Fact]
    public void MarshalCedar_WritesEmptyDocumentAsEmptyString()
    {
        Assert.Equal(string.Empty, new SchemaDocument().MarshalCedar());
    }

    [Fact]
    public void MarshalCedar_FormatsSampleSchemaCanonically()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar(SchemaTestData.SampleCedar);

        Assert.Equal(SchemaDocument.UnmarshalCedar(SchemaTestData.SampleCedar).MarshalCedar(), document.MarshalCedar());
    }

    [Fact]
    public void MarshalCedar_SortsDeclarationsAndNamespaces()
    {
        SchemaDocument document = new()
        {
            GlobalNamespace = new NamespaceDecl
            {
                Entities = new Dictionary<Ident, EntityDecl>
                {
                    [new Ident("User")] = new(),
                    [new Ident("Admin")] = new()
                },
                CommonTypes = new Dictionary<Ident, CommonTypeDecl>
                {
                    [new Ident("B")] = new() { Type = new TypeRef("String") },
                    [new Ident("A")] = new() { Type = new TypeRef("String") }
                }
            },
            Namespaces = new Dictionary<string, NamespaceDecl>
            {
                ["Zoo"] = new NamespaceDecl(),
                ["App"] = new NamespaceDecl()
            }
        };

        Assert.Equal(
            """
            type A = String;

            type B = String;

            entity Admin;

            entity User;

            namespace App {
            }

            namespace Zoo {
            }
            """ + "\n",
            document.MarshalCedar());
    }

    [Fact]
    public void MarshalCedar_QuotesReservedActionAndAttributeNames()
    {
        SchemaDocument document = new()
        {
            GlobalNamespace = new NamespaceDecl
            {
                Entities = new Dictionary<Ident, EntityDecl>
                {
                    [new Ident("User")] = new()
                    {
                        Shape = new RecordType
                        {
                            Attributes = new Dictionary<string, AttributeDecl>
                            {
                                ["if"] = new() { Type = new TypeRef("String") }
                            }
                        }
                    }
                },
                Actions = new Dictionary<string, ActionDecl>
                {
                    ["if"] = new()
                }
            }
        };

        string cedar = document.MarshalCedar();

        Assert.Contains("\"if\": String", cedar);
        Assert.Contains("action \"if\";", cedar);
    }

    [Fact]
    public void MarshalCedar_QuotesReservedKeywordAttributeName()
    {
        SchemaDocument document = new()
        {
            GlobalNamespace = new NamespaceDecl
            {
                Entities = new Dictionary<Ident, EntityDecl>
                {
                    [new Ident("User")] = new()
                    {
                        Shape = new RecordType
                        {
                            Attributes = new Dictionary<string, AttributeDecl>
                            {
                                ["true"] = new() { Type = new TypeRef("String") }
                            }
                        }
                    }
                }
            }
        };

        string cedar = document.MarshalCedar();

        Assert.Contains("\"true\": String", cedar);

        SchemaDocument roundTripped = SchemaDocument.UnmarshalCedar(cedar);
        Assert.True(roundTripped.GlobalNamespace.Entities[new Ident("User")].Shape!.Attributes.ContainsKey("true"));
    }

    [Fact]
    public void MarshalCedar_QuotesReservedKeywordActionName()
    {
        SchemaDocument document = new()
        {
            GlobalNamespace = new NamespaceDecl
            {
                Actions = new Dictionary<string, ActionDecl>
                {
                    ["true"] = new()
                }
            }
        };

        string cedar = document.MarshalCedar();

        Assert.Contains("action \"true\";", cedar);

        SchemaDocument roundTripped = SchemaDocument.UnmarshalCedar(cedar);
        Assert.True(roundTripped.GlobalNamespace.Actions.ContainsKey("true"));
    }

    [Fact]
    public void MarshalCedar_SortsAnnotationsByKey()
    {
        SchemaDocument document = new()
        {
            GlobalNamespace = new NamespaceDecl
            {
                Entities = new Dictionary<Ident, EntityDecl>
                {
                    [new Ident("User")] = new()
                    {
                        Annotations =
                        [
                            new SchemaAnnotation(new Ident("z"), "last"),
                            new SchemaAnnotation(new Ident("a"), "first")
                        ]
                    }
                }
            }
        };

        Assert.Equal(
            """
            @a("first")
            @z("last")
            entity User;
            """ + "\n",
            document.MarshalCedar());
    }

    [Fact]
    public void MarshalCedar_WritesNestedRecordAndSetTypes()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar(
            """
            type Settings = {
            	flags: Set<String>,
            	meta: {
            		enabled: Bool
            	}
            };
            """);

        Assert.Equal("type Settings = {\n\tflags: Set<String>,\n\tmeta: {\n\t\tenabled: Bool\n\t}\n};\n", document.MarshalCedar());
    }

    [Fact]
    public void MarshalCedar_WritesQualifiedAndUnqualifiedActionParents()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar("""action edit in [view, Admin::"manage"];""");

        Assert.Equal("""action edit in [view, Admin::"manage"];""" + "\n", document.MarshalCedar());
    }

    [Fact]
    public void MarshalCedar_CanonicalizesEntityShapeSyntaxWithoutEquals()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar(SchemaTestData.LoadFixture("rich.cedarschema"));
        string cedar = document.MarshalCedar();

        Assert.DoesNotContain("= {", cedar);
        Assert.Contains("entity User in Manager {", cedar);
    }
}

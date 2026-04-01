using Cedar.Schema.Internal;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class SchemaRoundTripTests
{
    [Fact]
    public void CedarRoundTrip_BasicFixturePreservesCanonicalDocument()
    {
        string source = SchemaTestData.LoadFixture("basic.cedarschema");
        SchemaDocument first = SchemaDocument.UnmarshalCedar(source);
        string cedar = first.MarshalCedar();
        SchemaDocument second = SchemaDocument.UnmarshalCedar(cedar);

        Assert.Equal(first.MarshalCedar(), second.MarshalCedar());
    }

    [Fact]
    public void CedarRoundTrip_RichFixturePreservesCanonicalDocument()
    {
        string source = SchemaTestData.LoadFixture("rich.cedarschema");
        SchemaDocument first = SchemaDocument.UnmarshalCedar(source);
        SchemaDocument second = SchemaDocument.UnmarshalCedar(first.MarshalCedar());

        Assert.Equal(first.MarshalCedar(), second.MarshalCedar());
    }

    [Fact]
    public void CedarRoundTrip_SampleSchemaPreservesCanonicalJson()
    {
        SchemaDocument first = SchemaDocument.UnmarshalCedar(SchemaTestData.SampleCedar);
        SchemaDocument second = SchemaDocument.UnmarshalCedar(first.MarshalCedar());

        Assert.Equal(first.MarshalJson(), second.MarshalJson());
    }

    [Fact]
    public void JsonRoundTrip_SamplePayloadPreservesCanonicalJson()
    {
        SchemaDocument first = SchemaDocument.UnmarshalJson(SchemaTestData.SampleJson);
        SchemaDocument second = SchemaDocument.UnmarshalJson(first.MarshalJson());

        Assert.Equal(first.MarshalJson(), second.MarshalJson());
    }

    [Fact]
    public void JsonRoundTrip_BuiltinPayloadPreservesCanonicalCedar()
    {
        const string json =
            """
            {
              "App": {
                "entityTypes": {
                  "User": {
                    "shape": {
                      "type": "Record",
                      "attributes": {
                        "active": { "type": "Boolean" },
                        "friends": {
                          "type": "Set",
                          "element": { "type": "Entity", "name": "Group" }
                        }
                      }
                    }
                  }
                },
                "actions": {}
              }
            }
            """;

        SchemaDocument first = SchemaDocument.UnmarshalJson(json);
        SchemaDocument second = SchemaDocument.UnmarshalJson(first.MarshalJson());

        Assert.Equal(first.MarshalCedar(), second.MarshalCedar());
    }

    [Fact]
    public void CrossFormat_ConvertersPreserveSemantics()
    {
        string json = HumanToJsonConverter.ConvertCedarToJson(SchemaTestData.SampleCedar);
        string cedar = HumanToJsonConverter.ConvertJsonToCedar(json);

        Assert.Equal(SchemaDocument.UnmarshalCedar(SchemaTestData.SampleCedar).MarshalCedar(), cedar);
    }

    [Fact]
    public void CrossFormat_ConvertersPreserveNamedContextType()
    {
        const string schema =
            """
            type CommonCtx = {
                authenticated: Bool,
            };

            action edit appliesTo {
                principal: User,
                resource: Document,
                context: CommonCtx,
            };
            """;

        string json = HumanToJsonConverter.ConvertCedarToJson(schema);
        string cedar = HumanToJsonConverter.ConvertJsonToCedar(json);
        SchemaDocument roundTripped = SchemaDocument.UnmarshalCedar(cedar);

        ActionDecl action = Assert.Single(roundTripped.GlobalNamespace.Actions).Value;
        Assert.NotNull(action.AppliesTo);
        Assert.Null(action.AppliesTo!.ContextRecord);
        Assert.NotNull(action.AppliesTo.ContextPath);
        Assert.Equal("CommonCtx", action.AppliesTo.ContextPath!.Name);
    }

    // --- Ported from Go convert_json_test.go: TestConvertJsonToHumanRoundtrip ---

    [Fact]
    public void JsonToHumanToJson_PreservesSemantics()
    {
        string json = SchemaTestData.SampleJson;
        SchemaDocument fromJson = SchemaDocument.UnmarshalJson(json);

        string cedar = fromJson.MarshalCedar();
        SchemaDocument fromCedar = SchemaDocument.UnmarshalCedar(cedar);

        SchemaAssert.JsonEqual(json, fromCedar.MarshalJson());
    }

    // --- Ported from Go schema_test.go (x/exp): TestSchemaCedarMarshalUnmarshal double-pass ---

    [Fact]
    public void CedarRoundTrip_DoublePassProducesIdenticalOutput()
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

        SchemaDocument s1 = SchemaDocument.UnmarshalCedar(cedar);
        string pass1 = s1.MarshalCedar();

        SchemaDocument s2 = SchemaDocument.UnmarshalCedar(pass1);
        string pass2 = s2.MarshalCedar();

        Assert.Equal(pass1, pass2);
    }

    // --- Ported from Go schema_test.go (x/exp): TestSchemaCrossFormatMarshaling ---

    [Fact]
    public void CrossFormat_CedarToJsonIsAllowed()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar("namespace test {}");

        string json = document.MarshalJson();

        Assert.NotNull(json);
    }

    [Fact]
    public void CrossFormat_JsonToCedarIsAllowed()
    {
        SchemaDocument document = SchemaDocument.UnmarshalJson("{}");

        // An empty JSON schema should marshal to valid JSON
        string json = document.MarshalJson();
        Assert.Equal("{}", json);
    }

    // --- Ported from Go convert_json_test.go: round-trip through multiple namespaces ---

    [Fact]
    public void CrossFormat_MultipleNamespacesRoundTrip()
    {
        const string cedar =
            """
            entity User;

            namespace AppA {
                entity Document;
            }

            namespace AppB {
                entity Photo;
            }
            """;

        string json = SchemaDocument.UnmarshalCedar(cedar).MarshalJson();
        SchemaDocument fromJson = SchemaDocument.UnmarshalJson(json);
        string roundTripped = fromJson.MarshalJson();

        SchemaAssert.JsonEqual(json, roundTripped);
    }
}

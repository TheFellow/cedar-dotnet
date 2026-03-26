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
}

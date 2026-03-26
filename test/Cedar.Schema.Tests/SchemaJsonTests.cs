using System;
using System.Text.Json;
using Cedar.Schema.Internal;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class SchemaJsonTests
{
    [Fact]
    public void MarshalJson_WritesEmptySchemaAsEmptyObject()
    {
        Assert.Equal("{}", new SchemaDocument().MarshalJson());
    }

    [Fact]
    public void MarshalJson_MatchesExpectedSamplePayload()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar(SchemaTestData.SampleCedar);

        SchemaAssert.JsonEqual(SchemaTestData.SampleJson, document.MarshalJson());
    }

    [Fact]
    public void UnmarshalJson_ReadsSamplePayload()
    {
        SchemaDocument document = SchemaDocument.UnmarshalJson(SchemaTestData.SampleJson);

        Assert.Contains(new Ident("Address"), document.GlobalNamespace.CommonTypes.Keys);
        Assert.Contains(new Ident("User"), document.GlobalNamespace.Entities.Keys);
        Assert.Contains("view", document.GlobalNamespace.Actions.Keys);
        Assert.Contains("MyApp", document.Namespaces.Keys);
    }

    [Fact]
    public void UnmarshalJson_ConvertsBuiltinsAndExtensionTypesToCanonicalCedar()
    {
        const string json =
            """
            {
              "": {
                "entityTypes": {
                  "User": {
                    "shape": {
                      "type": "Record",
                      "attributes": {
                        "flag": { "type": "Boolean" },
                        "stamp": { "type": "Extension", "name": "datetime" }
                      }
                    }
                  }
                },
                "actions": {}
              }
            }
            """;

        string cedar = SchemaDocument.UnmarshalJson(json).MarshalCedar();

        Assert.Contains("flag: Bool", cedar, StringComparison.Ordinal);
        Assert.Contains("stamp: datetime", cedar, StringComparison.Ordinal);
    }

    [Fact]
    public void MarshalJson_RoundTripsEnumEntities()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar("""entity Role enum ["admin", "user"];""");

        SchemaAssert.JsonEqual(
            """
            {
              "": {
                "entityTypes": {
                  "Role": {
                    "enum": [
                      "admin",
                      "user"
                    ]
                  }
                },
                "actions": {}
              }
            }
            """,
            document.MarshalJson());
    }

    [Fact]
    public void HumanToJsonConverter_ConvertsBothDirections()
    {
        string json = HumanToJsonConverter.ConvertCedarToJson(SchemaTestData.SampleCedar);
        string cedar = HumanToJsonConverter.ConvertJsonToCedar(json);

        SchemaAssert.JsonEqual(SchemaTestData.SampleJson, json);
        Assert.Equal(SchemaDocument.UnmarshalCedar(SchemaTestData.SampleCedar).MarshalCedar(), cedar);
    }

    [Fact]
    public void UnmarshalJson_InvalidTypeThrows()
    {
        const string json =
            """
            {
              "": {
                "entityTypes": {},
                "actions": {},
                "commonTypes": {
                  "Broken": {
                    "type": "Nope"
                  }
                }
              }
            }
            """;

        JsonException exception = Assert.Throws<JsonException>(() => SchemaDocument.UnmarshalJson(json));
        Assert.Contains("Nope", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalJson_PreservesOptionalAttributesAndAnnotations()
    {
        SchemaDocument document = SchemaDocument.UnmarshalJson(SchemaTestData.SampleJson);
        RecordType address = Assert.IsType<RecordType>(document.GlobalNamespace.CommonTypes[new Ident("Address")].Type);

        Assert.True(address.Attributes["zipcode"].Optional);
        Assert.Equal("town", address.Attributes["city"].Annotations[0].Value);
    }
}

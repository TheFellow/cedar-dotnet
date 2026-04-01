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
    public void MarshalJson_SerializesEntityTagsAsTags()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar("""entity Photo tags String;""");

        string json = document.MarshalJson();

        Assert.Contains("\"tags\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("additionalAttributes", json, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalJson_ReadsTagsAsTags()
    {
        const string json =
            """
            {
              "": {
                "entityTypes": {
                  "Photo": {
                    "tags": { "type": "String" }
                  }
                },
                "actions": {}
              }
            }
            """;

        SchemaDocument document = SchemaDocument.UnmarshalJson(json);
        EntityDecl entity = Assert.Single(document.GlobalNamespace.Entities).Value;

        Assert.NotNull(entity.Tags);
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

    [Fact]
    public void MarshalJson_SerializesNamedContextTypeAsEntityOrCommon()
    {
        const string cedar =
            """
            type CommonCtx = {
                ok: Bool,
            };

            action go appliesTo {
                principal: User,
                resource: Doc,
                context: CommonCtx,
            };
            """;

        string json = SchemaDocument.UnmarshalCedar(cedar).MarshalJson();

        Assert.Contains("\"context\"", json, StringComparison.Ordinal);
        Assert.Contains("\"EntityOrCommon\"", json, StringComparison.Ordinal);
        Assert.Contains("\"CommonCtx\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalJson_DeserializesNamedContextTypeAsContextPath()
    {
        const string json =
            """
            {
              "": {
                "entityTypes": {},
                "actions": {
                  "go": {
                    "appliesTo": {
                      "principalTypes": ["User"],
                      "resourceTypes": ["Doc"],
                      "context": {
                        "type": "EntityOrCommon",
                        "name": "CommonCtx"
                      }
                    }
                  }
                }
              }
            }
            """;

        SchemaDocument document = SchemaDocument.UnmarshalJson(json);
        ActionDecl action = Assert.Single(document.GlobalNamespace.Actions).Value;

        Assert.NotNull(action.AppliesTo);
        Assert.Null(action.AppliesTo!.ContextRecord);
        Assert.NotNull(action.AppliesTo.ContextPath);
        Assert.Equal("CommonCtx", action.AppliesTo.ContextPath!.Name);
    }

    // --- Ported from Go json_test.go: TestParsesExampleSchema round-trip ---

    [Fact]
    public void UnmarshalJson_MarshalJson_RoundTripsEntityWithMultipleAttributes()
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
                        "name": { "type": "EntityOrCommon", "name": "String" },
                        "age": { "type": "EntityOrCommon", "name": "Long" },
                        "active": { "type": "EntityOrCommon", "name": "Bool" }
                      }
                    }
                  }
                },
                "actions": {}
              }
            }
            """;

        SchemaDocument first = SchemaDocument.UnmarshalJson(json);
        string serialized = first.MarshalJson();
        SchemaDocument second = SchemaDocument.UnmarshalJson(serialized);

        Assert.Equal(first.MarshalJson(), second.MarshalJson());
    }

    // --- Ported from Go schema_test.go (x/exp): TestSchemaJSONMarshalUnmarshal with empty ---

    [Fact]
    public void UnmarshalJson_EmptyObject_RoundTrips()
    {
        SchemaDocument document = SchemaDocument.UnmarshalJson("{}");

        Assert.Equal("{}", document.MarshalJson());
    }

    // --- Ported from Go convert_json_test.go: TestConvertJsonToHumanRoundtrip ---

    [Fact]
    public void UnmarshalJson_ToHumanAndBack_PreservesJsonSemantics()
    {
        string json = SchemaTestData.SampleJson;
        SchemaDocument fromJson = SchemaDocument.UnmarshalJson(json);
        string cedar = fromJson.MarshalCedar();
        SchemaDocument fromCedar = SchemaDocument.UnmarshalCedar(cedar);
        string roundTripJson = fromCedar.MarshalJson();

        SchemaAssert.JsonEqual(json, roundTripJson);
    }

    // --- Ported from Go json_test.go: entity parent serialization ---

    [Fact]
    public void MarshalJson_SerializesEntityMemberOfRelationship()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar(
            """
            entity Group;
            entity User in [Group] {
                name: String,
            };
            """);

        string json = document.MarshalJson();

        Assert.Contains("memberOfTypes", json, StringComparison.Ordinal);
        Assert.Contains("Group", json, StringComparison.Ordinal);
    }

    // --- Ported from Go json_test.go: Set type serialization ---

    [Fact]
    public void MarshalJson_SerializesSetTypeInEntityShape()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar(
            """
            entity User {
                tags: Set<String>,
            };
            """);

        string json = document.MarshalJson();

        Assert.Contains("\"Set\"", json, StringComparison.Ordinal);
        Assert.Contains("element", json, StringComparison.Ordinal);
    }

    // --- Ported from Go schema_test.go: JSON schema with action parents ---

    [Fact]
    public void UnmarshalJson_ActionMemberOfRoundTrips()
    {
        const string json =
            """
            {
              "": {
                "entityTypes": {},
                "actions": {
                  "read": {},
                  "view": {
                    "memberOf": [
                      { "id": "read" }
                    ]
                  }
                }
              }
            }
            """;

        SchemaDocument document = SchemaDocument.UnmarshalJson(json);
        string roundTrip = document.MarshalJson();

        SchemaAssert.JsonEqual(json, roundTrip);
    }
}

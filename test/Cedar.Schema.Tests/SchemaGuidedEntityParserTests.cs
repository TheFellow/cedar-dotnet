using System;
using System.IO;
using System.Linq;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class SchemaGuidedEntityParserTests
{
    [Fact]
    public void ParseEntityMap_StringAttribute_ParsesGuidedString()
    {
        Entity entity = ParseSingleEntity("\"hello\"", "{ \"type\": \"EntityOrCommon\", \"name\": \"String\" }");

        Assert.Equal(new CedarString("hello"), entity.Attributes[new CedarString("v")]);
    }

    [Fact]
    public void ParseEntityMap_LongAttribute_ParsesGuidedLong()
    {
        Entity entity = ParseSingleEntity("42", "{ \"type\": \"EntityOrCommon\", \"name\": \"Long\" }");

        Assert.Equal(new CedarLong(42), entity.Attributes[new CedarString("v")]);
    }

    [Fact]
    public void ParseEntityMap_BoolAttribute_ParsesGuidedBool()
    {
        Entity entity = ParseSingleEntity("true", "{ \"type\": \"EntityOrCommon\", \"name\": \"Boolean\" }");

        Assert.Equal(CedarBool.True, entity.Attributes[new CedarString("v")]);
    }

    [Fact]
    public void ParseEntityMap_EntityAttribute_ParsesEntityUid()
    {
        Entity entity = ParseSingleEntity("{\"type\":\"User\",\"id\":\"alice\"}", "{ \"type\": \"Entity\", \"name\": \"User\" }");

        Assert.Equal(new EntityUid(new EntityType("User"), new CedarString("alice")), entity.Attributes[new CedarString("v")]);
    }

    [Fact]
    public void ParseEntityMap_SetOfStrings_ParsesGuidedSet()
    {
        Entity entity = ParseSingleEntity("[\"a\",\"b\"]", "{ \"type\": \"Set\", \"element\": { \"type\": \"EntityOrCommon\", \"name\": \"String\" } }");

        Assert.Equal(new CedarSet(new CedarString("a"), new CedarString("b")), entity.Attributes[new CedarString("v")]);
    }

    [Fact]
    public void ParseEntityMap_SetOfEntities_ParsesGuidedSet()
    {
        Entity entity = ParseSingleEntity("[{\"type\":\"User\",\"id\":\"alice\"}]", "{ \"type\": \"Set\", \"element\": { \"type\": \"Entity\", \"name\": \"User\" } }");

        Assert.Equal(
            new CedarSet(new EntityUid(new EntityType("User"), new CedarString("alice"))),
            entity.Attributes[new CedarString("v")]);
    }

    [Fact]
    public void ParseEntityMap_RecordAttribute_ParsesGuidedRecord()
    {
        Entity entity = ParseSingleEntity(
            "{\"name\":\"alice\",\"age\":30}",
            """
            {
              "type": "Record",
              "attributes": {
                "name": { "type": "EntityOrCommon", "name": "String" },
                "age": { "type": "EntityOrCommon", "name": "Long" }
              }
            }
            """);

        Assert.Equal(
            new CedarRecord(new RecordMap
            {
                [new CedarString("name")] = new CedarString("alice"),
                [new CedarString("age")] = new CedarLong(30)
            }),
            entity.Attributes[new CedarString("v")]);
    }

    [Fact]
    public void ParseEntityMap_RecordWithTypeAndId_ParsesRecordNotEntityUid()
    {
        Entity entity = ParseSingleEntity(
            "{\"type\":\"User\",\"id\":\"alice\"}",
            """
            {
              "type": "Record",
              "attributes": {
                "type": { "type": "EntityOrCommon", "name": "String" },
                "id": { "type": "EntityOrCommon", "name": "String" }
              }
            }
            """);

        ICedarData value = entity.Attributes[new CedarString("v")];
        CedarRecord record = Assert.IsType<CedarRecord>(value);

        Assert.Equal(new CedarString("User"), record[new CedarString("type")]);
        Assert.Equal(new CedarString("alice"), record[new CedarString("id")]);
    }

    [Fact]
    public void ParseEntityMap_RecordOptionalMissing_ParsesRecord()
    {
        Entity entity = ParseSingleEntity(
            "{\"name\":\"alice\"}",
            """
            {
              "type": "Record",
              "attributes": {
                "name": { "type": "EntityOrCommon", "name": "String" },
                "age": { "type": "EntityOrCommon", "name": "Long", "required": false }
              }
            }
            """);

        CedarRecord record = Assert.IsType<CedarRecord>(entity.Attributes[new CedarString("v")]);
        Assert.Equal(new CedarString("alice"), record[new CedarString("name")]);
        Assert.False(record.TryGetValue(new CedarString("age"), out _));
    }

    [Fact]
    public void ParseEntityMap_RecordMissingRequired_Throws()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ParseSingleEntity(
            "{}",
            """
            {
              "type": "Record",
              "attributes": {
                "name": { "type": "EntityOrCommon", "name": "String" }
              }
            }
            """));

        Assert.Contains("missing required attribute 'name'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseEntityMap_RecordUnknownKey_Throws()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ParseSingleEntity(
            "{\"name\":\"alice\",\"extra\":\"bad\"}",
            """
            {
              "type": "Record",
              "attributes": {
                "name": { "type": "EntityOrCommon", "name": "String" }
              }
            }
            """));

        Assert.Contains("unknown attribute 'extra'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseEntityMap_ExtensionDuration_ParsesGuidedExtension()
    {
        Entity entity = ParseSingleEntity(
            "{\"__extn\":{\"fn\":\"duration\",\"arg\":\"1h30m\"}}",
            "{ \"type\": \"Extension\", \"name\": \"duration\" }");

        Assert.Equal(CedarDuration.Parse("1h30m"), entity.Attributes[new CedarString("v")]);
    }

    [Fact]
    public void ParseEntityMap_IdenticalDuplicateEntity_SkipsDuplicate()
    {
        SchemaDocument schema = BuildSchema("{ \"type\": \"EntityOrCommon\", \"name\": \"String\" }");
        string entityJson =
            """
            [
              {"uid":{"type":"TestEntity","id":"e1"},"attrs":{"v":"hello"},"parents":[],"tags":{}},
              {"uid":{"type":"TestEntity","id":"e1"},"attrs":{"v":"hello"},"parents":[],"tags":{}}
            ]
            """;

        EntityMap map = SchemaGuidedEntityParser.ParseEntityMap(System.Text.Encoding.UTF8.GetBytes(entityJson), schema);

        Assert.Single(map);
    }

    [Fact]
    public void ParseEntityMap_DifferingDuplicateEntity_Throws()
    {
        SchemaDocument schema = BuildSchema("{ \"type\": \"EntityOrCommon\", \"name\": \"String\" }");
        string entityJson =
            """
            [
              {"uid":{"type":"TestEntity","id":"e1"},"attrs":{"v":"hello"},"parents":[],"tags":{}},
              {"uid":{"type":"TestEntity","id":"e1"},"attrs":{"v":"goodbye"},"parents":[],"tags":{}}
            ]
            """;

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SchemaGuidedEntityParser.ParseEntityMap(System.Text.Encoding.UTF8.GetBytes(entityJson), schema));

        Assert.Contains("Duplicate entity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseEntityMap_TagType_UsesGuidedTagParsing()
    {
        SchemaDocument schema = BuildSchema(
            "{ \"type\": \"EntityOrCommon\", \"name\": \"String\" }",
            tagsTypeJson: "{ \"type\": \"Entity\", \"name\": \"User\" }");
        string entityJson =
            """
            [
              {
                "uid": {"type":"TestEntity","id":"e1"},
                "attrs": {"v":"hello"},
                "parents": [],
                "tags": {"owner": {"type":"User","id":"alice"}}
              }
            ]
            """;

        EntityMap map = SchemaGuidedEntityParser.ParseEntityMap(System.Text.Encoding.UTF8.GetBytes(entityJson), schema);
        Entity entity = Assert.Single(map);

        Assert.Equal(new EntityUid(new EntityType("User"), new CedarString("alice")), entity.Tags[new CedarString("owner")]);
    }

    private static Entity ParseSingleEntity(string valueJson, string attributeTypeJson)
    {
        SchemaDocument schema = BuildSchema(attributeTypeJson);
        string entityJson =
            "["
            + "{"
            + "\"uid\":{\"type\":\"TestEntity\",\"id\":\"e1\"},"
            + "\"attrs\":{\"v\":" + valueJson + "},"
            + "\"parents\":[],"
            + "\"tags\":{}"
            + "}"
            + "]";

        EntityMap map = SchemaGuidedEntityParser.ParseEntityMap(System.Text.Encoding.UTF8.GetBytes(entityJson), schema);
        return Assert.Single(map);
    }

    private static SchemaDocument BuildSchema(string attributeTypeJson, string? tagsTypeJson = null)
    {
        string tagsProperty = tagsTypeJson is null ? string.Empty : $",\n              \"tags\": {tagsTypeJson}";
        string schemaJson =
            "{\n"
            + "  \"\": {\n"
            + "    \"entityTypes\": {\n"
            + "      \"TestEntity\": {\n"
            + "        \"shape\": {\n"
            + "          \"type\": \"Record\",\n"
            + "          \"attributes\": {\n"
            + "            \"v\": " + attributeTypeJson + "\n"
            + "          }\n"
            + "        }" + tagsProperty + "\n"
            + "      },\n"
            + "      \"User\": {}\n"
            + "    },\n"
            + "    \"actions\": {}\n"
            + "  }\n"
            + "}";

        return SchemaDocument.UnmarshalJson(schemaJson);
    }
}

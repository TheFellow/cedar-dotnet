using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cedar.Schema;
using Xunit;

namespace Cedar.Conformance;

public sealed class CorpusSchemaTests
{
    [Theory]
    [MemberData(nameof(CorpusTestData.SchemaKeys), MemberType = typeof(CorpusTestData))]
    public void SchemaRoundTrip_JsonIsStableAcrossCedarRoundTrip(string scenarioFile)
    {
        CorpusScenarioCase scenario = CorpusTestData.GetScenario(scenarioFile);

        SchemaDocument cedarDocument = SchemaDocument.UnmarshalCedar(scenario.SchemaText!, scenario.SchemaPath!);
        string initialJson = cedarDocument.MarshalJson();

        SchemaDocument jsonDocument = SchemaDocument.UnmarshalJson(initialJson);
        string roundTrippedCedar = jsonDocument.MarshalCedar();
        string roundTrippedJson = SchemaDocument.UnmarshalCedar(roundTrippedCedar, scenario.SchemaPath!).MarshalJson();

        Assert.Equal(NormalizeSchemaJson(initialJson), NormalizeSchemaJson(roundTrippedJson));
    }

    [Theory]
    [MemberData(nameof(CorpusTestData.SchemaKeys), MemberType = typeof(CorpusTestData))]
    public void SchemaRustCrossCheck_JsonMatchesRustCorpus(string scenarioFile)
    {
        CorpusScenarioCase scenario = CorpusTestData.GetScenario(scenarioFile);
        Assert.NotNull(scenario.RustSchemaJson);

        SchemaDocument cedarDocument = SchemaDocument.UnmarshalCedar(scenario.SchemaText!, scenario.SchemaPath!);
        string initialJson = cedarDocument.MarshalJson();

        SchemaDocument jsonDocument = SchemaDocument.UnmarshalJson(initialJson);
        string roundTrippedCedar = jsonDocument.MarshalCedar();
        string roundTrippedJson = SchemaDocument.UnmarshalCedar(roundTrippedCedar, scenario.SchemaPath!).MarshalJson();

        Assert.Equal(NormalizeSchemaJson(scenario.RustSchemaJson), NormalizeSchemaJson(roundTrippedJson));
    }

    private static string NormalizeSchemaJson(string json)
    {
        JsonNode? node = JsonNode.Parse(json);
        JsonNode normalized = NormalizeNode(node) ?? throw new JsonException("Schema JSON parsed to null.");
        return normalized.ToJsonString();
    }

    private static JsonNode? NormalizeNode(JsonNode? node)
    {
        return node switch
        {
            null => null,
            JsonObject obj => NormalizeObject(obj),
            JsonArray array => NormalizeArray(array),
            _ => node.DeepClone()
        };
    }

    private static JsonObject NormalizeObject(JsonObject obj)
    {
        JsonObject normalized = [];
        foreach (var property in obj.OrderBy(static property => property.Key, StringComparer.Ordinal))
        {
            if (property.Key == "appliesTo" && IsEmptyAppliesTo(property.Value))
            {
                continue;
            }

            normalized[property.Key] = NormalizeNode(property.Value);
        }

        return normalized;
    }

    private static JsonArray NormalizeArray(JsonArray array)
    {
        JsonArray normalized = [];
        foreach (JsonNode? child in array)
        {
            normalized.Add(NormalizeNode(child));
        }

        return normalized;
    }

    private static bool IsEmptyAppliesTo(JsonNode? node)
    {
        if (node is not JsonObject appliesTo || appliesTo.Count != 2)
        {
            return false;
        }

        return IsEmptyStringArray(appliesTo["principalTypes"]) && IsEmptyStringArray(appliesTo["resourceTypes"]);
    }

    private static bool IsEmptyStringArray(JsonNode? node)
    {
        return node is JsonArray array && array.Count == 0;
    }
}

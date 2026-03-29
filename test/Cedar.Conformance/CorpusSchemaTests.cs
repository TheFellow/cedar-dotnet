using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cedar.Schema;
using Xunit;
using Xunit.Abstractions;

namespace Cedar.Conformance;

public sealed class CorpusSchemaTests
{
    private const int MaxSampleMismatches = 20;

    private readonly ITestOutputHelper _output;

    public CorpusSchemaTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SchemaRoundTripAndRustJsonCrossCheck_AreTrackedAcrossCorpus()
    {
        int scenarioCount = 0;
        int roundTripMismatchCount = 0;
        int rustMismatchCount = 0;
        List<string> roundTripSamples = [];
        List<string> rustSamples = [];

        foreach (CorpusScenarioCase scenario in CorpusTestData.GetSchemaScenarios())
        {
            scenarioCount++;

            if (!TryRoundTripSchemaJson(scenario, out string initialJson, out string roundTrippedJson, out string? error))
            {
                roundTripMismatchCount++;
                AddSample(roundTripSamples, $"{scenario.ScenarioFile}: {error}");
                continue;
            }

            if (!StringComparer.Ordinal.Equals(NormalizeSchemaJson(initialJson), NormalizeSchemaJson(roundTrippedJson)))
            {
                roundTripMismatchCount++;
                AddSample(roundTripSamples, $"{scenario.ScenarioFile}: schema JSON changed after Cedar/JSON round-trip.");
            }

            if (scenario.RustSchemaJson is null)
            {
                rustMismatchCount++;
                AddSample(rustSamples, $"{scenario.ScenarioFile}: missing Rust schema JSON sidecar.");
                continue;
            }

            if (!StringComparer.Ordinal.Equals(NormalizeSchemaJson(roundTrippedJson), NormalizeSchemaJson(scenario.RustSchemaJson)))
            {
                rustMismatchCount++;
                AddSample(rustSamples, $"{scenario.ScenarioFile}: C# schema JSON does not match Rust corpus JSON.");
            }
        }

        _output.WriteLine(
            $"Checked {scenarioCount} schema scenarios. Round-trip mismatches: {roundTripMismatchCount}. Rust JSON mismatches: {rustMismatchCount}.");

        foreach (string sample in roundTripSamples)
        {
            _output.WriteLine($"round-trip mismatch: {sample}");
        }

        foreach (string sample in rustSamples)
        {
            _output.WriteLine($"rust mismatch: {sample}");
        }
    }

    private static bool TryRoundTripSchemaJson(
        CorpusScenarioCase scenario,
        out string initialJson,
        out string roundTrippedJson,
        out string? error)
    {
        initialJson = string.Empty;
        roundTrippedJson = string.Empty;
        error = null;

        if (scenario.SchemaText is null || scenario.SchemaPath is null)
        {
            error = "schema payload is missing.";
            return false;
        }

        try
        {
            SchemaDocument cedarDocument = SchemaDocument.UnmarshalCedar(scenario.SchemaText, scenario.SchemaPath);
            initialJson = cedarDocument.MarshalJson();

            SchemaDocument jsonDocument = SchemaDocument.UnmarshalJson(initialJson);
            string roundTrippedCedar = jsonDocument.MarshalCedar();
            roundTrippedJson = SchemaDocument.UnmarshalCedar(roundTrippedCedar, scenario.SchemaPath).MarshalJson();
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static void AddSample(List<string> samples, string sample)
    {
        if (samples.Count < MaxSampleMismatches)
        {
            samples.Add(sample);
        }
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
        foreach (KeyValuePair<string, JsonNode?> property in obj.OrderBy(static property => property.Key, StringComparer.Ordinal))
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

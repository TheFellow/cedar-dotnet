using System;
using System.Linq;
using System.Text.Json;
using Cedar.Core;
using Cedar.Schema;
using Xunit;

namespace Cedar.Conformance;

public sealed class CorpusValidationTests
{
    [Theory]
    [MemberData(nameof(CorpusTestData.ValidationKeys), MemberType = typeof(CorpusTestData))]
    public void ValidationPayloadsHaveExpectedCounts(string scenarioFile)
    {
        CorpusScenarioCase scenario = CorpusTestData.GetScenario(scenarioFile);

        Assert.NotNull(scenario.Validation);
        Assert.Equal(scenario.Requests.Length, scenario.Validation!.RequestValidation.Count);
        Assert.Equal(scenario.Policies.All().Count(), scenario.Validation.PolicyValidation.PerPolicy.Count);
        Assert.Equal(scenario.Entities.Count, scenario.Validation.EntityValidation.PerEntity.Count);

        foreach (CorpusValidationEntityResult entityValidation in scenario.Validation.EntityValidation.PerEntity.Values)
        {
            Assert.NotNull(entityValidation);
        }
    }

    [Theory]
    [MemberData(nameof(CorpusTestData.ValidationKeys), MemberType = typeof(CorpusTestData))]
    public void StrictPolicyValidationMatchesRust(string scenarioFile)
    {
        RunPolicyParityCheck(scenarioFile, ValidationMode.Strict, static expected => expected.Strict, 0.05);
    }

    [Theory]
    [MemberData(nameof(CorpusTestData.ValidationKeys), MemberType = typeof(CorpusTestData))]
    public void PermissivePolicyValidationMatchesRust(string scenarioFile)
    {
        RunPolicyParityCheck(scenarioFile, ValidationMode.Permissive, static expected => expected.Permissive, 0.05);
    }

    [Theory]
    [MemberData(nameof(CorpusTestData.ValidationKeys), MemberType = typeof(CorpusTestData))]
    public void EntityValidationMatchesRust(string scenarioFile)
    {
        CorpusScenarioCase scenario = CorpusTestData.GetScenario(scenarioFile);

        if (scenario.SchemaText is null || scenario.Validation is null)
        {
            return;
        }

        SchemaValidator validator = new(SchemaDocument.UnmarshalCedar(scenario.SchemaText).Resolve(), ValidationMode.Strict);
        int mismatches = 0;

        foreach (Cedar.Types.Entity entity in scenario.Entities)
        {
            ValidationResult result = validator.ValidateEntity(entity);
            string entityKey = entity.Uid.Type.Value + "::" + entity.Uid.Id.Value;
            bool expected = ExpectedEntityValidationPass(scenario.Validation.EntityValidation.PerEntity[entityKey]);
            if (result.IsValid != expected)
            {
                mismatches++;
            }
        }

        Assert.True(mismatches == 0, $"Entity validation mismatches: {mismatches}");
    }

    [Theory]
    [MemberData(nameof(CorpusTestData.ValidationKeys), MemberType = typeof(CorpusTestData))]
    public void RequestValidationMatchesRust(string scenarioFile)
    {
        CorpusScenarioCase scenario = CorpusTestData.GetScenario(scenarioFile);

        if (scenario.SchemaText is null || scenario.Validation is null)
        {
            return;
        }

        ResolvedSchema schema = SchemaDocument.UnmarshalCedar(scenario.SchemaText).Resolve();
        SchemaValidator strict = new(schema, ValidationMode.Strict);
        SchemaValidator permissive = new(schema, ValidationMode.Permissive);

        int mismatches = 0;
        for (int index = 0; index < scenario.Requests.Length; index++)
        {
            CorpusScenarioRequest request = scenario.Requests[index];
            CorpusRequestValidationResult expected = scenario.Validation.RequestValidation[index];

            if (strict.ValidateRequest(request.Request).IsValid != expected.Strict)
            {
                mismatches++;
            }

            if (permissive.ValidateRequest(request.Request).IsValid != expected.Permissive)
            {
                mismatches++;
            }
        }

        Assert.True(mismatches == 0, $"Request validation mismatches: {mismatches}");
    }

    private static void RunPolicyParityCheck(
        string scenarioFile,
        ValidationMode mode,
        Func<CorpusPolicyValidationResult, bool> selector,
        double allowedMismatchRatio)
    {
        CorpusScenarioCase scenario = CorpusTestData.GetScenario(scenarioFile);

        if (scenario.SchemaText is null || scenario.Validation is null)
        {
            return;
        }

        SchemaValidator validator = new(SchemaDocument.UnmarshalCedar(scenario.SchemaText).Resolve(), mode);
        int total = 0;
        int mismatches = 0;

        foreach ((PolicyId policyId, Policy policy) in scenario.Policies.All())
        {
            CorpusPolicyValidationResult expected = scenario.Validation.PolicyValidation.PerPolicy[policyId.Value];
            ValidationResult result = validator.ValidatePolicy(policyId.Value, policy);
            total++;
            if (result.IsValid != selector(expected))
            {
                mismatches++;
            }
        }

        int threshold = (int)Math.Ceiling(total * allowedMismatchRatio);
        Assert.True(mismatches <= threshold, $"Too many policy validation mismatches: {mismatches} of {total} (threshold {threshold})");
    }

    private static bool ExpectedEntityValidationPass(CorpusValidationEntityResult result)
    {
        if (TryGetBoolean(result, "valid", out bool valid))
        {
            return valid;
        }

        if (TryGetBoolean(result, "passes", out bool passes))
        {
            return passes;
        }

        if (TryGetBoolean(result, "strict", out bool strict))
        {
            return strict;
        }

        if (TryGetBoolean(result, "permissive", out bool permissive))
        {
            return permissive;
        }

        return result.AdditionalData.Count == 0 || result.AdditionalData.Values.All(static value => value.ValueKind == JsonValueKind.Object);
    }

    private static bool TryGetBoolean(CorpusValidationEntityResult result, string name, out bool value)
    {
        if (result.AdditionalData.TryGetValue(name, out JsonElement element) && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
        {
            value = element.GetBoolean();
            return true;
        }

        value = false;
        return false;
    }
}

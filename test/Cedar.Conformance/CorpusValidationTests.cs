using System.Linq;
using Xunit;

namespace Cedar.Conformance;

public sealed class CorpusValidationTests
{
    [Theory]
    [MemberData(nameof(CorpusTestData.ValidationScenarios), MemberType = typeof(CorpusTestData))]
    public void ValidationPayloadsParseForEachScenario(CorpusScenarioCase scenario)
    {
        Assert.NotNull(scenario.Validation);

        Assert.Equal(scenario.Requests.Length, scenario.Validation!.RequestValidation.Count);
        Assert.Equal(scenario.Policies.All().Count(), scenario.Validation.PolicyValidation.PerPolicy.Count);
        Assert.Equal(scenario.Entities.Count, scenario.Validation.EntityValidation.PerEntity.Count);

        foreach (CorpusValidationEntityResult entityValidation in scenario.Validation.EntityValidation.PerEntity.Values)
        {
            Assert.NotNull(entityValidation);
        }
    }
}

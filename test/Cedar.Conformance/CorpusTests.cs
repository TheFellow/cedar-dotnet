using System.Linq;
using Cedar.Core;
using Xunit;

namespace Cedar.Conformance;

public sealed class CorpusTests
{
    [Theory]
    [MemberData(nameof(CorpusTestData.RequestKeys), MemberType = typeof(CorpusTestData))]
    public void AuthorizationMatchesCorpusExpectations(string scenarioFile, int requestIndex)
    {
        CorpusScenarioCase scenario = CorpusTestData.GetScenario(scenarioFile);
        CorpusScenarioRequest request = scenario.Requests[requestIndex];

        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(scenario.Policies, scenario.Entities, request.Request);

        Assert.Equal(request.ExpectedDecision, decision);
        CorpusAssertions.AssertEqualSorted(request.ExpectedReasons, diagnostic.Reasons.Select(static reason => reason.PolicyId.Value));
        CorpusAssertions.AssertEqualSorted(request.ExpectedErrors, diagnostic.Errors.Select(static error => error.PolicyId.Value));
    }
}

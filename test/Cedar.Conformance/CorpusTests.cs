using System.Linq;
using Cedar.Core;
using Xunit;

namespace Cedar.Conformance;

public sealed class CorpusTests
{
    [Theory]
    [MemberData(nameof(CorpusTestData.Requests), MemberType = typeof(CorpusTestData))]
    public void AuthorizationMatchesCorpusExpectations(CorpusRequestCase testCase)
    {
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(testCase.Policies, testCase.Entities, testCase.Request);

        Assert.Equal(testCase.ExpectedDecision, decision);
        CorpusAssertions.AssertEqualSorted(testCase.ExpectedReasons, diagnostic.Reasons.Select(static reason => reason.PolicyId.Value));
        CorpusAssertions.AssertEqualSorted(testCase.ExpectedErrors, diagnostic.Errors.Select(static error => error.PolicyId.Value));
    }
}

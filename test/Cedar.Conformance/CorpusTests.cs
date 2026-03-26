using System;
using System.Collections.Generic;
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
        AssertEqualSorted(testCase.ExpectedReasons, diagnostic.Reasons.Select(static reason => reason.PolicyId.Value));
        AssertEqualSorted(testCase.ExpectedErrors, diagnostic.Errors.Select(static error => error.PolicyId.Value));
    }

    private static void AssertEqualSorted(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        List<string> expectedList = expected.OrderBy(static item => item, StringComparer.Ordinal).ToList();
        List<string> actualList = actual.OrderBy(static item => item, StringComparer.Ordinal).ToList();
        Assert.Equal(expectedList, actualList);
    }
}

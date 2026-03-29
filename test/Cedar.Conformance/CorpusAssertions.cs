using System;
using System.Collections.Generic;
using System.Linq;
using Cedar.Core;
using Cedar.Types;
using Xunit;

namespace Cedar.Conformance;

internal static class CorpusAssertions
{
    public static void AssertAuthorizationMatchesExpected(CorpusScenarioCase scenario, PolicySet policies, EntityMap entities)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(entities);

        foreach (CorpusScenarioRequest request in scenario.Requests)
        {
            AssertAuthorizationMatchesExpected(request, policies, entities);
        }
    }

    public static void AssertAuthorizationMatchesExpected(CorpusScenarioRequest request, PolicySet policies, EntityMap entities)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(entities);

        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, entities, request.Request);

        Assert.Equal(request.ExpectedDecision, decision);
        AssertEqualSorted(request.ExpectedReasons, diagnostic.Reasons.Select(static reason => reason.PolicyId.Value));
        AssertEqualSorted(request.ExpectedErrors, diagnostic.Errors.Select(static error => error.PolicyId.Value));
    }

    public static void AssertEqualSorted(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        List<string> expectedList = expected.OrderBy(static item => item, StringComparer.Ordinal).ToList();
        List<string> actualList = actual.OrderBy(static item => item, StringComparer.Ordinal).ToList();
        Assert.Equal(expectedList, actualList);
    }
}

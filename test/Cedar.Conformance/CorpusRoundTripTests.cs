using System.Collections.Generic;
using System.Linq;
using Cedar.Core;
using Cedar.Types;
using Xunit;

namespace Cedar.Conformance;

public sealed class CorpusRoundTripTests
{
    [Theory]
    [MemberData(nameof(CorpusTestData.Scenarios), MemberType = typeof(CorpusTestData))]
    public void AuthorizationRemainsStableAcrossPolicyAndEntityRoundTrips(CorpusScenarioCase scenario)
    {
        PolicySet cedarPolicyRoundTrip = PolicySet.ParseCedar(scenario.Policies.MarshalCedar());
        CorpusAssertions.AssertAuthorizationMatchesExpected(scenario, cedarPolicyRoundTrip, scenario.Entities);

        PolicySet jsonPolicyRoundTrip = PolicySet.UnmarshalJson(scenario.Policies.MarshalJson());
        CorpusAssertions.AssertAuthorizationMatchesExpected(scenario, jsonPolicyRoundTrip, scenario.Entities);

        string entityJson = ConformanceJson.SerializeEntityMap(scenario.Entities);
        EntityMap entityRoundTrip = ConformanceJson.DeserializeEntityMap(entityJson);

        List<Entity> expectedEntities = scenario.Entities.ToList();
        List<Entity> actualEntities = entityRoundTrip.ToList();
        Assert.Equal(expectedEntities, actualEntities);
    }
}

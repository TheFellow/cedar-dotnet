using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema.Internal.Validate;

internal sealed record RequestEnvironment(
    EntityType PrincipalType,
    EntityUid ActionUid,
    EntityType ResourceType,
    CedarRecordType ContextType)
{
    internal static List<RequestEnvironment> Generate(ResolvedSchema schema)
    {
        List<RequestEnvironment> environments = [];
        foreach ((EntityUid uid, ResolvedAction action) in schema.Actions)
        {
            if (action.AppliesTo is null)
            {
                continue;
            }

            CedarRecordType context = CedarTypeOps.SchemaRecordToCedarRecord(action.AppliesTo.Context);
            foreach (EntityType principalType in action.AppliesTo.Principals)
            {
                foreach (EntityType resourceType in action.AppliesTo.Resources)
                {
                    environments.Add(new RequestEnvironment(principalType, uid, resourceType, context));
                }
            }
        }

        return environments;
    }

    internal static List<RequestEnvironment> FilterForPolicy(
        IEnumerable<RequestEnvironment> environments,
        EntityType[]? principalTypes,
        EntityType[]? resourceTypes,
        EntityUid[]? actionUids)
    {
        List<RequestEnvironment> result = [];
        foreach (RequestEnvironment environment in environments)
        {
            if (!MatchesEntityTypeConstraint(environment.PrincipalType, principalTypes))
            {
                continue;
            }

            if (!MatchesEntityTypeConstraint(environment.ResourceType, resourceTypes))
            {
                continue;
            }

            if (!MatchesActionConstraint(environment.ActionUid, actionUids))
            {
                continue;
            }

            result.Add(environment);
        }

        return result;
    }

    private static bool MatchesEntityTypeConstraint(EntityType entityType, EntityType[]? constraints)
    {
        return constraints is null || System.Array.IndexOf(constraints, entityType) >= 0;
    }

    private static bool MatchesActionConstraint(EntityUid actionUid, EntityUid[]? constraints)
    {
        return constraints is null || System.Array.IndexOf(constraints, actionUid) >= 0;
    }
}

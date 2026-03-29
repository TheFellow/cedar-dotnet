using System;
using System.Collections.Generic;
using System.Linq;
using Cedar.Ast.Internal;
using Cedar.Types;

namespace Cedar.Schema.Internal.Validate;

internal static class ScopeValidator
{
    internal static (EntityType[]? EntityTypes, List<ValidationIssue> Errors) ValidatePrincipalScope(IScope scope, SchemaValidator validator)
    {
        return scope switch
        {
            ScopeAll => (null, []),
            ScopeEq scopeEq => ValidateScopeEntity(scopeEq.Entity, validator),
            ScopeIn scopeIn => ValidateInScope(scopeIn.Entity, validator),
            ScopeIs scopeIs => ValidateScopeType(CedarPathToEntityType(scopeIs.Type), validator),
            ScopeIsIn scopeIsIn => ValidateIsInScope(scopeIsIn, validator),
            _ => ([], [new ValidationIssue($"unsupported principal scope {scope.GetType().Name}")])
        };
    }

    internal static (EntityUid[]? ActionUids, List<ValidationIssue> Errors) ValidateAndGetActionUids(IScope scope, SchemaValidator validator)
    {
        List<ValidationIssue> errors = [];

        return scope switch
        {
            ScopeAll => (null, errors),
            ScopeEq scopeEq => ValidateActionEq(scopeEq.Entity, validator, errors),
            ScopeIn scopeIn => ValidateActionIn(scopeIn.Entity, validator, errors),
            ScopeInSet scopeInSet => ValidateActionInSet(scopeInSet.Entities, validator, errors),
            _ => (Array.Empty<EntityUid>(), [new ValidationIssue($"unsupported action scope {scope.GetType().Name}")])
        };
    }

    internal static (EntityType[]? EntityTypes, List<ValidationIssue> Errors) ValidateResourceScope(IScope scope, SchemaValidator validator)
    {
        return scope switch
        {
            ScopeAll => (null, []),
            ScopeEq scopeEq => ValidateScopeEntity(scopeEq.Entity, validator),
            ScopeIn scopeIn => ValidateInScope(scopeIn.Entity, validator),
            ScopeIs scopeIs => ValidateScopeType(CedarPathToEntityType(scopeIs.Type), validator),
            ScopeIsIn scopeIsIn => ValidateIsInScope(scopeIsIn, validator),
            _ => ([], [new ValidationIssue($"unsupported resource scope {scope.GetType().Name}")])
        };
    }

    internal static ValidationIssue? ValidateActionApplication(
        EntityType[]? principalTypes,
        EntityType[]? resourceTypes,
        EntityUid[]? actionUids,
        SchemaValidator validator)
    {
        if (principalTypes is null && resourceTypes is null && actionUids is null)
        {
            return null;
        }

        List<ResolvedAction> actions = [];
        bool hasUnknownAction = false;

        if (actionUids is null)
        {
            actions.AddRange(validator.Schema.Actions.Values);
        }
        else
        {
            foreach (EntityUid uid in actionUids)
            {
                if (validator.Schema.Actions.TryGetValue(uid, out ResolvedAction? action))
                {
                    actions.Add(action);
                }
                else
                {
                    hasUnknownAction = true;
                }
            }
        }

        if (hasUnknownAction)
        {
            return new ValidationIssue("unable to find an applicable action given the policy scope constraints");
        }

        foreach (ResolvedAction action in actions)
        {
            if (action.AppliesTo is null)
            {
                continue;
            }

            bool principalMatch = principalTypes is null || principalTypes.Any(action.AppliesTo.Principals.Contains);
            bool resourceMatch = resourceTypes is null || resourceTypes.Any(action.AppliesTo.Resources.Contains);
            if (principalMatch && resourceMatch)
            {
                return null;
            }
        }

        return new ValidationIssue("unable to find an applicable action given the policy scope constraints");
    }

    internal static EntityUid[] GetActionsInSet(IEnumerable<EntityUid> uids, SchemaValidator validator)
    {
        List<EntityUid> result = [];
        foreach (EntityUid uid in uids)
        {
            result.Add(uid);
            foreach (EntityUid actionUid in validator.Schema.Actions.Keys)
            {
                if (actionUid != uid && IsActionDescendant(actionUid, uid, validator))
                {
                    result.Add(actionUid);
                }
            }
        }

        return result.ToArray();
    }

    internal static EntityType[] GetEntityTypesIn(EntityType target, SchemaValidator validator)
    {
        List<EntityType> result = [target];
        foreach ((EntityType name, ResolvedEntity entity) in validator.Schema.Entities)
        {
            if (entity.ParentTypes.Contains(target))
            {
                result.Add(name);
            }
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach ((EntityType name, ResolvedEntity entity) in validator.Schema.Entities)
            {
                if (result.Contains(name))
                {
                    continue;
                }

                foreach (EntityType parent in entity.ParentTypes)
                {
                    if (result.Contains(parent))
                    {
                        result.Add(name);
                        changed = true;
                        break;
                    }
                }
            }
        }

        return result.ToArray();
    }

    internal static EntityType CedarPathToEntityType(CedarPath path)
    {
        return new EntityType(path.Value);
    }

    private static (EntityType[]? EntityTypes, List<ValidationIssue> Errors) ValidateInScope(EntityUid uid, SchemaValidator validator)
    {
        (EntityType[]? entityTypes, List<ValidationIssue> errors) = ValidateScopeEntity(uid, validator);
        if (errors.Count > 0)
        {
            return (Array.Empty<EntityType>(), errors);
        }

        return (GetEntityTypesIn(uid.Type, validator), []);
    }

    private static (EntityType[]? EntityTypes, List<ValidationIssue> Errors) ValidateIsInScope(ScopeIsIn scope, SchemaValidator validator)
    {
        (EntityType[]? entityTypes, List<ValidationIssue> typeErrors) = ValidateScopeType(CedarPathToEntityType(scope.Type), validator);
        if (typeErrors.Count > 0)
        {
            return (Array.Empty<EntityType>(), typeErrors);
        }

        (EntityType[]? _, List<ValidationIssue> entityErrors) = ValidateScopeEntity(scope.Entity, validator);
        if (entityErrors.Count > 0)
        {
            return (Array.Empty<EntityType>(), entityErrors);
        }

        EntityType[] typesIn = GetEntityTypesIn(scope.Entity.Type, validator);
        return typesIn.Contains(CedarPathToEntityType(scope.Type))
            ? (entityTypes, [])
            : (Array.Empty<EntityType>(), []);
    }

    private static (EntityType[]? EntityTypes, List<ValidationIssue> Errors) ValidateScopeEntity(EntityUid uid, SchemaValidator validator)
    {
        EntityType type = uid.Type;
        if (validator.IsKnownEntityType(type))
        {
            return ([type], []);
        }

        if (CedarTypeOps.IsActionEntity(type) && validator.Schema.Actions.ContainsKey(uid))
        {
            return ([type], []);
        }

        return (null, [new ValidationIssue($"unrecognized entity type `{type}`")]);
    }

    private static (EntityType[]? EntityTypes, List<ValidationIssue> Errors) ValidateScopeType(EntityType entityType, SchemaValidator validator)
    {
        return validator.IsKnownEntityType(entityType)
            ? ([entityType], [])
            : (null, [new ValidationIssue($"unrecognized entity type `{entityType}`")]);
    }

    private static (EntityUid[]? ActionUids, List<ValidationIssue> Errors) ValidateActionEq(EntityUid uid, SchemaValidator validator, List<ValidationIssue> errors)
    {
        if (!validator.Schema.Actions.ContainsKey(uid))
        {
            errors.Add(new ValidationIssue($"unrecognized action `{uid}`"));
        }

        return ([uid], errors);
    }

    private static (EntityUid[]? ActionUids, List<ValidationIssue> Errors) ValidateActionIn(EntityUid uid, SchemaValidator validator, List<ValidationIssue> errors)
    {
        if (!validator.Schema.Actions.ContainsKey(uid))
        {
            errors.Add(new ValidationIssue($"unrecognized action `{uid}`"));
        }

        return (GetActionsInSet([uid], validator), errors);
    }

    private static (EntityUid[]? ActionUids, List<ValidationIssue> Errors) ValidateActionInSet(EntityUid[] uids, SchemaValidator validator, List<ValidationIssue> errors)
    {
        foreach (EntityUid uid in uids)
        {
            if (!validator.Schema.Actions.ContainsKey(uid))
            {
                errors.Add(new ValidationIssue($"unrecognized action `{uid}`"));
            }
        }

        return (GetActionsInSet(uids, validator), errors);
    }

    private static bool IsActionDescendant(EntityUid actionUid, EntityUid ancestorUid, SchemaValidator validator)
    {
        ResolvedAction action = validator.Schema.Actions[actionUid];
        foreach (EntityUid parent in action.Entity.Parents)
        {
            if (parent == ancestorUid || IsActionDescendant(parent, ancestorUid, validator))
            {
                return true;
            }
        }

        return false;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Cedar.Schema.Internal.Validate;
using Cedar.Types;

namespace Cedar.Schema;

public sealed partial class SchemaValidator
{
    public ValidationResult ValidateEntity(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (IsActionEntityType(entity.Uid.Type))
        {
            return ValidateActionEntity(entity);
        }

        if (_schema.Entities.TryGetValue(entity.Uid.Type, out ResolvedEntity? schemaEntity))
        {
            return ValidateRegularEntity(entity, schemaEntity);
        }

        if (_schema.Enums.ContainsKey(entity.Uid.Type))
        {
            return ValidationResult.Success;
        }

        return ValidationResult.Failure($"entity type \"{entity.Uid.Type}\" not found in schema");
    }

    public ValidationResult ValidateEntities(EntityMap entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (Entity entity in entities)
        {
            ValidationResult result = ValidateEntity(entity);
            if (result.IsValid)
            {
                continue;
            }

            bool isDeserError = result.Errors.Count > 0 && result.Errors[0].StartsWith(ValueChecker.DeserializationPrefix, StringComparison.Ordinal);
            return ValidationResult.Failure(isDeserError ? "error during entity deserialization" : "entity does not conform to the schema");
        }

        return ValidationResult.Success;
    }

    private ValidationResult ValidateActionEntity(Entity entity)
    {
        if (!_schema.Actions.TryGetValue(entity.Uid, out ResolvedAction? action))
        {
            return ValidationResult.Failure($"action {entity.Uid} not found in schema");
        }

        if (entity.Attributes.Count > 0)
        {
            return ValidationResult.Failure($"action {entity.Uid} should not have attributes");
        }

        if (entity.Tags.Count > 0)
        {
            return ValidationResult.Failure($"action {entity.Uid} should not have tags");
        }

        HashSet<EntityUid> closure = [];
        void Walk(EntityUid uid)
        {
            if (!closure.Add(uid))
            {
                return;
            }

            if (_schema.Actions.TryGetValue(uid, out ResolvedAction? schemaAction))
            {
                foreach (EntityUid parent in schemaAction.Entity.Parents)
                {
                    Walk(parent);
                }
            }
        }

        foreach (EntityUid parent in action.Entity.Parents)
        {
            Walk(parent);
        }

        foreach (EntityUid parent in entity.Parents)
        {
            if (!closure.Contains(parent))
            {
                return ValidationResult.Failure($"action {entity.Uid} has unexpected parent {parent}");
            }
        }

        foreach (EntityUid parent in closure)
        {
            if (!entity.Parents.Contains(parent))
            {
                return ValidationResult.Failure($"action {entity.Uid} missing expected parent {parent}");
            }
        }

        return ValidationResult.Success;
    }

    private ValidationResult ValidateRegularEntity(Entity entity, ResolvedEntity schemaEntity)
    {
        foreach (EntityUid parent in entity.Parents)
        {
            if (!schemaEntity.ParentTypes.Contains(parent.Type))
            {
                return ValidationResult.Failure($"invalid parent type \"{parent.Type}\" for entity type \"{entity.Uid.Type}\"");
            }
        }

        (bool isDeserError, string? error) = ValueChecker.CheckRecord(entity.Attributes, schemaEntity.Shape);
        if (error is not null)
        {
            return ValidationResult.Failure(ValueChecker.PrefixError(isDeserError, error));
        }

        if (schemaEntity.Tags is null)
        {
            if (entity.Tags.Count > 0)
            {
                return ValidationResult.Failure(ValueChecker.PrefixError(true, $"entity type \"{entity.Uid.Type}\" does not allow tags"));
            }

            return ValidationResult.Success;
        }

        foreach (KeyValuePair<Cedar.Types.CedarString, ICedarData> tag in entity.Tags)
        {
            (isDeserError, error) = ValueChecker.CheckValue(tag.Value, schemaEntity.Tags);
            if (error is not null)
            {
                return ValidationResult.Failure(ValueChecker.PrefixError(isDeserError, error));
            }
        }

        return ValidationResult.Success;
    }

    private static bool IsActionEntityType(EntityType type)
    {
        string value = type.Value;
        return value == "Action" || value.EndsWith("::Action", StringComparison.Ordinal);
    }
}

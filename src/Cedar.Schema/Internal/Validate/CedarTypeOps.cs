using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Cedar.Types;

namespace Cedar.Schema.Internal.Validate;

internal static class CedarTypeOps
{
    internal static string CedarTypeName(CedarType type)
    {
        return type switch
        {
            CedarNever => "__cedar::internal::Never",
            CedarTrue => "__cedar::internal::True",
            CedarFalse => "__cedar::internal::False",
            CedarBool => "Bool",
            CedarLong => "Long",
            CedarString => "String",
            CedarSetType setType => "Set<" + CedarTypeName(setType.Element) + ">",
            CedarRecordType recordType => CedarRecordTypeName(recordType),
            CedarEntityType entityType => CedarEntityTypeName(entityType.Lub),
            CedarExtType extensionType => extensionType.Name.Value,
            _ => "?"
        };
    }

    internal static int CedarTypeKindRank(CedarType type)
    {
        return type switch
        {
            CedarTrue => 0,
            CedarFalse => 1,
            CedarBool => 2,
            CedarNever => 3,
            CedarLong => 4,
            CedarString => 5,
            CedarSetType => 6,
            CedarRecordType => 7,
            CedarEntityType => 8,
            CedarExtType => 9,
            _ => -1
        };
    }

    internal static int CompareCedarType(CedarType left, CedarType right)
    {
        int leftRank = CedarTypeKindRank(left);
        int rightRank = CedarTypeKindRank(right);
        if (leftRank != rightRank)
        {
            return leftRank - rightRank;
        }

        return (left, right) switch
        {
            (CedarSetType leftSet, CedarSetType rightSet) => CompareCedarType(leftSet.Element, rightSet.Element),
            (CedarRecordType leftRecord, CedarRecordType rightRecord) => CompareRecordTypes(leftRecord, rightRecord),
            (CedarEntityType leftEntity, CedarEntityType rightEntity) => CompareEntityLub(leftEntity.Lub, rightEntity.Lub),
            _ => StringComparer.Ordinal.Compare(CedarTypeName(left), CedarTypeName(right))
        };
    }

    internal static (CedarType? Type, string? Error) LeastUpperBound(CedarType left, CedarType right, bool strict)
    {
        if (right is CedarNever)
        {
            return (left, null);
        }

        return left switch
        {
            CedarNever => (right, null),
            CedarTrue when right is CedarTrue => (new CedarTrue(), null),
            CedarTrue when right is CedarFalse or CedarBool => (new CedarBool(), null),
            CedarFalse when right is CedarFalse => (new CedarFalse(), null),
            CedarFalse when right is CedarTrue or CedarBool => (new CedarBool(), null),
            CedarBool when right is CedarTrue or CedarFalse or CedarBool => (new CedarBool(), null),
            CedarLong when right is CedarLong => (new CedarLong(), null),
            CedarString when right is CedarString => (new CedarString(), null),
            CedarSetType leftSet when right is CedarSetType rightSet => LeastUpperBoundSet(leftSet, rightSet, strict),
            CedarRecordType leftRecord when right is CedarRecordType rightRecord => LubRecord(leftRecord, rightRecord, strict),
            CedarEntityType leftEntity when right is CedarEntityType rightEntity => (new CedarEntityType(leftEntity.Lub.Union(rightEntity.Lub)), null),
            CedarExtType leftExt when right is CedarExtType rightExt && leftExt.Name == rightExt.Name => (leftExt, null),
            _ => (null, "incompatible types for least upper bound")
        };
    }

    internal static (CedarType? Type, string? Error) LubRecord(CedarRecordType left, CedarRecordType right, bool strict)
    {
        if (strict)
        {
            if (left.Attrs.Count != right.Attrs.Count || left.Attrs.Keys.Except(right.Attrs.Keys, StringComparer.Ordinal).Any())
            {
                return (null, "record types have different attributes in strict mode");
            }
        }

        Dictionary<string, CedarAttr> attributes = new(StringComparer.Ordinal);

        foreach ((string key, CedarAttr leftAttr) in left.Attrs)
        {
            if (right.Attrs.TryGetValue(key, out CedarAttr rightAttr))
            {
                (CedarType? type, string? error) = LeastUpperBound(leftAttr.Type, rightAttr.Type, strict);
                if (error is not null)
                {
                    if (strict)
                    {
                        return (null, error);
                    }

                    continue;
                }

                attributes[key] = new CedarAttr(type!, leftAttr.Required && rightAttr.Required);
                continue;
            }

            attributes[key] = new CedarAttr(leftAttr.Type, false);
        }

        foreach ((string key, CedarAttr rightAttr) in right.Attrs)
        {
            if (!left.Attrs.ContainsKey(key))
            {
                attributes[key] = new CedarAttr(rightAttr.Type, false);
            }
        }

        return (new CedarRecordType(attributes), null);
    }

    internal static bool IsSubtype(CedarType left, CedarType right)
    {
        return right switch
        {
            CedarString => left is CedarString,
            CedarExtType rightExt => left is CedarExtType leftExt && leftExt.Name == rightExt.Name,
            _ => false
        };
    }

    internal static CedarRecordType SchemaRecordToCedarRecord(ResolvedRecordType record)
    {
        Dictionary<string, CedarAttr> attributes = new(StringComparer.Ordinal);
        foreach ((string name, ResolvedAttribute attribute) in record.Attributes)
        {
            attributes[name] = new CedarAttr(ResolvedTypeToCedarType(attribute.Type), !attribute.Optional);
        }

        return new CedarRecordType(attributes);
    }

    internal static CedarType ResolvedTypeToCedarType(ResolvedType type)
    {
        return type switch
        {
            ResolvedStringType => new CedarString(),
            ResolvedLongType => new CedarLong(),
            ResolvedBoolType => new CedarBool(),
            ResolvedExtensionType extensionType => new CedarExtType(extensionType.Name),
            ResolvedSetType setType => new CedarSetType(ResolvedTypeToCedarType(setType.Element)),
            ResolvedRecordType recordType => SchemaRecordToCedarRecord(recordType),
            ResolvedEntityType entityType => new CedarEntityType(EntityLub.Single(entityType.Name)),
            _ => throw new InvalidOperationException($"unsupported resolved type {type.GetType().FullName}")
        };
    }

    internal static CedarAttr? LookupAttributeType(CedarType type, string attr, ResolvedSchema schema, bool strict)
    {
        return type switch
        {
            CedarRecordType recordType => recordType.Attrs.TryGetValue(attr, out CedarAttr attribute) ? attribute : null,
            CedarEntityType entityType => LookupEntityAttr(entityType.Lub, attr, schema, strict),
            _ => null
        };
    }

    internal static CedarAttr? LookupEntityAttr(EntityLub lub, string attr, ResolvedSchema schema, bool strict)
    {
        CedarAttr? result = null;
        foreach (EntityType entityType in lub.Elements)
        {
            if (!schema.Entities.TryGetValue(entityType, out ResolvedEntity? entity))
            {
                return null;
            }

            if (!entity.Shape.Attributes.TryGetValue(attr, out ResolvedAttribute? schemaAttr))
            {
                return null;
            }

            CedarAttr current = new(ResolvedTypeToCedarType(schemaAttr.Type), !schemaAttr.Optional);
            if (result is null)
            {
                result = current;
                continue;
            }

            (CedarType? lubType, string? error) = LeastUpperBound(result.Value.Type, current.Type, strict);
            if (error is not null)
            {
                return null;
            }

            result = new CedarAttr(lubType!, result.Value.Required && current.Required);
        }

        return result;
    }

    internal static bool EntityHasTags(EntityLub lub, ResolvedSchema schema)
    {
        foreach (EntityType entityType in lub.Elements)
        {
            if (!schema.Entities.TryGetValue(entityType, out ResolvedEntity? entity) || entity.Tags is null)
            {
                return false;
            }
        }

        return true;
    }

    internal static (CedarType Type, string? Error) EntityTagType(EntityLub lub, ResolvedSchema schema, bool strict)
    {
        CedarType result = new CedarNever();
        foreach (EntityType entityType in lub.Elements)
        {
            if (!schema.Entities.TryGetValue(entityType, out ResolvedEntity? entity) || entity.Tags is null)
            {
                return (new CedarNever(), null);
            }

            CedarType tagType = ResolvedTypeToCedarType(entity.Tags);
            (CedarType? lubType, string? error) = LeastUpperBound(result, tagType, strict);
            if (error is not null)
            {
                return (new CedarNever(), TypeIncompatErr(result, tagType));
            }

            result = lubType!;
        }

        return (result, null);
    }

    internal static bool IsEntityDescendant(EntityType child, EntityType ancestor, ResolvedSchema schema)
    {
        if (!schema.Entities.TryGetValue(child, out ResolvedEntity? entity))
        {
            return false;
        }

        foreach (EntityType parent in entity.ParentTypes)
        {
            if (parent == ancestor || IsEntityDescendant(parent, ancestor, schema))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool AnyEntityDescendantOf(EntityLub left, EntityLub right, ResolvedSchema schema)
    {
        foreach (EntityType leftType in left.Elements)
        {
            foreach (EntityType rightType in right.Elements)
            {
                if (leftType == rightType || IsEntityDescendant(leftType, rightType, schema))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static string? CheckStrictEntityLUB(CedarType left, CedarType right)
    {
        if (left is CedarNever || left is not CedarEntityType leftEntity || right is not CedarEntityType rightEntity)
        {
            return null;
        }

        foreach (EntityType leftType in leftEntity.Lub.Elements)
        {
            if (rightEntity.Lub.Elements.Contains(leftType))
            {
                return null;
            }
        }

        return "entity types are incompatible in strict mode";
    }

    internal static string TypeIncompatErr(CedarType left, CedarType right)
    {
        string leftName = CedarTypeName(left);
        string rightName = CedarTypeName(right);
        if (CompareCedarType(left, right) > 0)
        {
            (leftName, rightName) = (rightName, leftName);
        }

        return $"the types {leftName} and {rightName} are not compatible";
    }

    internal static string TypeIncompatErrMulti(IEnumerable<CedarType> types)
    {
        List<string> names = types
            .OrderBy(static type => type, Comparer<CedarType>.Create(CompareCedarType))
            .Select(CedarTypeName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (names.Count == 2)
        {
            return $"the types {names[0]} and {names[1]} are not compatible";
        }

        StringBuilder builder = new();
        builder.Append("the types ");
        for (int index = 0; index < names.Count - 1; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(names[index]);
        }

        builder.Append(", and ");
        builder.Append(names[^1]);
        builder.Append(" are not compatible");
        return builder.ToString();
    }

    internal static bool IsActionEntity(EntityType entityType)
    {
        string value = entityType.Value;
        return value == "Action" || value.EndsWith("::Action", StringComparison.Ordinal);
    }

    private static (CedarType? Type, string? Error) LeastUpperBoundSet(CedarSetType left, CedarSetType right, bool strict)
    {
        (CedarType? element, string? error) = LeastUpperBound(left.Element, right.Element, strict);
        return error is null
            ? (new CedarSetType(element!), null)
            : (null, error);
    }

    private static int CompareRecordTypes(CedarRecordType left, CedarRecordType right)
    {
        string[] leftKeys = left.Attrs.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();
        string[] rightKeys = right.Attrs.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();
        int count = Math.Min(leftKeys.Length, rightKeys.Length);

        for (int index = 0; index < count; index++)
        {
            int keyComparison = StringComparer.Ordinal.Compare(leftKeys[index], rightKeys[index]);
            if (keyComparison != 0)
            {
                return keyComparison;
            }

            int typeComparison = CompareCedarType(left.Attrs[leftKeys[index]].Type, right.Attrs[rightKeys[index]].Type);
            if (typeComparison != 0)
            {
                return typeComparison;
            }
        }

        return leftKeys.Length.CompareTo(rightKeys.Length);
    }

    private static int CompareEntityLub(EntityLub left, EntityLub right)
    {
        int count = Math.Min(left.Elements.Length, right.Elements.Length);
        for (int index = 0; index < count; index++)
        {
            int comparison = StringComparer.Ordinal.Compare(left.Elements[index].Value, right.Elements[index].Value);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Elements.Length.CompareTo(right.Elements.Length);
    }

    private static string CedarEntityTypeName(EntityLub lub)
    {
        if (lub.Elements.Length == 1)
        {
            return lub.Elements[0].Value;
        }

        return "__cedar::internal::Union<" + string.Join(", ", lub.Elements.Select(static entityType => entityType.Value)) + ">";
    }

    private static string CedarRecordTypeName(CedarRecordType record)
    {
        if (record.Attrs.Count == 0)
        {
            return "{}";
        }

        StringBuilder builder = new();
        builder.Append('{');

        foreach ((string key, CedarAttr attribute) in record.Attrs.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(key);
            if (!attribute.Required)
            {
                builder.Append('?');
            }

            builder.Append(": ");
            builder.Append(CedarTypeName(attribute.Type));
            builder.Append(',');
        }

        builder.Append('}');
        return builder.ToString();
    }
}

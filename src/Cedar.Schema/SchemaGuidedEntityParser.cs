using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cedar.Types;

namespace Cedar.Schema;

/// <summary>
/// Parses Cedar entity JSON using schema-guided coercion and validation rules.
/// </summary>
public static class SchemaGuidedEntityParser
{
    /// <summary>
    /// Parses an entity JSON array into an <see cref="EntityMap"/> using the supplied schema.
    /// </summary>
    /// <param name="json">The UTF-8 encoded entity JSON array.</param>
    /// <param name="schema">The schema that guides entity parsing.</param>
    /// <returns>The parsed entity map.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> or <paramref name="schema"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown when the JSON does not conform to the expected entity format or schema-guided constraints.</exception>
    public static EntityMap ParseEntityMap(byte[] json, SchemaDocument schema)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(schema);

        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Entity file must contain a JSON array.");
        }

        Dictionary<EntityUid, Entity> entities = new();
        foreach (JsonElement entityElement in document.RootElement.EnumerateArray())
        {
            Entity entity = ParseEntity(entityElement, schema);
            if (entities.TryGetValue(entity.Uid, out Entity? existing))
            {
                if (existing != entity)
                {
                    throw new InvalidDataException($"Duplicate entity '{entity.Uid}' has different content.");
                }

                continue;
            }

            entities.Add(entity.Uid, entity);
        }

        return new EntityMap(entities.Values);
    }

    private static Entity ParseEntity(JsonElement element, SchemaDocument schema)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Entity values must be JSON objects.");
        }

        EntityUid uid = ParseEntityUid(GetRequiredProperty(element, "uid", JsonValueKind.Object));

        EntityUidSet parents = element.TryGetProperty("parents", out JsonElement parentsElement)
            ? ParseParents(parentsElement)
            : new EntityUidSet();

        if (!TryLookupEntityDeclaration(schema, uid.Type.Value, out string currentNamespace, out EntityDecl? entityDecl, out bool isEnum))
        {
            throw new InvalidDataException($"Entity type '{uid.Type.Value}' not found in schema.");
        }

        if (isEnum)
        {
            if (element.TryGetProperty("attrs", out JsonElement attrsForEnum)
                && attrsForEnum.ValueKind == JsonValueKind.Object
                && attrsForEnum.EnumerateObject().Any())
            {
                throw new InvalidDataException($"Entity '{uid}': enum entities must not declare attributes.");
            }

            if (element.TryGetProperty("tags", out JsonElement tagsForEnum)
                && tagsForEnum.ValueKind == JsonValueKind.Object
                && tagsForEnum.EnumerateObject().Any())
            {
                throw new InvalidDataException($"Entity '{uid}': enum entities must not declare tags.");
            }

            return new Entity(uid, parents, new CedarRecord(), new CedarRecord());
        }

        CedarRecord attributes = ParseEntityAttributes(element, entityDecl!.Shape, schema, currentNamespace);
        CedarRecord tags = ParseEntityTags(element, entityDecl.Tags, schema, currentNamespace);

        return new Entity(uid, parents, attributes, tags);
    }

    private static CedarRecord ParseEntityAttributes(JsonElement element, RecordType? shape, SchemaDocument schema, string currentNamespace)
    {
        if (shape is null)
        {
            return element.TryGetProperty("attrs", out JsonElement attrsElement)
                ? ParseRecord(attrsElement, "attrs")
                : new CedarRecord();
        }

        if (!element.TryGetProperty("attrs", out JsonElement guidedAttrsElement) || guidedAttrsElement.ValueKind == JsonValueKind.Null)
        {
            return ParseGuidedRecord(shape, null, schema, currentNamespace, "attrs");
        }

        return ParseGuidedRecord(shape, guidedAttrsElement, schema, currentNamespace, "attrs");
    }

    private static CedarRecord ParseEntityTags(JsonElement element, SchemaType? tagsType, SchemaDocument schema, string currentNamespace)
    {
        if (!element.TryGetProperty("tags", out JsonElement tagsElement) || tagsElement.ValueKind == JsonValueKind.Null)
        {
            return new CedarRecord();
        }

        if (tagsType is null)
        {
            if (tagsElement.ValueKind == JsonValueKind.Object && tagsElement.EnumerateObject().Any())
            {
                throw new InvalidDataException("Entity tags are not allowed by schema.");
            }

            return new CedarRecord();
        }

        if (tagsElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Property 'tags' must be an object.");
        }

        RecordMap values = [];
        foreach (JsonProperty property in tagsElement.EnumerateObject())
        {
            values.Add(new CedarString(property.Name), ParseGuidedValue(property.Value, tagsType, schema, currentNamespace));
        }

        return new CedarRecord(values);
    }

    private static CedarRecord ParseGuidedRecord(RecordType shape, JsonElement? element, SchemaDocument schema, string currentNamespace, string name)
    {
        if (element.HasValue && element.Value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Property '{name}' must be an object.");
        }

        Dictionary<string, JsonElement> properties = new(StringComparer.Ordinal);
        if (element.HasValue)
        {
            foreach (JsonProperty property in element.Value.EnumerateObject())
            {
                properties.Add(property.Name, property.Value);
            }
        }

        foreach (string propertyName in properties.Keys)
        {
            if (!shape.Attributes.ContainsKey(propertyName))
            {
                throw new InvalidDataException($"Property '{name}' contains unknown attribute '{propertyName}'.");
            }
        }

        RecordMap values = [];
        foreach (KeyValuePair<string, AttributeDecl> attribute in shape.Attributes)
        {
            if (!properties.TryGetValue(attribute.Key, out JsonElement propertyValue))
            {
                if (attribute.Value.Optional)
                {
                    continue;
                }

                throw new InvalidDataException($"Property '{name}' is missing required attribute '{attribute.Key}'.");
            }

            values.Add(new CedarString(attribute.Key), ParseGuidedValue(propertyValue, attribute.Value.Type, schema, currentNamespace));
        }

        return new CedarRecord(values);
    }

    private static ICedarData ParseGuidedValue(JsonElement element, SchemaType type, SchemaDocument schema, string currentNamespace)
    {
        SchemaType normalizedType = NormalizeSchemaType(type, schema, currentNamespace, new HashSet<string>(StringComparer.Ordinal));

        return normalizedType switch
        {
            BoolType => ParseBool(element),
            LongType => ParseLongValue(element),
            StringType => ParseStringValue(element),
            EntityTypeRef => ParseEntityUid(element),
            SetType setType => ParseGuidedSet(element, setType, schema, currentNamespace),
            RecordType recordType => ParseGuidedRecord(recordType, element, schema, currentNamespace, "value"),
            ExtensionType extensionType => ParseExtensionValue(element, extensionType),
            TypeRef typeRef => ParseGuidedValue(element, typeRef, schema, currentNamespace),
            _ => throw new InvalidDataException($"Unsupported schema type '{normalizedType.GetType().Name}'.")
        };
    }

    private static CedarBool ParseBool(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => CedarBool.True,
            JsonValueKind.False => CedarBool.False,
            _ => throw new InvalidDataException($"Expected Bool value, got '{element.ValueKind}'.")
        };
    }

    private static CedarLong ParseLongValue(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidDataException($"Expected Long value, got '{element.ValueKind}'.");
        }

        return ParseLong(element);
    }

    private static CedarString ParseStringValue(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Expected String value, got '{element.ValueKind}'.");
        }

        return new CedarString(element.GetString() ?? string.Empty);
    }

    private static CedarSet ParseGuidedSet(JsonElement element, SetType setType, SchemaDocument schema, string currentNamespace)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Expected Set value, got '{element.ValueKind}'.");
        }

        List<ICedarData> values = [];
        foreach (JsonElement child in element.EnumerateArray())
        {
            values.Add(ParseGuidedValue(child, setType.Element, schema, currentNamespace));
        }

        return new CedarSet(values);
    }

    private static ICedarData ParseExtensionValue(JsonElement element, ExtensionType extensionType)
    {
        string functionName = extensionType.Name.Value switch
        {
            "ipaddr" => "ip",
            "decimal" => "decimal",
            "datetime" => "datetime",
            "duration" => "duration",
            "pattern" => "pattern",
            _ => throw new InvalidDataException($"Unsupported extension type '{extensionType.Name.Value}'.")
        };

        string argument;
        if (element.ValueKind == JsonValueKind.String)
        {
            argument = element.GetString() ?? string.Empty;
        }
        else
        {
            if (!TryGetExtensionPayload(element, out JsonElement payload))
            {
                throw new InvalidDataException($"Expected extension value for '{extensionType.Name.Value}'.");
            }

            string actualFunctionName = GetRequiredString(payload, "fn");
            if (!string.Equals(actualFunctionName, functionName, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Expected extension function '{functionName}', got '{actualFunctionName}'.");
            }

            argument = GetRequiredString(payload, "arg");
        }

        try
        {
            return functionName switch
            {
                "ip" => CedarIpAddress.Parse(argument),
                "decimal" => CedarDecimal.Parse(argument),
                "datetime" => CedarDatetime.Parse(argument),
                "duration" => CedarDuration.Parse(argument),
                "pattern" => CedarPattern.Parse(argument),
                _ => throw new InvalidDataException($"Unsupported extension function '{functionName}'.")
            };
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or FormatException or OverflowException)
        {
            throw new InvalidDataException($"Invalid value for extension type '{extensionType.Name.Value}'.", exception);
        }
    }

    private static SchemaType NormalizeSchemaType(SchemaType type, SchemaDocument schema, string currentNamespace, HashSet<string> resolvingCommonTypes)
    {
        return type switch
        {
            TypeRef typeRef => NormalizeTypeReference(typeRef.Name, schema, currentNamespace, resolvingCommonTypes),
            _ => type
        };
    }

    private static SchemaType NormalizeTypeReference(string name, SchemaDocument schema, string currentNamespace, HashSet<string> resolvingCommonTypes)
    {
        return name switch
        {
            "String" or "__cedar::String" => new StringType(),
            "Long" or "__cedar::Long" => new LongType(),
            "Bool" or "Boolean" or "__cedar::Bool" or "__cedar::Boolean" => new BoolType(),
            "ipaddr" => new ExtensionType(new Ident("ipaddr")),
            "decimal" => new ExtensionType(new Ident("decimal")),
            "datetime" => new ExtensionType(new Ident("datetime")),
            "duration" => new ExtensionType(new Ident("duration")),
            "pattern" => new ExtensionType(new Ident("pattern")),
            _ => NormalizeUserDefinedTypeReference(name, schema, currentNamespace, resolvingCommonTypes)
        };
    }

    private static SchemaType NormalizeUserDefinedTypeReference(string name, SchemaDocument schema, string currentNamespace, HashSet<string> resolvingCommonTypes)
    {
        if (TryLookupEntityType(schema, currentNamespace, name, out EntityType entityType))
        {
            return new EntityTypeRef(entityType);
        }

        if (TryLookupCommonType(schema, currentNamespace, name, out string commonTypeNamespace, out string qualifiedCommonTypeName, out CommonTypeDecl? commonType))
        {
            if (!resolvingCommonTypes.Add(qualifiedCommonTypeName))
            {
                throw new InvalidDataException($"Common type cycle detected for '{qualifiedCommonTypeName}'.");
            }

            try
            {
                return NormalizeSchemaType(commonType!.Type, schema, commonTypeNamespace, resolvingCommonTypes);
            }
            finally
            {
                resolvingCommonTypes.Remove(qualifiedCommonTypeName);
            }
        }

        throw new InvalidDataException($"Unknown schema type '{name}'.");
    }

    private static bool TryLookupEntityDeclaration(SchemaDocument schema, string entityTypeName, out string currentNamespace, out EntityDecl? entityDecl, out bool isEnum)
    {
        SplitQualifiedTypeName(entityTypeName, out string namespaceName, out string localName);
        currentNamespace = namespaceName;
        entityDecl = null;
        isEnum = false;

        if (!TryGetNamespaceDeclaration(schema, namespaceName, out NamespaceDecl? declaration))
        {
            return false;
        }

        if (declaration!.Entities.TryGetValue(new Ident(localName), out EntityDecl? foundEntity))
        {
            entityDecl = foundEntity;
            return true;
        }

        if (declaration.Enums.ContainsKey(new Ident(localName)))
        {
            isEnum = true;
            return true;
        }

        return false;
    }

    private static bool TryLookupEntityType(SchemaDocument schema, string currentNamespace, string typeName, out EntityType entityType)
    {
        if (TryLookupNamedType(schema, currentNamespace, typeName, static declaration => declaration.Entities.ContainsKey, static declaration => declaration.Enums.ContainsKey, out string namespaceName, out string localName))
        {
            entityType = new EntityType(QualifyTypeName(namespaceName, localName));
            return true;
        }

        entityType = default;
        return false;
    }

    private static bool TryLookupCommonType(SchemaDocument schema, string currentNamespace, string typeName, out string namespaceName, out string qualifiedTypeName, out CommonTypeDecl? commonType)
    {
        if (typeName.Contains("::", StringComparison.Ordinal))
        {
            SplitQualifiedTypeName(typeName, out namespaceName, out string localName);
            qualifiedTypeName = QualifyTypeName(namespaceName, localName);
            if (TryGetNamespaceDeclaration(schema, namespaceName, out NamespaceDecl? declaration)
                && declaration!.CommonTypes.TryGetValue(new Ident(localName), out CommonTypeDecl? explicitCommonType))
            {
                commonType = explicitCommonType;
                return true;
            }

            commonType = null;
            return false;
        }

        if (TryGetNamespaceDeclaration(schema, currentNamespace, out NamespaceDecl? currentDeclaration)
            && currentDeclaration!.CommonTypes.TryGetValue(new Ident(typeName), out CommonTypeDecl? currentCommonType))
        {
            namespaceName = currentNamespace;
            qualifiedTypeName = QualifyTypeName(namespaceName, typeName);
            commonType = currentCommonType;
            return true;
        }

        if (schema.GlobalNamespace.CommonTypes.TryGetValue(new Ident(typeName), out CommonTypeDecl? globalCommonType))
        {
            namespaceName = string.Empty;
            qualifiedTypeName = typeName;
            commonType = globalCommonType;
            return true;
        }

        namespaceName = string.Empty;
        qualifiedTypeName = string.Empty;
        commonType = null;
        return false;
    }

    private static bool TryLookupNamedType(
        SchemaDocument schema,
        string currentNamespace,
        string typeName,
        Func<NamespaceDecl, Func<Ident, bool>> entityLookupFactory,
        Func<NamespaceDecl, Func<Ident, bool>> enumLookupFactory,
        out string namespaceName,
        out string localName)
    {
        if (typeName.Contains("::", StringComparison.Ordinal))
        {
            SplitQualifiedTypeName(typeName, out namespaceName, out localName);
            if (TryGetNamespaceDeclaration(schema, namespaceName, out NamespaceDecl? explicitDeclaration)
                && explicitDeclaration is not null)
            {
                Ident ident = new(localName);
                if (entityLookupFactory(explicitDeclaration)(ident) || enumLookupFactory(explicitDeclaration)(ident))
                {
                    return true;
                }
            }

            namespaceName = string.Empty;
            localName = string.Empty;
            return false;
        }

        if (TryGetNamespaceDeclaration(schema, currentNamespace, out NamespaceDecl? currentDeclaration)
            && currentDeclaration is not null)
        {
            Ident ident = new(typeName);
            if (entityLookupFactory(currentDeclaration)(ident) || enumLookupFactory(currentDeclaration)(ident))
            {
                namespaceName = currentNamespace;
                localName = typeName;
                return true;
            }
        }

        if (schema.GlobalNamespace.Entities.ContainsKey(new Ident(typeName)) || schema.GlobalNamespace.Enums.ContainsKey(new Ident(typeName)))
        {
            namespaceName = string.Empty;
            localName = typeName;
            return true;
        }

        namespaceName = string.Empty;
        localName = string.Empty;
        return false;
    }

    private static bool TryGetNamespaceDeclaration(SchemaDocument schema, string namespaceName, out NamespaceDecl? declaration)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            declaration = schema.GlobalNamespace;
            return true;
        }

        return schema.Namespaces.TryGetValue(namespaceName, out declaration);
    }

    private static void SplitQualifiedTypeName(string typeName, out string namespaceName, out string localName)
    {
        int separatorIndex = typeName.LastIndexOf("::", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            namespaceName = string.Empty;
            localName = typeName;
            return;
        }

        namespaceName = typeName[..separatorIndex];
        localName = typeName[(separatorIndex + 2)..];
    }

    private static string QualifyTypeName(string namespaceName, string localName)
    {
        return string.IsNullOrEmpty(namespaceName)
            ? localName
            : namespaceName + "::" + localName;
    }

    private static CedarRecord ParseRecord(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return new CedarRecord();
        }

        ICedarData value = ParseCedarValue(element);
        return value as CedarRecord ?? throw new InvalidDataException($"Property '{name}' must be an object.");
    }

    private static ICedarData ParseCedarValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => CedarBool.True,
            JsonValueKind.False => CedarBool.False,
            JsonValueKind.Number => ParseLong(element),
            JsonValueKind.String => new CedarString(element.GetString() ?? string.Empty),
            JsonValueKind.Array => ParseSet(element),
            JsonValueKind.Object => ParseObject(element),
            _ => throw new InvalidDataException($"Unsupported JSON token '{element.ValueKind}' in Cedar value.")
        };
    }

    private static CedarLong ParseLong(JsonElement element)
    {
        if (!element.TryGetInt64(out long value))
        {
            throw new InvalidDataException("Numeric values must fit in signed 64-bit range.");
        }

        return new CedarLong(value);
    }

    private static CedarSet ParseSet(JsonElement element)
    {
        List<ICedarData> values = [];
        foreach (JsonElement child in element.EnumerateArray())
        {
            values.Add(ParseCedarValue(child));
        }

        return new CedarSet(values);
    }

    private static ICedarData ParseObject(JsonElement element)
    {
        if (TryParseEntityUid(element, out EntityUid? uid))
        {
            return uid!;
        }

        if (TryParseExtension(element, out ICedarData? extensionValue))
        {
            return extensionValue!;
        }

        RecordMap values = [];
        foreach (JsonProperty property in element.EnumerateObject())
        {
            values.Add(new CedarString(property.Name), ParseCedarValue(property.Value));
        }

        return new CedarRecord(values);
    }

    private static EntityUidSet ParseParents(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Entity parents must be an array.");
        }

        List<EntityUid> values = [];
        foreach (JsonElement parent in element.EnumerateArray())
        {
            values.Add(ParseEntityUid(parent));
        }

        return new EntityUidSet(values);
    }

    private static EntityUid ParseEntityUid(JsonElement element)
    {
        if (!TryParseEntityUid(element, out EntityUid? uid))
        {
            throw new InvalidDataException("Expected entity uid object in {type,id} or {__entity:{type,id}} format.");
        }

        return uid!;
    }

    private static bool TryParseEntityUid(JsonElement element, out EntityUid? uid)
    {
        uid = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        JsonElement payload = element;
        if (element.TryGetProperty("__entity", out JsonElement explicitEntity))
        {
            if (explicitEntity.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            payload = explicitEntity;
        }

        if (!payload.TryGetProperty("type", out JsonElement typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (!payload.TryGetProperty("id", out JsonElement idElement) || idElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string type = typeElement.GetString() ?? string.Empty;
        string id = idElement.GetString() ?? string.Empty;
        uid = new EntityUid(new EntityType(type), new CedarString(id));
        return true;
    }

    private static bool TryParseExtension(JsonElement element, out ICedarData? value)
    {
        value = null;
        if (!TryGetExtensionPayload(element, out JsonElement payload))
        {
            return false;
        }

        string fn = GetRequiredString(payload, "fn");
        string arg = GetRequiredString(payload, "arg");

        try
        {
            value = fn switch
            {
                "decimal" => CedarDecimal.Parse(arg),
                "datetime" => CedarDatetime.Parse(arg),
                "duration" => CedarDuration.Parse(arg),
                "ip" => CedarIpAddress.Parse(arg),
                "pattern" => CedarPattern.Parse(arg),
                _ => null
            };
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or FormatException or OverflowException)
        {
            value = null;
            return false;
        }

        return value is not null;
    }

    private static bool TryGetExtensionPayload(JsonElement element, out JsonElement payload)
    {
        if (element.TryGetProperty("__extn", out JsonElement explicitExtension))
        {
            if (explicitExtension.ValueKind == JsonValueKind.Object
                && explicitExtension.TryGetProperty("fn", out JsonElement explicitFn)
                && explicitFn.ValueKind == JsonValueKind.String
                && explicitExtension.TryGetProperty("arg", out JsonElement explicitArg)
                && explicitArg.ValueKind == JsonValueKind.String)
            {
                payload = explicitExtension;
                return true;
            }

            payload = default;
            return false;
        }

        if (element.TryGetProperty("fn", out JsonElement functionElement)
            && functionElement.ValueKind == JsonValueKind.String
            && element.TryGetProperty("arg", out JsonElement argumentElement)
            && argumentElement.ValueKind == JsonValueKind.String)
        {
            payload = element;
            return true;
        }

        payload = default;
        return false;
    }

    private static JsonElement GetRequiredProperty(JsonElement objectElement, string propertyName, JsonValueKind expectedKind)
    {
        if (!objectElement.TryGetProperty(propertyName, out JsonElement value))
        {
            throw new InvalidDataException($"Missing required property '{propertyName}'.");
        }

        if (value.ValueKind != expectedKind)
        {
            throw new InvalidDataException($"Property '{propertyName}' must be '{expectedKind}', got '{value.ValueKind}'.");
        }

        return value;
    }

    private static string GetRequiredString(JsonElement objectElement, string propertyName)
    {
        JsonElement value = GetRequiredProperty(objectElement, propertyName, JsonValueKind.String);
        return value.GetString() ?? string.Empty;
    }
}
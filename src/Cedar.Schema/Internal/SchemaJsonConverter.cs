using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cedar.Schema;
using Cedar.Types;

namespace Cedar.Schema.Internal;

internal static class SchemaJsonConverter
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(SchemaDocument schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        SortedDictionary<string, JsonNamespaceModel> root = new(StringComparer.Ordinal);
        if (HasBareDeclarations(schema.GlobalNamespace))
        {
            root.Add(string.Empty, ToJsonNamespace(schema.GlobalNamespace));
        }

        List<string> names = new(schema.Namespaces.Keys);
        names.Sort(StringComparer.Ordinal);
        for (int index = 0; index < names.Count; index++)
        {
            string name = names[index];
            root.Add(name, ToJsonNamespace(schema.Namespaces[name]));
        }

        return JsonSerializer.Serialize(root, Options);
    }

    public static SchemaDocument Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        Dictionary<string, JsonNamespaceModel> root = JsonSerializer.Deserialize<Dictionary<string, JsonNamespaceModel>>(json, Options)
            ?? throw new JsonException("Expected a JSON object.");

        NamespaceDecl globalNamespace = new();
        Dictionary<string, NamespaceDecl> namespaces = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonNamespaceModel> pair in root)
        {
            NamespaceDecl declaration = FromJsonNamespace(pair.Key, pair.Value);
            if (pair.Key.Length == 0)
            {
                globalNamespace = declaration;
            }
            else
            {
                namespaces.Add(pair.Key, declaration);
            }
        }

        return new SchemaDocument
        {
            GlobalNamespace = globalNamespace,
            Namespaces = namespaces
        };
    }

    private static JsonNamespaceModel ToJsonNamespace(NamespaceDecl declaration)
    {
        JsonNamespaceModel model = new();

        if (declaration.Annotations.Count > 0)
        {
            model.Annotations = ToJsonAnnotations(declaration.Annotations);
        }

        if (declaration.CommonTypes.Count > 0)
        {
            model.CommonTypes = new SortedDictionary<string, JsonCommonTypeModel>(StringComparer.Ordinal);
            foreach (Ident name in SortedIdents(declaration.CommonTypes.Keys))
            {
                CommonTypeDecl commonType = declaration.CommonTypes[name];
                JsonCommonTypeModel jsonType = ToJsonType(commonType.Type, static jt => new JsonCommonTypeModel
                {
                    Type = jt.Type,
                    Element = jt.Element,
                    Attributes = jt.Attributes,
                    Name = jt.Name
                });

                if (commonType.Annotations.Count > 0)
                {
                    jsonType.Annotations = ToJsonAnnotations(commonType.Annotations);
                }

                model.CommonTypes.Add(name.Value, jsonType);
            }
        }

        foreach (Ident name in SortedIdents(declaration.Entities.Keys))
        {
            EntityDecl entity = declaration.Entities[name];
            JsonEntityTypeModel jsonEntity = new();

            if (entity.Annotations.Count > 0)
            {
                jsonEntity.Annotations = ToJsonAnnotations(entity.Annotations);
            }

            if (entity.ParentTypes.Count > 0)
            {
                jsonEntity.MemberOfTypes = [];
                for (int index = 0; index < entity.ParentTypes.Count; index++)
                {
                    jsonEntity.MemberOfTypes.Add(entity.ParentTypes[index].Value);
                }
            }

            if (entity.Shape is not null)
            {
                jsonEntity.Shape = ToJsonRecordType(entity.Shape);
            }

            if (entity.Tags is not null)
            {
                jsonEntity.Tags = ToJsonType(entity.Tags);
            }

            model.EntityTypes.Add(name.Value, jsonEntity);
        }

        foreach (Ident name in SortedIdents(declaration.Enums.Keys))
        {
            EnumDecl enumDecl = declaration.Enums[name];
            JsonEntityTypeModel jsonEnum = new()
            {
                Enum = new List<string>(enumDecl.Values)
            };

            if (enumDecl.Annotations.Count > 0)
            {
                jsonEnum.Annotations = ToJsonAnnotations(enumDecl.Annotations);
            }

            model.EntityTypes.Add(name.Value, jsonEnum);
        }

        List<string> actionNames = new(declaration.Actions.Keys);
        actionNames.Sort(StringComparer.Ordinal);
        for (int index = 0; index < actionNames.Count; index++)
        {
            string name = actionNames[index];
            ActionDecl action = declaration.Actions[name];
            JsonActionModel jsonAction = new();

            if (action.Annotations.Count > 0)
            {
                jsonAction.Annotations = ToJsonAnnotations(action.Annotations);
            }

            if (action.Parents.Count > 0)
            {
                jsonAction.MemberOf = [];
                for (int parentIndex = 0; parentIndex < action.Parents.Count; parentIndex++)
                {
                    ParentRef parent = action.Parents[parentIndex];
                    jsonAction.MemberOf.Add(new JsonActionParentModel
                    {
                        Id = parent.Id,
                        Type = parent.Type?.Value
                    });
                }
            }

            if (action.AppliesTo is not null)
            {
                JsonAppliesToModel appliesTo = new();
                for (int entityIndex = 0; entityIndex < action.AppliesTo.Principals.Count; entityIndex++)
                {
                    appliesTo.PrincipalTypes.Add(action.AppliesTo.Principals[entityIndex].Value);
                }

                for (int entityIndex = 0; entityIndex < action.AppliesTo.Resources.Count; entityIndex++)
                {
                    appliesTo.ResourceTypes.Add(action.AppliesTo.Resources[entityIndex].Value);
                }

                if (action.AppliesTo.Context is not null)
                {
                    appliesTo.Context = ToJsonType(action.AppliesTo.Context);
                }

                jsonAction.AppliesTo = appliesTo;
            }

            model.Actions.Add(name, jsonAction);
        }

        return model;
    }

    private static NamespaceDecl FromJsonNamespace(string namespaceName, JsonNamespaceModel model)
    {
        Dictionary<Ident, CommonTypeDecl> commonTypes = [];
        if (model.CommonTypes is not null)
        {
            foreach (KeyValuePair<string, JsonCommonTypeModel> pair in model.CommonTypes)
            {
                commonTypes.Add(new Ident(pair.Key), new CommonTypeDecl
                {
                    Type = FromJsonType(pair.Value),
                    Annotations = FromJsonAnnotations(pair.Value.Annotations)
                });
            }
        }

        Dictionary<Ident, EntityDecl> entities = [];
        Dictionary<Ident, EnumDecl> enums = [];
        foreach (KeyValuePair<string, JsonEntityTypeModel> pair in model.EntityTypes)
        {
            if (pair.Value.Enum is { Count: > 0 })
            {
                enums.Add(new Ident(pair.Key), new EnumDecl
                {
                    Annotations = FromJsonAnnotations(pair.Value.Annotations),
                    Values = pair.Value.Enum
                });
                continue;
            }

            List<EntityType> parentTypes = [];
            if (pair.Value.MemberOfTypes is not null)
            {
                for (int index = 0; index < pair.Value.MemberOfTypes.Count; index++)
                {
                    parentTypes.Add(new EntityType(pair.Value.MemberOfTypes[index]));
                }
            }

            entities.Add(new Ident(pair.Key), new EntityDecl
            {
                Annotations = FromJsonAnnotations(pair.Value.Annotations),
                ParentTypes = parentTypes,
                Shape = pair.Value.Shape is null ? null : FromJsonRecordType($"{namespaceName}:entity {pair.Key} shape", pair.Value.Shape),
                Tags = pair.Value.Tags is null ? null : FromJsonType(pair.Value.Tags)
            });
        }

        Dictionary<string, ActionDecl> actions = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonActionModel> pair in model.Actions)
        {
            List<ParentRef> parents = [];
            if (pair.Value.MemberOf is not null)
            {
                for (int index = 0; index < pair.Value.MemberOf.Count; index++)
                {
                    JsonActionParentModel parent = pair.Value.MemberOf[index];
                    parents.Add(new ParentRef(parent.Type is null ? null : new EntityType(parent.Type), parent.Id));
                }
            }

            AppliesToDecl? appliesTo = null;
            if (pair.Value.AppliesTo is not null)
            {
                List<EntityType> principals = [];
                for (int index = 0; index < pair.Value.AppliesTo.PrincipalTypes.Count; index++)
                {
                    principals.Add(new EntityType(pair.Value.AppliesTo.PrincipalTypes[index]));
                }

                List<EntityType> resources = [];
                for (int index = 0; index < pair.Value.AppliesTo.ResourceTypes.Count; index++)
                {
                    resources.Add(new EntityType(pair.Value.AppliesTo.ResourceTypes[index]));
                }

                appliesTo = new AppliesToDecl
                {
                    Principals = principals,
                    Resources = resources,
                    Context = pair.Value.AppliesTo.Context is null ? null : FromJsonType(pair.Value.AppliesTo.Context)
                };
            }

            actions.Add(pair.Key, new ActionDecl
            {
                Annotations = FromJsonAnnotations(pair.Value.Annotations),
                Parents = parents,
                AppliesTo = appliesTo
            });
        }

        return new NamespaceDecl
        {
            Annotations = FromJsonAnnotations(model.Annotations),
            Entities = entities,
            Enums = enums,
            Actions = actions,
            CommonTypes = commonTypes
        };
    }

    private static JsonTypeModel ToJsonType(SchemaType type)
    {
        return ToJsonType(type, static value => value);
    }

    private static T ToJsonType<T>(SchemaType type, Func<JsonTypeModel, T> projector)
        where T : JsonTypeModel
    {
        JsonTypeModel model = type switch
        {
            StringType => new JsonTypeModel { Type = "String" },
            LongType => new JsonTypeModel { Type = "Long" },
            BoolType => new JsonTypeModel { Type = "Boolean" },
            ExtensionType extension => new JsonTypeModel { Type = "Extension", Name = extension.Name.Value },
            SetType setType => new JsonTypeModel { Type = "Set", Element = ToJsonType(setType.Element) },
            RecordType recordType => ToJsonRecordType(recordType),
            EntityTypeRef entityTypeRef => new JsonTypeModel { Type = "Entity", Name = entityTypeRef.Name.Value },
            TypeRef typeRef => new JsonTypeModel { Type = "EntityOrCommon", Name = typeRef.Name },
            _ => throw new InvalidOperationException($"Unknown schema type: {type.GetType().FullName}")
        };

        return projector(model);
    }

    private static JsonTypeModel ToJsonRecordType(RecordType recordType)
    {
        JsonTypeModel model = new()
        {
            Type = "Record",
            Attributes = new SortedDictionary<string, JsonAttributeModel>(StringComparer.Ordinal)
        };

        List<string> names = new(recordType.Attributes.Keys);
        names.Sort(StringComparer.Ordinal);
        for (int index = 0; index < names.Count; index++)
        {
            string name = names[index];
            AttributeDecl attribute = recordType.Attributes[name];
            JsonAttributeModel jsonAttribute = ToJsonType(attribute.Type, static jt => new JsonAttributeModel
            {
                Type = jt.Type,
                Element = jt.Element,
                Attributes = jt.Attributes,
                Name = jt.Name
            });

            if (attribute.Optional)
            {
                jsonAttribute.Required = false;
            }

            if (attribute.Annotations.Count > 0)
            {
                jsonAttribute.Annotations = ToJsonAnnotations(attribute.Annotations);
            }

            model.Attributes.Add(name, jsonAttribute);
        }

        return model;
    }

    private static SchemaType FromJsonType(JsonTypeModel model)
    {
        return model.Type switch
        {
            "String" => new StringType(),
            "Long" => new LongType(),
            "Boolean" => new BoolType(),
            "Extension" => new ExtensionType(new Ident(model.Name ?? throw new JsonException("Extension type missing name."))),
            "Set" => new SetType(FromJsonType(model.Element ?? throw new JsonException("Set type missing element."))),
            "Record" => FromJsonRecordType("record", model),
            "Entity" => new EntityTypeRef(new EntityType(model.Name ?? throw new JsonException("Entity type missing name."))),
            "EntityOrCommon" => new TypeRef(model.Name ?? throw new JsonException("EntityOrCommon type missing name.")),
            _ => throw new JsonException($"Unknown schema type \"{model.Type}\".")
        };
    }

    private static RecordType FromJsonRecordType(string context, JsonTypeModel model)
    {
        Dictionary<string, AttributeDecl> attributes = new(StringComparer.Ordinal);
        if (model.Attributes is not null)
        {
            foreach (KeyValuePair<string, JsonAttributeModel> pair in model.Attributes)
            {
                JsonTypeModel attributeType = new()
                {
                    Type = pair.Value.Type,
                    Element = pair.Value.Element,
                    Attributes = pair.Value.Attributes,
                    Name = pair.Value.Name
                };

                attributes.Add(pair.Key, new AttributeDecl
                {
                    Type = FromJsonType(attributeType),
                    Optional = pair.Value.Required.HasValue && !pair.Value.Required.Value,
                    Annotations = FromJsonAnnotations(pair.Value.Annotations)
                });
            }
        }

        return new RecordType
        {
            Attributes = attributes
        };
    }

    private static SortedDictionary<string, string> ToJsonAnnotations(IReadOnlyList<SchemaAnnotation> annotations)
    {
        SortedDictionary<string, string> result = new(StringComparer.Ordinal);
        for (int index = 0; index < annotations.Count; index++)
        {
            SchemaAnnotation annotation = annotations[index];
            result.Add(annotation.Key.Value, annotation.Value);
        }

        return result;
    }

    private static IReadOnlyList<SchemaAnnotation> FromJsonAnnotations(IReadOnlyDictionary<string, string>? annotations)
    {
        if (annotations is null || annotations.Count == 0)
        {
            return Array.Empty<SchemaAnnotation>();
        }

        List<string> names = new(annotations.Keys);
        names.Sort(StringComparer.Ordinal);

        List<SchemaAnnotation> result = new(names.Count);
        for (int index = 0; index < names.Count; index++)
        {
            string name = names[index];
            result.Add(new SchemaAnnotation(new Ident(name), annotations[name]));
        }

        return result;
    }

    private static bool HasBareDeclarations(NamespaceDecl declaration)
    {
        return declaration.Entities.Count > 0 || declaration.Enums.Count > 0 || declaration.Actions.Count > 0 || declaration.CommonTypes.Count > 0;
    }

    private static List<Ident> SortedIdents(IEnumerable<Ident> values)
    {
        List<Ident> result = new(values);
        result.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value));
        return result;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    private class JsonNamespaceModel
    {
        [JsonPropertyName("entityTypes")]
        public SortedDictionary<string, JsonEntityTypeModel> EntityTypes { get; set; } = new(StringComparer.Ordinal);

        [JsonPropertyName("actions")]
        public SortedDictionary<string, JsonActionModel> Actions { get; set; } = new(StringComparer.Ordinal);

        [JsonPropertyName("commonTypes")]
        public SortedDictionary<string, JsonCommonTypeModel>? CommonTypes { get; set; }

        [JsonPropertyName("annotations")]
        public SortedDictionary<string, string>? Annotations { get; set; }
    }

    private class JsonEntityTypeModel
    {
        [JsonPropertyName("memberOfTypes")]
        public List<string>? MemberOfTypes { get; set; }

        [JsonPropertyName("shape")]
        public JsonTypeModel? Shape { get; set; }

        [JsonPropertyName("tags")]
        public JsonTypeModel? Tags { get; set; }

        [JsonPropertyName("annotations")]
        public SortedDictionary<string, string>? Annotations { get; set; }

        [JsonPropertyName("enum")]
        public List<string>? Enum { get; set; }
    }

    private class JsonActionModel
    {
        [JsonPropertyName("memberOf")]
        public List<JsonActionParentModel>? MemberOf { get; set; }

        [JsonPropertyName("appliesTo")]
        public JsonAppliesToModel? AppliesTo { get; set; }

        [JsonPropertyName("annotations")]
        public SortedDictionary<string, string>? Annotations { get; set; }
    }

    private class JsonActionParentModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    private class JsonAppliesToModel
    {
        [JsonPropertyName("principalTypes")]
        public List<string> PrincipalTypes { get; set; } = [];

        [JsonPropertyName("resourceTypes")]
        public List<string> ResourceTypes { get; set; } = [];

        [JsonPropertyName("context")]
        public JsonTypeModel? Context { get; set; }
    }

    private class JsonTypeModel
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("element")]
        public JsonTypeModel? Element { get; set; }

        [JsonPropertyName("attributes")]
        public SortedDictionary<string, JsonAttributeModel>? Attributes { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class JsonCommonTypeModel : JsonTypeModel
    {
        [JsonPropertyName("annotations")]
        public SortedDictionary<string, string>? Annotations { get; set; }
    }

    private sealed class JsonAttributeModel : JsonTypeModel
    {
        [JsonPropertyName("required")]
        public bool? Required { get; set; }

        [JsonPropertyName("annotations")]
        public SortedDictionary<string, string>? Annotations { get; set; }
    }
}

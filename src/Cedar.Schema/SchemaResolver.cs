using System;
using System.Collections.Generic;
using System.Linq;
using Cedar.Types;

namespace Cedar.Schema;

public static class SchemaResolver
{
    public static ResolvedSchema Resolve(SchemaDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        ResolverState state = new();
        state.RegisterDeclarations(document);
        state.CheckShadowing(document);
        state.DetectCommonTypeCycles();
        state.ResolveAllDeclarations(document);
        state.ValidateActionMembership();
        return state.BuildResult();
    }

    private sealed class ResolverState
    {
        private readonly HashSet<EntityType> _entityTypes = [];
        private readonly HashSet<EntityType> _enumTypes = [];
        private readonly Dictionary<string, SchemaType> _commonTypes = new(StringComparer.Ordinal);
        private readonly Dictionary<EntityUid, ResolvedAction> _actions = [];
        private readonly Dictionary<EntityType, ResolvedEntity> _entities = [];
        private readonly Dictionary<EntityType, ResolvedEnum> _enums = [];
        private readonly Dictionary<string, ResolvedNamespace> _namespaces = new(StringComparer.Ordinal);

        public void RegisterDeclarations(SchemaDocument document)
        {
            RegisterDeclarations(string.Empty, document.GlobalNamespace);

            foreach ((string namespaceName, NamespaceDecl declaration) in document.Namespaces)
            {
                RegisterDeclarations(namespaceName, declaration);
            }
        }

        public void CheckShadowing(SchemaDocument document)
        {
            HashSet<string> bareTypes = new(StringComparer.Ordinal);

            foreach (Ident name in document.GlobalNamespace.Entities.Keys)
            {
                bareTypes.Add(name.Value);
            }

            foreach (Ident name in document.GlobalNamespace.Enums.Keys)
            {
                bareTypes.Add(name.Value);
            }

            foreach (Ident name in document.GlobalNamespace.CommonTypes.Keys)
            {
                bareTypes.Add(name.Value);
            }

            foreach ((string namespaceName, NamespaceDecl declaration) in document.Namespaces)
            {
                foreach (Ident name in declaration.Entities.Keys)
                {
                    CheckTypeShadowing(namespaceName, name.Value, bareTypes);
                }

                foreach (Ident name in declaration.Enums.Keys)
                {
                    CheckTypeShadowing(namespaceName, name.Value, bareTypes);
                }

                foreach (Ident name in declaration.CommonTypes.Keys)
                {
                    CheckTypeShadowing(namespaceName, name.Value, bareTypes);
                }
            }

            HashSet<string> bareActions = new(document.GlobalNamespace.Actions.Keys, StringComparer.Ordinal);
            foreach ((string namespaceName, NamespaceDecl declaration) in document.Namespaces)
            {
                foreach (string actionName in declaration.Actions.Keys)
                {
                    if (bareActions.Contains(actionName))
                    {
                        throw new InvalidOperationException(
                            $"definition of \"{namespaceName}::Action::\\\"{actionName}\\\"\" illegally shadows the existing definition of \"Action::\\\"{actionName}\\\"\"");
                    }
                }
            }
        }

        public void DetectCommonTypeCycles()
        {
            Dictionary<string, List<string>> dependencies = new(StringComparer.Ordinal);
            foreach ((string name, SchemaType schemaType) in _commonTypes)
            {
                string namespaceName = ExtractNamespace(name);
                List<string> refs = [];
                foreach (TypeRef typeRef in CollectTypeRefs(schemaType))
                {
                    string resolved = ResolveTypeRefPath(namespaceName, typeRef);
                    if (_commonTypes.ContainsKey(resolved))
                    {
                        refs.Add(resolved);
                    }
                }

                dependencies[name] = refs;
            }

            Dictionary<string, int> inDegree = new(StringComparer.Ordinal);
            foreach (string name in _commonTypes.Keys)
            {
                inDegree[name] = 0;
            }

            foreach (List<string> refs in dependencies.Values)
            {
                foreach (string reference in refs)
                {
                    inDegree[reference]++;
                }
            }

            Queue<string> queue = new(inDegree.Where(static pair => pair.Value == 0).Select(static pair => pair.Key));
            int visited = 0;

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                visited++;

                foreach (string neighbor in dependencies[current])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (visited == _commonTypes.Count)
            {
                return;
            }

            foreach ((string name, int degree) in inDegree)
            {
                if (degree > 0)
                {
                    throw new InvalidOperationException($"cycle detected in common type definitions involving \"{name}\"");
                }
            }
        }

        public void ResolveAllDeclarations(SchemaDocument document)
        {
            ResolveNamespace(string.Empty, document.GlobalNamespace);

            foreach ((string namespaceName, NamespaceDecl declaration) in document.Namespaces)
            {
                _namespaces[namespaceName] = new ResolvedNamespace(namespaceName)
                {
                    Annotations = declaration.Annotations
                };

                ResolveNamespace(namespaceName, declaration);
            }
        }

        public void ValidateActionMembership()
        {
            foreach ((EntityUid uid, ResolvedAction action) in _actions)
            {
                foreach (EntityUid parent in action.Entity.Parents)
                {
                    if (!_actions.ContainsKey(parent))
                    {
                        throw new InvalidOperationException($"action {uid}: undefined parent action {parent}");
                    }
                }
            }

            Dictionary<EntityUid, int> visited = [];
            foreach (EntityUid uid in _actions.Keys)
            {
                Visit(uid);
            }

            void Visit(EntityUid uid)
            {
                if (visited.TryGetValue(uid, out int state))
                {
                    if (state == 1)
                    {
                        throw new InvalidOperationException($"cycle detected in action hierarchy involving {uid}");
                    }

                    if (state == 2)
                    {
                        return;
                    }
                }

                visited[uid] = 1;
                foreach (EntityUid parent in _actions[uid].Entity.Parents)
                {
                    Visit(parent);
                }

                visited[uid] = 2;
            }
        }

        public ResolvedSchema BuildResult()
        {
            return new ResolvedSchema
            {
                Actions = _actions,
                Entities = _entities,
                Enums = _enums,
                Namespaces = _namespaces
            };
        }

        private void RegisterDeclarations(string namespaceName, NamespaceDecl declaration)
        {
            foreach (Ident name in declaration.Entities.Keys)
            {
                if (declaration.Enums.ContainsKey(name))
                {
                    throw new InvalidOperationException($"\"{QualifyEntityType(namespaceName, name.Value)}\" is declared twice");
                }

                _entityTypes.Add(new EntityType(QualifyEntityType(namespaceName, name.Value)));
            }

            foreach (Ident name in declaration.Enums.Keys)
            {
                _enumTypes.Add(new EntityType(QualifyEntityType(namespaceName, name.Value)));
            }

            foreach ((Ident name, CommonTypeDecl commonType) in declaration.CommonTypes)
            {
                _commonTypes[QualifyPath(namespaceName, name.Value)] = commonType.Type;
            }
        }

        private void ResolveNamespace(string namespaceName, NamespaceDecl declaration)
        {
            ResolveEntities(namespaceName, declaration.Entities);
            ResolveEnums(namespaceName, declaration.Enums);
            ResolveActions(namespaceName, declaration.Actions);
        }

        private void ResolveEntities(string namespaceName, IReadOnlyDictionary<Ident, EntityDecl> entities)
        {
            foreach ((Ident name, EntityDecl entity) in entities)
            {
                EntityType qualifiedName = new(QualifyEntityType(namespaceName, name.Value));
                List<EntityType> parents = [];

                foreach (EntityType parentType in entity.ParentTypes)
                {
                    try
                    {
                        parents.Add(ResolveEntityTypeRef(namespaceName, parentType));
                    }
                    catch (InvalidOperationException exception)
                    {
                        throw new InvalidOperationException($"entity \"{qualifiedName}\": {exception.Message}", exception);
                    }
                }

                ResolvedRecordType shape = new();
                if (entity.Shape is not null)
                {
                    try
                    {
                        shape = ResolveRecordType(namespaceName, entity.Shape);
                    }
                    catch (InvalidOperationException exception)
                    {
                        throw new InvalidOperationException($"entity \"{qualifiedName}\" shape: {exception.Message}", exception);
                    }
                }

                ResolvedType? tags = null;
                if (entity.Tags is not null)
                {
                    try
                    {
                        tags = ResolveType(namespaceName, entity.Tags);
                    }
                    catch (InvalidOperationException exception)
                    {
                        throw new InvalidOperationException($"entity \"{qualifiedName}\" tags: {exception.Message}", exception);
                    }
                }

                _entities[qualifiedName] = new ResolvedEntity
                {
                    Name = qualifiedName,
                    Annotations = entity.Annotations,
                    ParentTypes = parents,
                    Shape = shape,
                    Tags = tags
                };
            }
        }

        private void ResolveEnums(string namespaceName, IReadOnlyDictionary<Ident, EnumDecl> enums)
        {
            foreach ((Ident name, EnumDecl @enum) in enums)
            {
                EntityType qualifiedName = new(QualifyEntityType(namespaceName, name.Value));
                List<EntityUid> values = [];

                foreach (string value in @enum.Values)
                {
                    values.Add(new EntityUid(qualifiedName, new CedarString(value)));
                }

                _enums[qualifiedName] = new ResolvedEnum
                {
                    Name = qualifiedName,
                    Annotations = @enum.Annotations,
                    Values = values
                };
            }
        }

        private void ResolveActions(string namespaceName, IReadOnlyDictionary<string, ActionDecl> actions)
        {
            EntityType actionType = new(QualifyActionType(namespaceName));

            foreach ((string name, ActionDecl action) in actions)
            {
                EntityUid uid = new(actionType, new CedarString(name));
                List<EntityUid> parents = [];
                foreach (ParentRef parentRef in action.Parents)
                {
                    parents.Add(ResolveActionParentRef(namespaceName, parentRef));
                }

                ResolvedAppliesTo? appliesTo = null;
                if (action.AppliesTo is not null)
                {
                    appliesTo = ResolveAppliesTo(namespaceName, name, action.AppliesTo);
                }

                _actions[uid] = new ResolvedAction
                {
                    Entity = new Entity(uid, new EntityUidSet(parents), new CedarRecord(), new CedarRecord()),
                    Annotations = action.Annotations,
                    AppliesTo = appliesTo
                };
            }
        }

        private ResolvedAppliesTo ResolveAppliesTo(string namespaceName, string actionName, AppliesToDecl appliesTo)
        {
            List<EntityType> principals = [];
            foreach (EntityType principal in appliesTo.Principals)
            {
                try
                {
                    principals.Add(ResolveEntityTypeRef(namespaceName, principal));
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidOperationException($"action \"{actionName}\" principal: {exception.Message}", exception);
                }
            }

            List<EntityType> resources = [];
            foreach (EntityType resource in appliesTo.Resources)
            {
                try
                {
                    resources.Add(ResolveEntityTypeRef(namespaceName, resource));
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidOperationException($"action \"{actionName}\" resource: {exception.Message}", exception);
                }
            }

            ResolvedRecordType context = new();
            if (appliesTo.ContextRecord is not null)
            {
                try
                {
                    context = ResolveRecordType(namespaceName, appliesTo.ContextRecord);
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidOperationException($"action \"{actionName}\" context: {exception.Message}", exception);
                }
            }
            else if (appliesTo.ContextPath is not null)
            {
                try
                {
                    ResolvedType resolved = ResolveType(namespaceName, appliesTo.ContextPath);
                    if (resolved is not ResolvedRecordType record)
                    {
                        throw new InvalidOperationException($"action \"{actionName}\" context must resolve to a record type");
                    }

                    context = record;
                }
                catch (InvalidOperationException exception) when (!exception.Message.StartsWith($"action \"{actionName}\"", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"action \"{actionName}\" context: {exception.Message}", exception);
                }
            }

            return new ResolvedAppliesTo
            {
                Principals = principals,
                Resources = resources,
                Context = context
            };
        }

        private ResolvedType ResolveType(string namespaceName, SchemaType type)
        {
            return type switch
            {
                StringType => new ResolvedStringType(),
                LongType => new ResolvedLongType(),
                BoolType => new ResolvedBoolType(),
                ExtensionType extensionType => new ResolvedExtensionType(extensionType.Name),
                SetType setType => new ResolvedSetType(ResolveType(namespaceName, setType.Element)),
                RecordType recordType => ResolveRecordType(namespaceName, recordType),
                EntityTypeRef entityTypeRef => new ResolvedEntityType(ResolveEntityTypeRef(namespaceName, entityTypeRef.Name)),
                TypeRef typeRef => ResolveTypeRef(namespaceName, typeRef),
                _ => throw new InvalidOperationException($"unknown schema type: {type.GetType().FullName}")
            };
        }

        private ResolvedRecordType ResolveRecordType(string namespaceName, RecordType record)
        {
            Dictionary<string, ResolvedAttribute> attributes = new(StringComparer.Ordinal);
            foreach ((string name, AttributeDecl attribute) in record.Attributes)
            {
                try
                {
                    attributes[name] = new ResolvedAttribute
                    {
                        Type = ResolveType(namespaceName, attribute.Type),
                        Optional = attribute.Optional,
                        Annotations = attribute.Annotations
                    };
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidOperationException($"attribute \"{name}\": {exception.Message}", exception);
                }
            }

            return new ResolvedRecordType
            {
                Attributes = attributes
            };
        }

        private EntityType ResolveEntityTypeRef(string namespaceName, EntityType entityType)
        {
            string value = entityType.Value;
            if (ContainsNamespaceSeparator(value))
            {
                EntityType qualified = new(value);
                if (_entityTypes.Contains(qualified) || _enumTypes.Contains(qualified))
                {
                    return qualified;
                }

                throw new InvalidOperationException($"undefined entity type \"{value}\"");
            }

            if (!string.IsNullOrEmpty(namespaceName))
            {
                EntityType qualified = new(namespaceName + "::" + value);
                if (_entityTypes.Contains(qualified) || _enumTypes.Contains(qualified))
                {
                    return qualified;
                }
            }

            EntityType bare = new(value);
            if (_entityTypes.Contains(bare) || _enumTypes.Contains(bare))
            {
                return bare;
            }

            throw new InvalidOperationException($"undefined entity type \"{value}\"");
        }

        private ResolvedType ResolveTypeRef(string namespaceName, TypeRef typeRef)
        {
            if (ContainsNamespaceSeparator(typeRef.Name))
            {
                return ResolveQualifiedTypeRef(typeRef);
            }

            if (!string.IsNullOrEmpty(namespaceName))
            {
                string qualifiedPath = namespaceName + "::" + typeRef.Name;
                if (_commonTypes.TryGetValue(qualifiedPath, out SchemaType? commonType))
                {
                    return ResolveType(namespaceName, commonType);
                }

                EntityType qualifiedEntity = new(qualifiedPath);
                if (_entityTypes.Contains(qualifiedEntity) || _enumTypes.Contains(qualifiedEntity))
                {
                    return new ResolvedEntityType(qualifiedEntity);
                }
            }

            if (_commonTypes.TryGetValue(typeRef.Name, out SchemaType? bareCommonType))
            {
                return ResolveType(string.Empty, bareCommonType);
            }

            EntityType bareEntity = new(typeRef.Name);
            if (_entityTypes.Contains(bareEntity) || _enumTypes.Contains(bareEntity))
            {
                return new ResolvedEntityType(bareEntity);
            }

            ResolvedType? builtin = LookupBuiltin(typeRef.Name);
            if (builtin is not null)
            {
                return builtin;
            }

            throw new InvalidOperationException($"undefined type \"{typeRef.Name}\"");
        }

        private ResolvedType ResolveQualifiedTypeRef(TypeRef typeRef)
        {
            if (typeRef.Name.StartsWith("__cedar::", StringComparison.Ordinal))
            {
                string builtinName = typeRef.Name["__cedar::".Length..];
                ResolvedType? builtin = LookupBuiltin(builtinName);
                if (builtin is null)
                {
                    throw new InvalidOperationException($"undefined built-in type \"{typeRef.Name}\"");
                }

                return builtin;
            }

            if (_commonTypes.TryGetValue(typeRef.Name, out SchemaType? commonType))
            {
                return ResolveType(ExtractNamespace(typeRef.Name), commonType);
            }

            EntityType entityType = new(typeRef.Name);
            if (_entityTypes.Contains(entityType) || _enumTypes.Contains(entityType))
            {
                return new ResolvedEntityType(entityType);
            }

            throw new InvalidOperationException($"undefined type \"{typeRef.Name}\"");
        }

        private string ResolveTypeRefPath(string namespaceName, TypeRef typeRef)
        {
            if (ContainsNamespaceSeparator(typeRef.Name))
            {
                return typeRef.Name;
            }

            if (!string.IsNullOrEmpty(namespaceName))
            {
                string qualifiedPath = namespaceName + "::" + typeRef.Name;
                if (_commonTypes.ContainsKey(qualifiedPath))
                {
                    return qualifiedPath;
                }
            }

            return typeRef.Name;
        }

        private static void CheckTypeShadowing(string namespaceName, string name, HashSet<string> bareTypes)
        {
            if (bareTypes.Contains(name))
            {
                throw new InvalidOperationException(
                    $"definition of \"{namespaceName}::{name}\" illegally shadows the existing definition of \"{name}\"");
            }
        }

        private static IEnumerable<TypeRef> CollectTypeRefs(SchemaType type)
        {
            switch (type)
            {
                case TypeRef typeRef:
                    yield return typeRef;
                    yield break;
                case SetType setType:
                    foreach (TypeRef inner in CollectTypeRefs(setType.Element))
                    {
                        yield return inner;
                    }

                    yield break;
                case RecordType recordType:
                    foreach (AttributeDecl attribute in recordType.Attributes.Values)
                    {
                        foreach (TypeRef inner in CollectTypeRefs(attribute.Type))
                        {
                            yield return inner;
                        }
                    }

                    yield break;
                case StringType:
                case LongType:
                case BoolType:
                case ExtensionType:
                case EntityTypeRef:
                    yield break;
                default:
                    throw new InvalidOperationException($"unknown schema type: {type.GetType().FullName}");
            }
        }

        private static ResolvedType? LookupBuiltin(string name)
        {
            return name switch
            {
                "String" => new ResolvedStringType(),
                "Long" => new ResolvedLongType(),
                "Bool" => new ResolvedBoolType(),
                "Boolean" => new ResolvedBoolType(),
                "ipaddr" => new ResolvedExtensionType(new Ident("ipaddr")),
                "decimal" => new ResolvedExtensionType(new Ident("decimal")),
                "datetime" => new ResolvedExtensionType(new Ident("datetime")),
                "duration" => new ResolvedExtensionType(new Ident("duration")),
                _ => null
            };
        }

        private static EntityUid ResolveActionParentRef(string namespaceName, ParentRef parentRef)
        {
            EntityType parentType = parentRef.Type ?? new EntityType(QualifyActionType(namespaceName));
            return new EntityUid(parentType, new CedarString(parentRef.Id));
        }

        private static string QualifyEntityType(string namespaceName, string name)
        {
            return string.IsNullOrEmpty(namespaceName) ? name : namespaceName + "::" + name;
        }

        private static string QualifyPath(string namespaceName, string name)
        {
            return string.IsNullOrEmpty(namespaceName) ? name : namespaceName + "::" + name;
        }

        private static string QualifyActionType(string namespaceName)
        {
            return string.IsNullOrEmpty(namespaceName) ? "Action" : namespaceName + "::Action";
        }

        private static string ExtractNamespace(string path)
        {
            int index = path.LastIndexOf("::", StringComparison.Ordinal);
            return index >= 0 ? path[..index] : string.Empty;
        }

        private static bool ContainsNamespaceSeparator(string value)
        {
            return value.Contains("::", StringComparison.Ordinal);
        }
    }
}

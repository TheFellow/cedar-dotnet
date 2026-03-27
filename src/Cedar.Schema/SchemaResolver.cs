using System;
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema;

public static class SchemaResolver
{
    public static ResolvedSchema Resolve(SchemaDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        Dictionary<EntityUid, ResolvedAction> actions = [];

        ResolveNamespace(string.Empty, document.GlobalNamespace, actions);

        foreach ((string namespaceName, NamespaceDecl declaration) in document.Namespaces)
        {
            ResolveNamespace(namespaceName, declaration, actions);
        }

        ValidateActionMembership(actions);

        return new ResolvedSchema
        {
            Actions = actions
        };
    }

    private static void ResolveNamespace(string namespaceName, NamespaceDecl declaration, Dictionary<EntityUid, ResolvedAction> actions)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        string actionTypeName = QualifyActionType(namespaceName);
        EntityType actionType = new(actionTypeName);

        foreach ((string name, ActionDecl actionDeclaration) in declaration.Actions)
        {
            EntityUid uid = new(actionType, new CedarString(name));
            List<EntityUid> parents = [];

            foreach (ParentRef parentRef in actionDeclaration.Parents)
            {
                parents.Add(ResolveActionParentRef(namespaceName, parentRef));
            }

            ResolvedAction resolvedAction = new()
            {
                Entity = new Entity(uid, new EntityUidSet(parents), new CedarRecord(), new CedarRecord()),
                Annotations = actionDeclaration.Annotations,
                AppliesTo = actionDeclaration.AppliesTo
            };

            if (!actions.TryAdd(uid, resolvedAction))
            {
                throw new InvalidOperationException($"Duplicate resolved action {uid}");
            }
        }
    }

    private static EntityUid ResolveActionParentRef(string namespaceName, ParentRef parentRef)
    {
        EntityType parentType = parentRef.Type ?? new EntityType(QualifyActionType(namespaceName));
        return new EntityUid(parentType, new CedarString(parentRef.Id));
    }

    private static void ValidateActionMembership(IReadOnlyDictionary<EntityUid, ResolvedAction> actions)
    {
        foreach ((EntityUid uid, ResolvedAction action) in actions)
        {
            foreach (EntityUid parent in action.Entity.Parents)
            {
                if (!actions.ContainsKey(parent))
                {
                    throw new InvalidOperationException($"action {uid}: undefined parent action {parent}");
                }
            }
        }

        Dictionary<EntityUid, int> visited = [];

        foreach (EntityUid uid in actions.Keys)
        {
            Visit(uid);
        }

        void Visit(EntityUid uid)
        {
            if (visited.TryGetValue(uid, out int state))
            {
                switch (state)
                {
                    case 1:
                        throw new InvalidOperationException($"cycle detected in action hierarchy involving {uid}");
                    case 2:
                        return;
                }
            }

            visited[uid] = 1;

            foreach (EntityUid parent in actions[uid].Entity.Parents)
            {
                Visit(parent);
            }

            visited[uid] = 2;
        }
    }

    private static string QualifyActionType(string namespaceName)
    {
        return string.IsNullOrEmpty(namespaceName)
            ? "Action"
            : namespaceName + "::Action";
    }
}
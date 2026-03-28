using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cedar.Ast.Internal;
using Cedar.Types;

namespace Cedar.Core.Internal.Json;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ScopeJsonModel
{
    [JsonPropertyName("op")]
    public required string Op { get; init; }

    [JsonPropertyName("entity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EntityUidJsonModel? Entity { get; init; }

    [JsonPropertyName("entity_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EntityType { get; init; }

    [JsonPropertyName("entities")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<EntityUidJsonModel>? Entities { get; init; }

    [JsonPropertyName("in")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EntityUidJsonModel? In { get; init; }

    internal static ScopeJsonModel FromAst(IScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return scope switch
        {
            ScopeAll => new ScopeJsonModel { Op = "All" },
            ScopeEq eq => new ScopeJsonModel
            {
                Op = "==",
                Entity = EntityUidJsonModel.FromEntity(eq.Entity)
            },
            ScopeIn @in => new ScopeJsonModel
            {
                Op = "in",
                Entity = EntityUidJsonModel.FromEntity(@in.Entity)
            },
            ScopeInSet inSet => new ScopeJsonModel
            {
                Op = "in",
                Entities = inSet.Entities.Select(EntityUidJsonModel.FromEntity).ToList()
            },
            ScopeIs isScope => new ScopeJsonModel
            {
                Op = "is",
                EntityType = isScope.Type.Value
            },
            ScopeIsIn isIn => new ScopeJsonModel
            {
                Op = "is",
                EntityType = isIn.Type.Value,
                In = EntityUidJsonModel.FromEntity(isIn.Entity)
            },
            _ => throw new JsonException($"Unsupported scope type '{scope.GetType().FullName}'.")
        };
    }

    internal IScope ToAst()
    {
        return Op switch
        {
            "All" => new ScopeAll(),
            "==" => new ScopeEq(RequireEntity(Entity, Op).ToEntity()),
            "in" => ToInScope(),
            "is" => ToIsScope(),
            _ => throw new JsonException($"Unsupported scope op '{Op}'.")
        };
    }

    private IScope ToInScope()
    {
        if (Entity is not null)
        {
            return new ScopeIn(Entity.ToEntity());
        }

        if (Entities is null || Entities.Count == 0)
        {
            throw new JsonException("Scope with op 'in' must include 'entity' or non-empty 'entities'.");
        }

        return new ScopeInSet(Entities.Select(static model => model.ToEntity()).ToArray());
    }

    private IScope ToIsScope()
    {
        if (string.IsNullOrEmpty(EntityType))
        {
            throw new JsonException("Scope with op 'is' must include 'entity_type'.");
        }

        EntityType entityType = new(EntityType);
        if (In is null)
        {
            return new ScopeIs(entityType);
        }

        return new ScopeIsIn(entityType, In.ToEntity());
    }

    private static EntityUidJsonModel RequireEntity(EntityUidJsonModel? entity, string op)
    {
        return entity ?? throw new JsonException($"Scope with op '{op}' must include 'entity'.");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record EntityUidJsonModel
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    internal static EntityUidJsonModel FromEntity(EntityUid entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new EntityUidJsonModel
        {
            Type = entity.Type.Value,
            Id = entity.Id.Value
        };
    }

    internal EntityUid ToEntity()
    {
        return new EntityUid(new EntityType(Type), new CedarString(Id));
    }
}

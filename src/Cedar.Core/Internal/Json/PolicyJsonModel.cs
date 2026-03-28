using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Types;

namespace Cedar.Core.Internal.Json;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record PolicyJsonModel
{
    [JsonPropertyName("effect")]
    [JsonPropertyOrder(0)]
    public required string Effect { get; init; }

    [JsonPropertyName("principal")]
    [JsonPropertyOrder(1)]
    public required ScopeJsonModel Principal { get; init; }

    [JsonPropertyName("action")]
    [JsonPropertyOrder(2)]
    public required ScopeJsonModel Action { get; init; }

    [JsonPropertyName("resource")]
    [JsonPropertyOrder(3)]
    public required ScopeJsonModel Resource { get; init; }

    [JsonPropertyName("conditions")]
    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ConditionJsonModel>? Conditions { get; init; }

    [JsonPropertyName("annotations")]
    [JsonPropertyOrder(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SortedDictionary<string, string>? Annotations { get; init; }

    internal static PolicyJsonModel FromAst(PolicyAst policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        SortedDictionary<string, string>? annotations = null;
        if (!policy.Annotations.IsEmpty)
        {
            annotations = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (Annotation annotation in policy.Annotations)
            {
                annotations[annotation.Key.Value] = annotation.Value.Value;
            }
        }

        List<ConditionJsonModel>? conditions = null;
        if (!policy.Conditions.IsEmpty)
        {
            conditions = [];
            foreach (INode condition in policy.Conditions)
            {
                if (condition is NodeNot unless)
                {
                    conditions.Add(new ConditionJsonModel
                    {
                        Kind = "unless",
                        Body = NodeJsonModel.FromAst(unless.Arg)
                    });
                    continue;
                }

                conditions.Add(new ConditionJsonModel
                {
                    Kind = "when",
                    Body = NodeJsonModel.FromAst(condition)
                });
            }
        }

        return new PolicyJsonModel
        {
            Effect = policy.Effect == Cedar.Core.Effect.Permit ? "permit" : "forbid",
            Principal = ScopeJsonModel.FromAst(policy.PrincipalScope),
            Action = ScopeJsonModel.FromAst(policy.ActionScope),
            Resource = ScopeJsonModel.FromAst(policy.ResourceScope),
            Conditions = conditions,
            Annotations = annotations
        };
    }

    internal PolicyAst ToAst()
    {
        Cedar.Core.Effect effect = Effect switch
        {
            "permit" => Cedar.Core.Effect.Permit,
            "forbid" => Cedar.Core.Effect.Forbid,
            _ => throw new JsonException($"Unknown policy effect '{Effect}'.")
        };

        List<INode> conditions = [];
        if (Conditions is not null)
        {
            foreach (ConditionJsonModel condition in Conditions)
            {
                INode body = NodeJsonModel.ToAst(condition.Body);
                switch (condition.Kind)
                {
                    case "when":
                        conditions.Add(body);
                        break;
                    case "unless":
                        conditions.Add(new NodeNot(body));
                        break;
                    default:
                        throw new JsonException($"Unknown condition kind '{condition.Kind}'.");
                }
            }
        }

        List<Annotation> annotations = [];
        if (Annotations is not null)
        {
            foreach (KeyValuePair<string, string> annotation in Annotations)
            {
                annotations.Add(new Annotation(new Ident(annotation.Key), new CedarString(annotation.Value)));
            }
        }

        return new PolicyAst(
            effect,
            Principal.ToAst(),
            Action.ToAst(),
            Resource.ToAst(),
            [.. conditions],
            [.. annotations],
            new Position(string.Empty, 0, 0, 0));
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ConditionJsonModel
{
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("body")]
    public required JsonObject Body { get; init; }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cedar.Ast.Internal;
using Cedar.Types;

namespace Cedar.Core.Internal.Json;

internal static class NodeJsonModel
{
    internal static JsonObject FromAst(INode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node switch
        {
            NodeEquals n => Binary("==", n.Left, n.Right, "right"),
            NodeNotEquals n => Binary("!=", n.Left, n.Right, "right"),
            NodeLessThan n => Binary("<", n.Left, n.Right, "right"),
            NodeLessThanOrEqual n => Binary("<=", n.Left, n.Right, "right"),
            NodeGreaterThan n => Binary(">", n.Left, n.Right, "right"),
            NodeGreaterThanOrEqual n => Binary(">=", n.Left, n.Right, "right"),
            NodeAnd n => Binary("&&", n.Left, n.Right, "right"),
            NodeOr n => Binary("||", n.Left, n.Right, "right"),
            NodeAdd n => Binary("+", n.Left, n.Right, "right"),
            NodeSub n => Binary("-", n.Left, n.Right, "right"),
            NodeMult n => Binary("*", n.Left, n.Right, "right"),
            NodeIn n => Binary("in", n.Left, n.Right, "right"),
            NodeContains n => Binary(".contains", n.Left, n.Right, "arg"),
            NodeContainsAll n => Binary(".containsAll", n.Left, n.Right, "arg"),
            NodeContainsAny n => Binary(".containsAny", n.Left, n.Right, "arg"),
            NodeGetTag n => Binary(".getTag", n.Left, n.Right, "arg"),
            NodeHasTag n => Binary(".hasTag", n.Left, n.Right, "arg"),
            NodeNot n => Unary("!", n.Arg),
            NodeNegate n => Unary("neg", n.Arg),
            NodeIsEmpty n => Unary(".isEmpty", n.Arg),
            NodeValue n => new JsonObject
            {
                ["Value"] = JsonSerializer.SerializeToNode<ICedarData>(n.Value, PolicyJsonSerializerOptions.Instance)
            },
            NodeVariable n => new JsonObject
            {
                ["Var"] = n.Name.Value
            },
            NodeAccess { Attribute: NodeValue { Value: CedarString attr } } n => new JsonObject
            {
                ["."] = new JsonObject
                {
                    ["left"] = FromAst(n.Arg),
                    ["attr"] = attr.Value
                }
            },
            NodeAccess => throw new InvalidOperationException("Cannot serialize NodeAccess with non-literal attribute expression to Cedar JSON."),
            NodeHas n => new JsonObject
            {
                ["has"] = new JsonObject
                {
                    ["left"] = FromAst(n.Arg),
                    ["attr"] = n.Attribute.Value
                }
            },
            NodeLike n => new JsonObject
            {
                ["like"] = new JsonObject
                {
                    ["left"] = FromAst(n.Arg),
                    ["pattern"] = PatternToJson(n.Pattern)
                }
            },
            NodeIfThenElse n => new JsonObject
            {
                ["if-then-else"] = new JsonObject
                {
                    ["if"] = FromAst(n.If),
                    ["then"] = FromAst(n.Then),
                    ["else"] = FromAst(n.Else)
                }
            },
            NodeIs n => new JsonObject
            {
                ["is"] = new JsonObject
                {
                    ["left"] = FromAst(n.Left),
                    ["entity_type"] = n.EntityType.Value
                }
            },
            NodeIsIn n => new JsonObject
            {
                ["is"] = new JsonObject
                {
                    ["left"] = FromAst(n.Left),
                    ["entity_type"] = n.EntityType.Value,
                    ["in"] = FromAst(n.Entity)
                }
            },
            NodeSet n => SetNode(n),
            NodeRecord n => RecordNode(n),
            NodeExtensionCall n => ExtensionNode(n),
            _ => throw new JsonException($"Unsupported AST node type '{node.GetType().FullName}'.")
        };
    }

    internal static INode ToAst(JsonNode node)
    {
        JsonObject container = AsObject(node, "node");
        if (container.Count != 1)
        {
            throw new JsonException("Node JSON object must contain exactly one discriminator property.");
        }

        KeyValuePair<string, JsonNode?> entry = default;
        foreach (KeyValuePair<string, JsonNode?> property in container)
        {
            entry = property;
            break;
        }

        JsonNode valueNode = entry.Value ?? throw new JsonException($"Node discriminator '{entry.Key}' cannot be null.");
        return entry.Key switch
        {
            "==" => ReadBinary(valueNode, static (left, right) => new NodeEquals(left, right), "right"),
            "!=" => ReadBinary(valueNode, static (left, right) => new NodeNotEquals(left, right), "right"),
            "<" => ReadBinary(valueNode, static (left, right) => new NodeLessThan(left, right), "right"),
            "<=" => ReadBinary(valueNode, static (left, right) => new NodeLessThanOrEqual(left, right), "right"),
            ">" => ReadBinary(valueNode, static (left, right) => new NodeGreaterThan(left, right), "right"),
            ">=" => ReadBinary(valueNode, static (left, right) => new NodeGreaterThanOrEqual(left, right), "right"),
            "&&" => ReadBinary(valueNode, static (left, right) => new NodeAnd(left, right), "right"),
            "||" => ReadBinary(valueNode, static (left, right) => new NodeOr(left, right), "right"),
            "+" => ReadBinary(valueNode, static (left, right) => new NodeAdd(left, right), "right"),
            "-" => ReadBinary(valueNode, static (left, right) => new NodeSub(left, right), "right"),
            "*" => ReadBinary(valueNode, static (left, right) => new NodeMult(left, right), "right"),
            "in" => ReadBinary(valueNode, static (left, right) => new NodeIn(left, right), "right"),
            ".contains" => ReadBinary(valueNode, static (left, right) => new NodeContains(left, right), "arg", "right"),
            ".containsAll" => ReadBinary(valueNode, static (left, right) => new NodeContainsAll(left, right), "arg", "right"),
            ".containsAny" => ReadBinary(valueNode, static (left, right) => new NodeContainsAny(left, right), "arg", "right"),
            ".getTag" => ReadBinary(valueNode, static (left, right) => new NodeGetTag(left, right), "arg", "right"),
            ".hasTag" => ReadBinary(valueNode, static (left, right) => new NodeHasTag(left, right), "arg", "right"),
            "contains" => ReadBinary(valueNode, static (left, right) => new NodeContains(left, right), "right", "arg"),
            "containsAll" => ReadBinary(valueNode, static (left, right) => new NodeContainsAll(left, right), "right", "arg"),
            "containsAny" => ReadBinary(valueNode, static (left, right) => new NodeContainsAny(left, right), "right", "arg"),
            "getTag" => ReadBinary(valueNode, static (left, right) => new NodeGetTag(left, right), "right", "arg"),
            "hasTag" => ReadBinary(valueNode, static (left, right) => new NodeHasTag(left, right), "right", "arg"),
            "!" => ReadUnary(valueNode, static arg => new NodeNot(arg)),
            "neg" => ReadUnary(valueNode, static arg => new NodeNegate(arg)),
            ".isEmpty" => ReadUnary(valueNode, static arg => new NodeIsEmpty(arg)),
            "isEmpty" => ReadUnary(valueNode, static arg => new NodeIsEmpty(arg)),
            "Value" => new NodeValue(ReadCedarValue(valueNode)),
            "Var" => new NodeVariable(new CedarString(ReadVariableName(valueNode))),
            "." => ReadStr(valueNode, static (arg, attr) => new NodeAccess(arg, new NodeValue(new CedarString(attr)))),
            "has" => ReadStr(valueNode, static (arg, attr) => new NodeHas(arg, new CedarString(attr))),
            "like" => ReadLike(valueNode),
            "if-then-else" => ReadIfThenElse(valueNode),
            "is" => ReadIs(valueNode),
            "Set" => ReadSet(valueNode),
            "Record" => ReadRecord(valueNode),
            _ => ReadExtensionCall(entry.Key, valueNode)
        };
    }

    private static JsonObject Binary(string discriminator, INode left, INode right, string rightPropertyName)
    {
        return new JsonObject
        {
            [discriminator] = new JsonObject
            {
                ["left"] = FromAst(left),
                [rightPropertyName] = FromAst(right)
            }
        };
    }

    private static JsonObject Unary(string discriminator, INode arg)
    {
        return new JsonObject
        {
            [discriminator] = new JsonObject
            {
                ["arg"] = FromAst(arg)
            }
        };
    }

    private static JsonObject SetNode(NodeSet node)
    {
        JsonArray elements = [];
        foreach (INode element in node.Elements)
        {
            elements.Add(FromAst(element));
        }

        return new JsonObject { ["Set"] = elements };
    }

    private static JsonObject RecordNode(NodeRecord node)
    {
        JsonArray pairs = [];
        foreach (NodeRecordElement element in node.Elements)
        {
            pairs.Add(new JsonObject
            {
                ["key"] = element.Key.Value,
                ["value"] = FromAst(element.Value)
            });
        }

        return new JsonObject
        {
            ["Record"] = new JsonObject
            {
                ["pairs"] = pairs
            }
        };
    }

    private static JsonObject ExtensionNode(NodeExtensionCall node)
    {
        JsonArray args = [];
        foreach (INode arg in node.Args)
        {
            args.Add(FromAst(arg));
        }

        return new JsonObject { [node.Name] = args };
    }

    private static INode ReadBinary(JsonNode node, Func<INode, INode, INode> create, params string[] rightNames)
    {
        JsonObject payload = AsObject(node, "binary");
        INode left = ToAst(ReadRequiredProperty(payload, "left"));

        JsonNode? rightNode = null;
        foreach (string rightName in rightNames)
        {
            if (payload.TryGetPropertyValue(rightName, out JsonNode? candidate) && candidate is not null)
            {
                rightNode = candidate;
                break;
            }
        }

        if (rightNode is null)
        {
            throw new JsonException($"Binary node payload is missing expected right-side property ({string.Join(", ", rightNames)}).");
        }

        return create(left, ToAst(rightNode));
    }

    private static INode ReadUnary(JsonNode node, Func<INode, INode> create)
    {
        JsonObject payload = AsObject(node, "unary");
        return create(ToAst(ReadRequiredProperty(payload, "arg")));
    }

    private static INode ReadStr(JsonNode node, Func<INode, string, INode> create)
    {
        JsonObject payload = AsObject(node, "string op");
        JsonNode leftNode = payload.TryGetPropertyValue("left", out JsonNode? left) && left is not null
            ? left
            : ReadRequiredProperty(payload, "arg");

        return create(ToAst(leftNode), ReadRequiredString(payload, "attr"));
    }

    private static INode ReadLike(JsonNode node)
    {
        JsonObject payload = AsObject(node, "like");
        INode left = ToAst(ReadRequiredProperty(payload, "left"));
        CedarPattern pattern = ReadPattern(ReadRequiredProperty(payload, "pattern"));
        return new NodeLike(left, pattern);
    }

    private static INode ReadIfThenElse(JsonNode node)
    {
        JsonObject payload = AsObject(node, "if-then-else");
        return new NodeIfThenElse(
            ToAst(ReadRequiredProperty(payload, "if")),
            ToAst(ReadRequiredProperty(payload, "then")),
            ToAst(ReadRequiredProperty(payload, "else")));
    }

    private static INode ReadIs(JsonNode node)
    {
        JsonObject payload = AsObject(node, "is");
        INode left = ToAst(ReadRequiredProperty(payload, "left"));
        EntityType entityType = new(ReadRequiredString(payload, "entity_type"));

        if (payload.TryGetPropertyValue("in", out JsonNode? inNode) && inNode is not null)
        {
            return new NodeIsIn(left, entityType, ToAst(inNode));
        }

        return new NodeIs(left, entityType);
    }

    private static INode ReadSet(JsonNode node)
    {
        JsonArray array = AsArray(node, "Set");
        ImmutableArray<INode>.Builder elements = ImmutableArray.CreateBuilder<INode>(array.Count);

        foreach (JsonNode? element in array)
        {
            elements.Add(ToAst(element ?? throw new JsonException("Set nodes cannot contain null elements.")));
        }

        return new NodeSet(elements.ToImmutable());
    }

    private static INode ReadRecord(JsonNode node)
    {
        JsonObject payload = AsObject(node, "Record");

        if (!payload.TryGetPropertyValue("pairs", out JsonNode? pairsNode) || pairsNode is null)
        {
            return ReadRecordLegacyMap(payload);
        }

        JsonArray pairs = AsArray(pairsNode, "Record.pairs");
        HashSet<string> keys = new(StringComparer.Ordinal);
        ImmutableArray<NodeRecordElement>.Builder elements = ImmutableArray.CreateBuilder<NodeRecordElement>(pairs.Count);

        foreach (JsonNode? pairNode in pairs)
        {
            JsonObject pair = AsObject(pairNode ?? throw new JsonException("Record pairs cannot contain null values."), "Record.pairs[]");
            string key = ReadRequiredString(pair, "key");
            if (!keys.Add(key))
            {
                throw new JsonException($"Duplicate record key '{key}'.");
            }

            elements.Add(new NodeRecordElement(new CedarString(key), ToAst(ReadRequiredProperty(pair, "value"))));
        }

        return new NodeRecord(elements.ToImmutable());
    }

    private static INode ReadRecordLegacyMap(JsonObject payload)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        ImmutableArray<NodeRecordElement>.Builder elements = ImmutableArray.CreateBuilder<NodeRecordElement>(payload.Count);

        foreach (KeyValuePair<string, JsonNode?> property in payload)
        {
            if (!keys.Add(property.Key))
            {
                throw new JsonException($"Duplicate record key '{property.Key}'.");
            }

            elements.Add(new NodeRecordElement(
                new CedarString(property.Key),
                ToAst(property.Value ?? throw new JsonException("Record values cannot be null."))));
        }

        return new NodeRecord(elements.ToImmutable());
    }

    private static INode ReadExtensionCall(string name, JsonNode node)
    {
        JsonArray argsNode = AsArray(node, $"extension call '{name}'");
        ImmutableArray<INode>.Builder args = ImmutableArray.CreateBuilder<INode>(argsNode.Count);

        foreach (JsonNode? arg in argsNode)
        {
            args.Add(ToAst(arg ?? throw new JsonException("Extension call arguments cannot be null.")));
        }

        return new NodeExtensionCall(name, args.ToImmutable());
    }

    private static ICedarData ReadCedarValue(JsonNode node)
    {
        ICedarData? value = node.Deserialize<ICedarData>(PolicyJsonSerializerOptions.Instance);
        return value ?? throw new JsonException("Value nodes cannot deserialize to null.");
    }

    private static string ReadVariableName(JsonNode node)
    {
        string variable = AsString(node, "Var");
        return variable is "principal" or "action" or "resource" or "context"
            ? variable
            : throw new JsonException($"Unsupported Cedar variable '{variable}'.");
    }

    private static JsonArray PatternToJson(CedarPattern pattern)
    {
        JsonArray components = [];
        foreach (object component in ParsePatternComponents(pattern.ToPatternText()))
        {
            switch (component)
            {
                case Wildcard:
                    components.Add("Wildcard");
                    break;
                case string literal:
                    components.Add(new JsonObject { ["Literal"] = literal });
                    break;
            }
        }

        return components;
    }

    private static CedarPattern ReadPattern(JsonNode node)
    {
        JsonArray components = AsArray(node, "like.pattern");
        if (components.Count == 0)
        {
            throw new JsonException("Pattern arrays must include at least one component.");
        }

        List<object> values = [];
        foreach (JsonNode? component in components)
        {
            if (component is JsonValue value && value.TryGetValue<string>(out string? stringValue) && stringValue is not null)
            {
                if (!string.Equals(stringValue, "Wildcard", StringComparison.Ordinal))
                {
                    throw new JsonException($"Invalid pattern component string '{stringValue}'.");
                }

                values.Add(Wildcard.Instance);
                continue;
            }

            JsonObject literalObject = AsObject(component ?? throw new JsonException("Pattern components cannot be null."), "pattern component");
            if (literalObject.Count != 1 || !literalObject.TryGetPropertyValue("Literal", out JsonNode? literalNode) || literalNode is null)
            {
                throw new JsonException("Pattern literal components must be objects with only a 'Literal' string property.");
            }

            values.Add(AsString(literalNode, "Literal"));
        }

        return new CedarPattern(values.ToArray());
    }

    private static IReadOnlyList<object> ParsePatternComponents(string patternText)
    {
        List<object> components = [];
        StringBuilder literal = new();
        bool escaped = false;

        foreach (char character in patternText)
        {
            if (escaped)
            {
                literal.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character == '*')
            {
                if (literal.Length > 0)
                {
                    components.Add(literal.ToString());
                    literal.Clear();
                }

                components.Add(Wildcard.Instance);
                continue;
            }

            literal.Append(character);
        }

        if (escaped)
        {
            literal.Append('\\');
        }

        if (literal.Length > 0 || components.Count == 0)
        {
            components.Add(literal.ToString());
        }

        return components;
    }

    private static JsonObject AsObject(JsonNode node, string context)
    {
        return node as JsonObject ?? throw new JsonException($"Expected {context} to be a JSON object.");
    }

    private static JsonArray AsArray(JsonNode node, string context)
    {
        return node as JsonArray ?? throw new JsonException($"Expected {context} to be a JSON array.");
    }

    private static JsonNode ReadRequiredProperty(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out JsonNode? node) || node is null)
        {
            throw new JsonException($"Missing required property '{propertyName}'.");
        }

        return node;
    }

    private static string ReadRequiredString(JsonObject obj, string propertyName)
    {
        return AsString(ReadRequiredProperty(obj, propertyName), propertyName);
    }

    private static string AsString(JsonNode node, string context)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out string? stringValue) && stringValue is not null)
        {
            return stringValue;
        }

        throw new JsonException($"Expected '{context}' to be a JSON string.");
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cedar.Types;

[JsonConverter(typeof(CedarPatternJsonConverter))]
public sealed record CedarPattern : CedarValue
{
    private readonly PatternComponent[] _components;

    public CedarPattern(params object[] components)
    {
        _components = NormalizeComponents(components);
    }

    private CedarPattern(PatternComponent[] components, bool alreadyNormalized)
    {
        _components = components;
    }

    public static CedarPattern Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        List<object> components = [];
        StringBuilder literal = new();
        bool escaped = false;

        foreach (char character in value)
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

        return new CedarPattern([.. components]);
    }

    public CedarPattern AddWildcard()
    {
        List<PatternComponent> components = CreateBuilderComponents();
        AppendWildcard(components);
        return new CedarPattern([.. components], alreadyNormalized: true);
    }

    public CedarPattern AddLiteral(string literal)
    {
        ArgumentNullException.ThrowIfNull(literal);

        List<PatternComponent> components = CreateBuilderComponents();
        AppendLiteral(components, literal);
        return new CedarPattern([.. components], alreadyNormalized: true);
    }

    public bool Match(CedarString value)
    {
        return MatchCore(value.Value);
    }

    private bool MatchCore(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int position = 0;

        for (int index = 0; index < _components.Length; index++)
        {
            PatternComponent component = _components[index];
            bool lastComponent = index == _components.Length - 1;

            if (component.Wildcard && component.Literal.Length == 0)
            {
                return true;
            }

            if (MatchChunk(component.Literal, value, position, out int nextPosition) && (nextPosition == value.Length || !lastComponent))
            {
                position = nextPosition;
                continue;
            }

            if (!component.Wildcard)
            {
                return false;
            }

            bool matched = false;
            for (int candidate = position + 1; candidate <= value.Length; candidate++)
            {
                if (!MatchChunk(component.Literal, value, candidate, out nextPosition))
                {
                    continue;
                }

                if (lastComponent && nextPosition < value.Length)
                {
                    continue;
                }

                position = nextPosition;
                matched = true;
                break;
            }

            if (!matched)
            {
                return false;
            }
        }

        return position == value.Length;
    }

    public override string MarshalCedar()
    {
        StringBuilder builder = new();
        builder.Append('"');

        foreach (PatternComponent component in _components)
        {
            if (component.Wildcard)
            {
                builder.Append('*');
            }

            if (component.Literal.Length > 0)
            {
                builder.Append(CedarString.EscapeCharAll(component.Literal).Replace("*", "\\*", StringComparison.Ordinal));
            }
        }

        builder.Append('"');
        return builder.ToString();
    }

    public bool Equals(CedarPattern? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || _components.Length != other._components.Length)
        {
            return false;
        }

        for (int index = 0; index < _components.Length; index++)
        {
            if (_components[index] != other._components[index])
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        return CedarHash.ForString(nameof(CedarPattern), ToPatternText());
    }

    public string ToPatternText()
    {
        StringBuilder builder = new();

        foreach (PatternComponent component in _components)
        {
            if (component.Wildcard)
            {
                builder.Append('*');
            }

            if (component.Literal.Length > 0)
            {
                builder.Append(component.Literal.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("*", "\\*", StringComparison.Ordinal));
            }
        }

        return builder.ToString();
    }

    internal void WriteJson(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartArray();

        foreach (PatternComponent component in _components)
        {
            if (component.Wildcard)
            {
                writer.WriteStringValue("Wildcard");
            }

            if (!component.Wildcard || component.Literal.Length > 0)
            {
                writer.WriteStartObject();
                writer.WriteString("Literal", component.Literal);
                writer.WriteEndObject();
            }
        }

        writer.WriteEndArray();
    }

    internal static CedarPattern ReadJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Pattern arrays must be JSON arrays.");
        }

        List<object> values = [];
        foreach (JsonElement component in element.EnumerateArray())
        {
            switch (component.ValueKind)
            {
                case JsonValueKind.String:
                    string stringValue = component.GetString()!;
                    if (!string.Equals(stringValue, "Wildcard", StringComparison.Ordinal))
                    {
                        throw new JsonException($"Invalid pattern component string '{stringValue}'.");
                    }

                    values.Add(Wildcard.Instance);
                    break;
                case JsonValueKind.Object:
                    string? propertyName = null;
                    JsonElement propertyValue = default;
                    int propertyCount = 0;

                    foreach (JsonProperty property in component.EnumerateObject())
                    {
                        propertyCount++;
                        propertyName = property.Name;
                        propertyValue = property.Value;
                    }

                    if (propertyCount != 1 || !string.Equals(propertyName, "Literal", StringComparison.Ordinal) || propertyValue.ValueKind != JsonValueKind.String)
                    {
                        throw new JsonException("Pattern literal components must be objects with only a 'Literal' string property.");
                    }

                    values.Add(propertyValue.GetString()!);
                    break;
                default:
                    throw new JsonException("Pattern components must be either the string 'Wildcard' or an object with a 'Literal' string property.");
            }
        }

        if (values.Count == 0)
        {
            throw new JsonException("Pattern arrays must include at least one component.");
        }

        return new CedarPattern([.. values]);
    }

    private List<PatternComponent> CreateBuilderComponents()
    {
        List<PatternComponent> components = [.. _components];

        if (components.Count == 1 && !components[0].Wildcard && components[0].Literal.Length == 0)
        {
            components.Clear();
        }

        return components;
    }

    private static bool MatchChunk(string chunk, string value, int offset, out int nextOffset)
    {
        nextOffset = offset;

        if (offset + chunk.Length > value.Length)
        {
            return false;
        }

        for (int index = 0; index < chunk.Length; index++)
        {
            if (chunk[index] != value[offset + index])
            {
                return false;
            }
        }

        nextOffset += chunk.Length;
        return true;
    }

    private static PatternComponent[] NormalizeComponents(IEnumerable<object> components)
    {
        List<PatternComponent> normalized = [];

        foreach (object component in components)
        {
            switch (component)
            {
                case string literal:
                    AppendLiteral(normalized, literal);
                    break;
                case CedarString literal:
                    AppendLiteral(normalized, literal.Value);
                    break;
                case Wildcard:
                    AppendWildcard(normalized);
                    break;
                default:
                    throw new ArgumentException($"Unexpected pattern component type: {component.GetType().FullName}", nameof(components));
            }
        }

        if (normalized.Count == 0)
        {
            normalized.Add(new PatternComponent(false, string.Empty));
        }

        return [.. normalized];
    }

    private static void AppendLiteral(List<PatternComponent> components, string literal)
    {
        if (components.Count == 0)
        {
            components.Add(new PatternComponent(false, literal));
            return;
        }

        PatternComponent last = components[^1];
        components[^1] = new PatternComponent(last.Wildcard, last.Literal + literal);
    }

    private static void AppendWildcard(List<PatternComponent> components)
    {
        if (components.Count == 0 || components[^1].Literal.Length > 0)
        {
            components.Add(new PatternComponent(true, string.Empty));
        }
    }

    private readonly record struct PatternComponent(bool Wildcard, string Literal);
}

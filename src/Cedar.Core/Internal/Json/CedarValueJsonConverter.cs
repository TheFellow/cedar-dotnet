using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cedar.Types;

namespace Cedar.Core.Internal.Json;

internal sealed class CedarValueJsonConverter : JsonConverter<ICedarData>
{
    public override ICedarData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        return ReadElement(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, ICedarData value, JsonSerializerOptions options)
    {
        WriteValue(writer, value);
    }

    internal static ICedarData ReadElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => CedarBool.True,
            JsonValueKind.False => CedarBool.False,
            JsonValueKind.Number => ReadLong(element),
            JsonValueKind.String => new CedarString(element.GetString()!),
            JsonValueKind.Array => ReadSet(element),
            JsonValueKind.Object => ReadObject(element),
            _ => throw new JsonException($"Unsupported JSON token kind '{element.ValueKind}'.")
        };
    }

    internal static void WriteValue(Utf8JsonWriter writer, ICedarData value)
    {
        switch (value)
        {
            case CedarBool boolean:
                writer.WriteBooleanValue(boolean.Value);
                break;
            case CedarLong integer:
                writer.WriteNumberValue(integer.Value);
                break;
            case CedarString text:
                writer.WriteStringValue(text.Value);
                break;
            case CedarDecimal decimalValue:
                WriteExtension(writer, "decimal", decimalValue.MarshalCedar()[9..^2]);
                break;
            case CedarDatetime datetime:
                WriteExtension(writer, "datetime", datetime.MarshalCedar()[10..^2]);
                break;
            case CedarDuration duration:
                WriteExtension(writer, "duration", duration.MarshalCedar()[10..^2]);
                break;
            case CedarIpAddress ipAddress:
                WriteExtension(writer, "ip", ipAddress.MarshalCedar()[4..^2]);
                break;
            case CedarPattern pattern:
                WriteExtension(writer, "pattern", pattern.ToPatternText());
                break;
            case CedarSet set:
                writer.WriteStartArray();
                foreach (ICedarData item in set)
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            case CedarRecord record:
                writer.WriteStartObject();
                foreach ((CedarString key, ICedarData item) in record)
                {
                    writer.WritePropertyName(key.Value);
                    WriteValue(writer, item);
                }

                writer.WriteEndObject();
                break;
            case EntityUid entityUid:
                EntityUidJsonConverter.WriteExplicit(writer, entityUid);
                break;
            default:
                throw new JsonException($"Unsupported Cedar JSON type '{value.GetType().FullName}'.");
        }
    }

    private static CedarLong ReadLong(JsonElement element)
    {
        if (!element.TryGetInt64(out long value))
        {
            throw new JsonException("JSON numbers must fit in a signed 64-bit integer.");
        }

        return new CedarLong(value);
    }

    private static CedarSet ReadSet(JsonElement element)
    {
        List<ICedarData> values = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            values.Add(ReadElement(item));
        }

        return new CedarSet(values);
    }

    private static ICedarData ReadObject(JsonElement element)
    {
        // Only the explicit __entity escape form produces an EntityUid in schema-free context.
        // The implicit {"type":"X","id":"Y"} form requires schema-guided parsing.
        if (element.TryGetProperty("__entity", out _)
            && EntityUidJsonConverter.TryReadElement(element, out EntityUid? entityUid))
        {
            return entityUid!;
        }

        if (TryReadExtension(element, out ICedarData? extension))
        {
            return extension!;
        }

        RecordMap values = [];
        foreach (JsonProperty property in element.EnumerateObject())
        {
            values.Add(new CedarString(property.Name), ReadElement(property.Value));
        }

        return new CedarRecord(values);
    }

    private static bool TryReadExtension(JsonElement element, out ICedarData? value)
    {
        value = null;

        if (!TryGetExtensionElement(element, out JsonElement extension))
        {
            return false;
        }

        if (!extension.TryGetProperty("fn", out JsonElement functionElement) || functionElement.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("Extension values must contain a string fn property.");
        }

        if (!extension.TryGetProperty("arg", out JsonElement argumentElement) || argumentElement.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("Extension values must contain a string arg property.");
        }

        string function = functionElement.GetString()!;
        string argument = argumentElement.GetString()!;

        value = function switch
        {
            "decimal" => CedarDecimal.Parse(argument),
            "datetime" => CedarDatetime.Parse(argument),
            "duration" => CedarDuration.Parse(argument),
            "ip" => CedarIpAddress.Parse(argument),
            "pattern" => CedarPattern.Parse(argument),
            _ => throw new JsonException($"Unsupported Cedar extension '{function}'.")
        };

        return true;
    }

    private static bool TryGetExtensionElement(JsonElement element, out JsonElement extension)
    {
        if (element.TryGetProperty("__extn", out JsonElement explicitExtension))
        {
            if (explicitExtension.ValueKind == JsonValueKind.Object
                && explicitExtension.TryGetProperty("fn", out JsonElement explicitFunction)
                && explicitFunction.ValueKind == JsonValueKind.String
                && explicitExtension.TryGetProperty("arg", out JsonElement explicitArgument)
                && explicitArgument.ValueKind == JsonValueKind.String)
            {
                extension = explicitExtension;
                return true;
            }

            extension = default;
            return false;
        }

        if (element.TryGetProperty("fn", out JsonElement functionElement)
            && functionElement.ValueKind == JsonValueKind.String
            && element.TryGetProperty("arg", out JsonElement argumentElement)
            && argumentElement.ValueKind == JsonValueKind.String)
        {
            extension = element;
            return true;
        }

        extension = default;
        return false;
    }

    private static void WriteExtension(Utf8JsonWriter writer, string function, string argument)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("__extn");
        writer.WriteStartObject();
        writer.WriteString("fn", function);
        writer.WriteString("arg", argument);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}

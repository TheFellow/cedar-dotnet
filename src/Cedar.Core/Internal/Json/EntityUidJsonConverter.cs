using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cedar.Types;

namespace Cedar.Core.Internal.Json;

internal sealed class EntityUidJsonConverter : JsonConverter<EntityUid>
{
    public override EntityUid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        return ReadElement(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, EntityUid value, JsonSerializerOptions options)
    {
        WriteExplicit(writer, value);
    }

    internal static EntityUid ReadElement(JsonElement element)
    {
        if (!TryReadElement(element, out EntityUid? value))
        {
            throw new JsonException("Expected an EntityUid in implicit {type,id} or explicit {__entity:{type,id}} form.");
        }

        return value!;
    }

    internal static bool TryReadElement(JsonElement element, out EntityUid? value)
    {
        value = null;

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

        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!payload.TryGetProperty("type", out JsonElement typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (!payload.TryGetProperty("id", out JsonElement idElement) || idElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = new EntityUid(new EntityType(typeElement.GetString()!), new CedarString(idElement.GetString()!));
        return true;
    }

    internal static void WriteExplicit(Utf8JsonWriter writer, EntityUid value)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("__entity");
        WritePayload(writer, value);
        writer.WriteEndObject();
    }

    internal static void WriteImplicit(Utf8JsonWriter writer, EntityUid value)
    {
        WritePayload(writer, value);
    }

    private static void WritePayload(Utf8JsonWriter writer, EntityUid value)
    {
        writer.WriteStartObject();
        writer.WriteString("type", value.Type.Value);
        writer.WriteString("id", value.Id.Value);
        writer.WriteEndObject();
    }
}

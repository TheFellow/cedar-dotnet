using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cedar.Types;

namespace Cedar.Core.Internal.Json;

internal sealed class EntityJsonConverter : JsonConverter<Entity>
{
    public override Entity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Entity values must be JSON objects.");
        }

        if (!root.TryGetProperty("uid", out JsonElement uidElement))
        {
            throw new JsonException("Entity values must contain a uid property.");
        }

        EntityUid uid = EntityUidJsonConverter.ReadElement(uidElement);
        EntityUidSet parents = root.TryGetProperty("parents", out JsonElement parentsElement)
            ? ReadParents(parentsElement)
            : new EntityUidSet();
        CedarRecord attributes = root.TryGetProperty("attrs", out JsonElement attributesElement)
            ? ReadRecord(attributesElement, "attrs")
            : new CedarRecord();
        CedarRecord tags = root.TryGetProperty("tags", out JsonElement tagsElement)
            ? ReadRecord(tagsElement, "tags")
            : new CedarRecord();

        return new Entity(uid, parents, attributes, tags);
    }

    public override void Write(Utf8JsonWriter writer, Entity value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("uid");
        EntityUidJsonConverter.WriteImplicit(writer, value.Uid);

        writer.WritePropertyName("parents");
        writer.WriteStartArray();
        foreach (EntityUid parent in value.Parents.OrderBy(static item => item.Type.Value, StringComparer.Ordinal).ThenBy(static item => item.Id.Value, StringComparer.Ordinal))
        {
            EntityUidJsonConverter.WriteImplicit(writer, parent);
        }

        writer.WriteEndArray();

        writer.WritePropertyName("attrs");
        CedarValueJsonConverter.WriteValue(writer, value.Attributes);

        writer.WritePropertyName("tags");
        CedarValueJsonConverter.WriteValue(writer, value.Tags);

        writer.WriteEndObject();
    }

    private static EntityUidSet ReadParents(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Entity parents must be encoded as an array.");
        }

        return new EntityUidSet(element.EnumerateArray().Select(EntityUidJsonConverter.ReadElement));
    }

    private static CedarRecord ReadRecord(JsonElement element, string propertyName)
    {
        ICedarData value = CedarValueJsonConverter.ReadElement(element);
        return value as CedarRecord ?? throw new JsonException($"Entity {propertyName} must be a JSON object.");
    }
}

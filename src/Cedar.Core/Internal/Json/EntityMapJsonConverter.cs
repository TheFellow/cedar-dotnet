using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cedar.Types;

namespace Cedar.Core.Internal.Json;

internal sealed class EntityMapJsonConverter : JsonConverter<EntityMap>
{
    private readonly EntityJsonConverter _entityConverter = new();

    public override EntityMap Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Entity maps must be encoded as arrays.");
        }

        List<Entity> entities = [];

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return new EntityMap(entities);
            }

            entities.Add(_entityConverter.Read(ref reader, typeof(Entity), options));
        }

        throw new JsonException("Unexpected end of JSON while reading an entity map.");
    }

    public override void Write(Utf8JsonWriter writer, EntityMap value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (Entity entity in value)
        {
            _entityConverter.Write(writer, entity, options);
        }

        writer.WriteEndArray();
    }
}

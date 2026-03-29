using System.Text.Encodings.Web;
using System.Text.Json;
using Cedar.Core.Internal.Json;
using Cedar.Types;

namespace Cedar.Conformance;

internal static class ConformanceJson
{
    private static readonly JsonSerializerOptions EntityOptions = CreateEntityOptions();

    public static string SerializeEntityMap(EntityMap map)
    {
        return JsonSerializer.Serialize(map, EntityOptions);
    }

    public static EntityMap DeserializeEntityMap(string json)
    {
        return JsonSerializer.Deserialize<EntityMap>(json, EntityOptions)
            ?? throw new JsonException("Entity map JSON deserialized to null.");
    }

    private static JsonSerializerOptions CreateEntityOptions()
    {
        JsonSerializerOptions options = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        options.Converters.Add(new CedarValueJsonConverter());
        options.Converters.Add(new EntityUidJsonConverter());
        options.Converters.Add(new EntityJsonConverter());
        options.Converters.Add(new EntityMapJsonConverter());
        return options;
    }
}

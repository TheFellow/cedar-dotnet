using System.Text.Json;
using Cedar.Core.Internal.Json;
using Cedar.Types;

namespace Cedar.Tests.TestSupport;

internal static class CedarJson
{
    public static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new();
        options.Converters.Add(new CedarValueJsonConverter());
        options.Converters.Add(new EntityUidJsonConverter());
        options.Converters.Add(new EntityJsonConverter());
        options.Converters.Add(new EntityMapJsonConverter());
        return options;
    }

    public static string SerializeData(ICedarData value)
    {
        return JsonSerializer.Serialize<ICedarData>(value, CreateOptions());
    }

    public static ICedarData DeserializeData(string json)
    {
        return JsonSerializer.Deserialize<ICedarData>(json, CreateOptions())!;
    }

    public static string SerializeEntity(Entity entity)
    {
        return JsonSerializer.Serialize(entity, CreateOptions());
    }

    public static Entity DeserializeEntity(string json)
    {
        return JsonSerializer.Deserialize<Entity>(json, CreateOptions())!;
    }

    public static string SerializeEntityUid(EntityUid uid)
    {
        return JsonSerializer.Serialize(uid, CreateOptions());
    }

    public static EntityUid DeserializeEntityUid(string json)
    {
        return JsonSerializer.Deserialize<EntityUid>(json, CreateOptions())!;
    }

    public static string SerializeEntityMap(EntityMap map)
    {
        return JsonSerializer.Serialize(map, CreateOptions());
    }

    public static EntityMap DeserializeEntityMap(string json)
    {
        return JsonSerializer.Deserialize<EntityMap>(json, CreateOptions())!;
    }
}

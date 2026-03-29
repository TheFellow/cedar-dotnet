using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cedar.Core.Internal.Json;

internal static class PolicyJsonSerializerOptions
{
    internal static JsonSerializerOptions Instance { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        options.Converters.Add(new CedarValueJsonConverter());
        options.Converters.Add(new EntityUidJsonConverter());
        options.Converters.Add(new EntityJsonConverter());
        options.Converters.Add(new EntityMapJsonConverter());
        return options;
    }
}

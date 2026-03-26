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
            WriteIndented = false
        };

        options.Converters.Add(new CedarValueJsonConverter());
        return options;
    }
}

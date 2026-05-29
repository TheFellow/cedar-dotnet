using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cedar.Types;

public sealed class CedarPatternJsonConverter : JsonConverter<CedarPattern>
{
    public override CedarPattern Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        return CedarPattern.ReadJson(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, CedarPattern value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        value.WriteJson(writer);
    }
}

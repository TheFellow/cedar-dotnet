using System.Text.Json.Serialization;

namespace Cedar.Core;

public readonly record struct Position(
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("offset")] int Offset,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("column")] int Column);

using System;

namespace Cedar.Schema.Internal;

internal static class HumanToJsonConverter
{
    public static string ConvertCedarToJson(string cedarText, string filename = "")
    {
        ArgumentNullException.ThrowIfNull(cedarText);
        return SchemaDocument.UnmarshalCedar(cedarText, filename).MarshalJson();
    }

    public static string ConvertJsonToCedar(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return SchemaDocument.UnmarshalJson(json).MarshalCedar();
    }
}

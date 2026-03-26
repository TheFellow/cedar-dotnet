using System;
using System.Text.Json;
using Cedar.Ast.Internal;

namespace Cedar.Core.Internal.Json;

internal static class PolicyJsonUnmarshal
{
    internal static PolicyAst Unmarshal(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);

        PolicyJsonModel model = JsonSerializer.Deserialize<PolicyJsonModel>(json, PolicyJsonSerializerOptions.Instance)
            ?? throw new JsonException("Policy JSON deserialized to null.");

        return model.ToAst();
    }
}

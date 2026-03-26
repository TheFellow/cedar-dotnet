using System;
using System.Text.Json;
using Cedar.Ast.Internal;

namespace Cedar.Core.Internal.Json;

internal static class PolicyJsonMarshal
{
    internal static string Marshal(PolicyAst policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        PolicyJsonModel model = PolicyJsonModel.FromAst(policy);
        return JsonSerializer.Serialize(model, PolicyJsonSerializerOptions.Instance);
    }
}

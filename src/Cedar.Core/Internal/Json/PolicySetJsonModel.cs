using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cedar.Core.Internal.Json;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record PolicySetJsonModel
{
    [JsonPropertyName("staticPolicies")]
    public SortedDictionary<string, PolicyJsonModel> StaticPolicies { get; init; } = new(StringComparer.Ordinal);
}

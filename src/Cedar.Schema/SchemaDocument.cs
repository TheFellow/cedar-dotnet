using System;
using System.Collections.Generic;
using Cedar.Schema.Internal;

namespace Cedar.Schema;

public sealed record SchemaDocument
{
    public NamespaceDecl GlobalNamespace { get; init; } = new();

    public IReadOnlyDictionary<string, NamespaceDecl> Namespaces { get; init; } = new Dictionary<string, NamespaceDecl>(StringComparer.Ordinal);

    public static SchemaDocument UnmarshalCedar(string cedarText, string filename = "")
    {
        ArgumentNullException.ThrowIfNull(cedarText);
        return SchemaParser.Parse(cedarText, filename);
    }

    public static SchemaDocument UnmarshalJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return SchemaJsonConverter.Deserialize(json);
    }

    public string MarshalCedar()
    {
        if (IsEmpty())
        {
            throw new InvalidOperationException("Schema is empty.");
        }

        return SchemaWriter.Write(this);
    }

    private bool IsEmpty()
    {
        return Namespaces.Count == 0
            && GlobalNamespace.Entities.Count == 0
            && GlobalNamespace.Enums.Count == 0
            && GlobalNamespace.Actions.Count == 0
            && GlobalNamespace.CommonTypes.Count == 0;
    }

    public string MarshalJson()
    {
        return SchemaJsonConverter.Serialize(this);
    }

    public ResolvedSchema Resolve()
    {
        return SchemaResolver.Resolve(this);
    }

    public override string ToString()
    {
        return MarshalCedar();
    }
}

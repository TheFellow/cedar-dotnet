using System;
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema;

public abstract record ResolvedType;

public sealed record ResolvedStringType : ResolvedType;

public sealed record ResolvedLongType : ResolvedType;

public sealed record ResolvedBoolType : ResolvedType;

public sealed record ResolvedExtensionType(Ident Name) : ResolvedType;

public sealed record ResolvedSetType(ResolvedType Element) : ResolvedType;

public sealed record ResolvedEntityType(EntityType Name) : ResolvedType;

public sealed record ResolvedRecordType : ResolvedType
{
    public IReadOnlyDictionary<string, ResolvedAttribute> Attributes { get; init; }
        = new Dictionary<string, ResolvedAttribute>(StringComparer.Ordinal);
}

public sealed record ResolvedAttribute
{
    public required ResolvedType Type { get; init; }

    public bool Optional { get; init; }

    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; }
        = Array.Empty<SchemaAnnotation>();
}

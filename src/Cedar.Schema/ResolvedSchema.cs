using System;
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema;

public sealed record ResolvedNamespace(string Name)
{
    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; }
        = Array.Empty<SchemaAnnotation>();
}

public sealed record ResolvedEntity
{
    public required EntityType Name { get; init; }

    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; }
        = Array.Empty<SchemaAnnotation>();

    public IReadOnlyList<EntityType> ParentTypes { get; init; }
        = Array.Empty<EntityType>();

    public ResolvedRecordType Shape { get; init; } = new();

    public ResolvedType? Tags { get; init; }
}

public sealed record ResolvedEnum
{
    public required EntityType Name { get; init; }

    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; }
        = Array.Empty<SchemaAnnotation>();

    public IReadOnlyList<EntityUid> Values { get; init; }
        = Array.Empty<EntityUid>();
}

public sealed record ResolvedAppliesTo
{
    public IReadOnlyList<EntityType> Principals { get; init; }
        = Array.Empty<EntityType>();

    public IReadOnlyList<EntityType> Resources { get; init; }
        = Array.Empty<EntityType>();

    public ResolvedRecordType Context { get; init; } = new();
}

public sealed record ResolvedAction
{
    public required Entity Entity { get; init; }

    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; } = Array.Empty<SchemaAnnotation>();

    public ResolvedAppliesTo? AppliesTo { get; init; }
}

public sealed record ResolvedSchema
{
    public IReadOnlyDictionary<EntityUid, ResolvedAction> Actions { get; init; } = new Dictionary<EntityUid, ResolvedAction>();

    public IReadOnlyDictionary<EntityType, ResolvedEntity> Entities { get; init; } = new Dictionary<EntityType, ResolvedEntity>();

    public IReadOnlyDictionary<EntityType, ResolvedEnum> Enums { get; init; } = new Dictionary<EntityType, ResolvedEnum>();

    public IReadOnlyDictionary<string, ResolvedNamespace> Namespaces { get; init; } = new Dictionary<string, ResolvedNamespace>(StringComparer.Ordinal);
}

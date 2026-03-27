using System;
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema;

public sealed record ResolvedAction
{
    public required Entity Entity { get; init; }

    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; } = Array.Empty<SchemaAnnotation>();

    public AppliesToDecl? AppliesTo { get; init; }
}

public sealed record ResolvedSchema
{
    public IReadOnlyDictionary<EntityUid, ResolvedAction> Actions { get; init; } = new Dictionary<EntityUid, ResolvedAction>();
}
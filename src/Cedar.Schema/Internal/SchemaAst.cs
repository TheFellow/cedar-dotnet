using System;
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema;

public sealed record NamespaceDecl
{
    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; } = Array.Empty<SchemaAnnotation>();

    public IReadOnlyDictionary<Ident, EntityDecl> Entities { get; init; } = new Dictionary<Ident, EntityDecl>();

    public IReadOnlyDictionary<Ident, EnumDecl> Enums { get; init; } = new Dictionary<Ident, EnumDecl>();

    public IReadOnlyDictionary<string, ActionDecl> Actions { get; init; } = new Dictionary<string, ActionDecl>(StringComparer.Ordinal);

    public IReadOnlyDictionary<Ident, CommonTypeDecl> CommonTypes { get; init; } = new Dictionary<Ident, CommonTypeDecl>();
}

public sealed record SchemaAnnotation(Ident Key, string Value);

public sealed record CommonTypeDecl
{
    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; } = Array.Empty<SchemaAnnotation>();

    public required SchemaType Type { get; init; }
}

public sealed record EntityDecl
{
    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; } = Array.Empty<SchemaAnnotation>();

    public IReadOnlyList<EntityType> ParentTypes { get; init; } = Array.Empty<EntityType>();

    public RecordType? Shape { get; init; }

    public SchemaType? Tags { get; init; }
}

public sealed record EnumDecl
{
    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; } = Array.Empty<SchemaAnnotation>();

    public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();
}

public sealed record ActionDecl
{
    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; } = Array.Empty<SchemaAnnotation>();

    public IReadOnlyList<ParentRef> Parents { get; init; } = Array.Empty<ParentRef>();

    public AppliesToDecl? AppliesTo { get; init; }
}

public sealed record AppliesToDecl
{
    public IReadOnlyList<EntityType> Principals { get; init; } = Array.Empty<EntityType>();

    public IReadOnlyList<EntityType> Resources { get; init; } = Array.Empty<EntityType>();

    public SchemaType? Context { get; init; }
}

public sealed record ParentRef(EntityType? Type, string Id);

public sealed record AttributeDecl
{
    public required SchemaType Type { get; init; }

    public bool Optional { get; init; }

    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; } = Array.Empty<SchemaAnnotation>();
}

public abstract record SchemaType;

public sealed record StringType : SchemaType;

public sealed record LongType : SchemaType;

public sealed record BoolType : SchemaType;

public sealed record ExtensionType(Ident Name) : SchemaType;

public sealed record SetType(SchemaType Element) : SchemaType;

public sealed record RecordType : SchemaType
{
    public IReadOnlyDictionary<string, AttributeDecl> Attributes { get; init; } = new Dictionary<string, AttributeDecl>(StringComparer.Ordinal);
}

public sealed record EntityTypeRef(EntityType Name) : SchemaType;

public sealed record TypeRef(string Name) : SchemaType;

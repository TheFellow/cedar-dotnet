# Sprint 010: Schema Validation

## 1. Overview

Port the cedar-go schema validation package (`inspiration/cedar-go/x/exp/schema/validate/`, ~3,250 lines across 10 files) plus the resolved schema expansion (`inspiration/cedar-go/x/exp/schema/resolved/`, ~634 lines across 2 files) into the existing `Cedar.Schema` assembly. The Go package provides four validation surfaces -- policy typechecking, entity validation, request validation, and capability tracking -- all built atop a fully resolved schema with entities, enums, namespaces, and inlined common types.

The existing C# `ResolvedSchema` is skeletal: it resolves only `Actions` (with raw `AppliesToDecl`). The Go `resolved.Schema` has `Entities` (with `ParentTypes`, `Shape`, `Tags`), `Enums` (with resolved `EntityUID` values), `Namespaces`, and `Actions` (with fully resolved `AppliesTo` including `Context` as a resolved `RecordType`). Phase 1 closes this gap. Phases 2-4 build the validator itself. Phase 5 wires corpus conformance tests and hardens the semport pipeline.

All new code lives in `src/Cedar.Schema/` under `namespace Cedar.Schema` (public API) and `namespace Cedar.Schema.Internal.Validate` (type system, typechecker internals). No new assembly is created. The existing `ResolvedSchema.Actions` API remains backward-compatible; new properties are additive.

### Sizing

| Go File | Lines | C# Estimated Lines | Phase |
|---------|------:|--------------------:|-------|
| `resolved/types.go` | 63 | ~80 | 1 |
| `resolved/resolve.go` | 571 | ~700 | 1 |
| `check_value.go` | 123 | ~150 | 2 |
| `entity.go` | 124 | ~160 | 2 |
| `request.go` | 91 | ~110 | 2 |
| `cedar_type.go` | 582 | ~650 | 3 |
| `capability.go` | 52 | ~60 | 3 |
| `ext_funcs.go` | 43 | ~50 | 3 |
| `request_env.go` | 70 | ~80 | 3 |
| `typechecker.go` | 1,547 | ~1,800 | 4 |
| `policy.go` | 448 | ~500 | 4 |
| `validator.go` | 38 | ~50 | 4 |
| **Total** | **3,752** | **~4,390** | |

### Reference Go files and C# counterparts

| Go File | C# Target |
|---------|-----------|
| `resolved/types.go` | `src/Cedar.Schema/ResolvedTypes.cs` |
| `resolved/resolve.go` | `src/Cedar.Schema/SchemaResolver.cs` (expand) |
| `validate/validator.go` | `src/Cedar.Schema/SchemaValidator.cs` |
| `validate/entity.go` | `src/Cedar.Schema/SchemaValidator.Entity.cs` |
| `validate/request.go` | `src/Cedar.Schema/SchemaValidator.Request.cs` |
| `validate/policy.go` | `src/Cedar.Schema/Internal/Validate/PolicyValidator.cs` + `ScopeValidator.cs` |
| `validate/typechecker.go` | `src/Cedar.Schema/Internal/Validate/TypeChecker.cs` |
| `validate/cedar_type.go` | `src/Cedar.Schema/Internal/Validate/CedarType.cs` + `CedarTypeOps.cs` |
| `validate/capability.go` | `src/Cedar.Schema/Internal/Validate/CapabilitySet.cs` |
| `validate/ext_funcs.go` | `src/Cedar.Schema/Internal/Validate/ExtensionFunctions.cs` |
| `validate/request_env.go` | `src/Cedar.Schema/Internal/Validate/RequestEnvironment.cs` |
| `validate/check_value.go` | `src/Cedar.Schema/Internal/Validate/ValueChecker.cs` |

## 2. Use Cases

1. **Policy validation before deployment.** A consumer calls `validator.ValidatePolicy(policyId, policy)` to catch type errors, unrecognized entity types, action applicability failures, and empty set literals (strict mode) before deploying policies to a PDP.

2. **Entity conformance at ingest time.** A consumer calls `validator.ValidateEntity(entity)` or `validator.ValidateEntities(entityMap)` to verify that entities match their schema-declared shapes, parent types, and tag types before persisting them to an entity store.

3. **Request validation at authorization time.** A consumer calls `validator.ValidateRequest(request)` to confirm that the principal type, resource type, and context record are valid for the specified action before issuing an authorization call.

4. **Strict vs. permissive modes.** Strict mode (default) reports additional diagnostics such as empty set literals in action scopes. Permissive mode mirrors Cedar's permissive validation behavior and skips condition typechecking when action applicability already failed.

5. **Corpus conformance.** `CorpusValidationTests` asserts pass/fail parity with Rust's validation output for all corpus scenarios, covering policy, entity, and request validation in both modes.

6. **Full schema resolution.** `SchemaDocument.Resolve()` returns a `ResolvedSchema` with `Entities`, `Enums`, `Namespaces`, and `Actions` (with resolved `AppliesTo` including a resolved context `ResolvedRecordType`), enabling downstream consumers beyond just validation.

## 3. Architecture

### 3.1 Resolved Type Hierarchy (`src/Cedar.Schema/ResolvedTypes.cs`)

The Go `resolved.IsType` interface is represented as a sealed abstract record hierarchy. This gives structural equality for free, pattern matching with exhaustiveness warnings in switch expressions, and natural immutability. The resolver allocates resolved types once during `Resolve()` and they are thereafter read-only.

**Design decision:** No `TypeRef` in the resolved output -- common types are inlined during resolution, so the validator never chases unresolved references. No `EntityTypeRef` -- replaced by `ResolvedEntityType(EntityType Name)` with a fully-qualified `EntityType`. The name uses `ResolvedType` (not `ResolvedSchemaType`) to avoid redundancy in `Cedar.Schema.ResolvedSchemaType`, and `ResolvedEntityType` (not `ResolvedEntityRefType`) because the whole point of resolution is that references are gone.

```csharp
using System;
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema;

// --- Resolved type hierarchy (mirrors Go resolved.IsType) ---

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
```

**Key property:** `ResolvedRecordType` uses `string` keys (not `CedarString`) because attribute names in schemas are plain strings. This is a deliberate deviation from Go's `types.String` keys, matching the existing `AttributeDecl` pattern in `SchemaAst.cs`. The `ValueChecker` converts `CedarString` keys to `string` during comparison.

### 3.2 ResolvedSchema Expansion (`src/Cedar.Schema/ResolvedSchema.cs`)

```csharp
using System;
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema;

// --- Resolved entity/enum/namespace/action types ---

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
    public ResolvedType? Tags { get; init; }  // null = tags not allowed
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

// --- Updated ResolvedAction ---
// AppliesTo changes from AppliesToDecl? to ResolvedAppliesTo?.
// Codebase audit: only SchemaResolver and conformance test structural
// assertions reference AppliesToDecl -- both are modified in this sprint.
public sealed record ResolvedAction
{
    public required Entity Entity { get; init; }
    public IReadOnlyList<SchemaAnnotation> Annotations { get; init; }
        = Array.Empty<SchemaAnnotation>();
    public ResolvedAppliesTo? AppliesTo { get; init; }
}

// --- Expanded ResolvedSchema ---
public sealed record ResolvedSchema
{
    public IReadOnlyDictionary<EntityUid, ResolvedAction> Actions { get; init; }
        = new Dictionary<EntityUid, ResolvedAction>();
    public IReadOnlyDictionary<EntityType, ResolvedEntity> Entities { get; init; }
        = new Dictionary<EntityType, ResolvedEntity>();
    public IReadOnlyDictionary<EntityType, ResolvedEnum> Enums { get; init; }
        = new Dictionary<EntityType, ResolvedEnum>();
    public IReadOnlyDictionary<string, ResolvedNamespace> Namespaces { get; init; }
        = new Dictionary<string, ResolvedNamespace>(StringComparer.Ordinal);
}
```

**Note:** `AppliesToDecl` itself (defined in `src/Cedar.Schema/Internal/SchemaAst.cs`) is not removed -- it remains used by `ActionDecl.AppliesTo` in the schema AST. Only `ResolvedAction.AppliesTo` changes type.

### 3.3 Project References and InternalsVisibleTo

```
Cedar.Schema.csproj
  <ProjectReference> Cedar.Types  (existing)
  <ProjectReference> Cedar.Ast    (NEW)
```

**Critical:** `Cedar.Schema` needs a project reference to `Cedar.Ast` only (not separately to `Cedar.Core`). The reason: `Policy.cs`, `PolicySet.cs`, `Request.cs`, and related types are compiled into `Cedar.Ast` via the linked-source pattern (`Cedar.Ast.csproj` includes files from `Cedar.Core/` via `<Compile Include>` directives, and `Cedar.Core.csproj` removes those same files via `<Compile Remove>`). Adding references to *both* `Cedar.Core` and `Cedar.Ast` would cause type identity conflicts since `Policy` would exist in both assemblies. `Cedar.Ast` transitively brings `Cedar.Types`.

`Cedar.Ast` must grant `InternalsVisibleTo("Cedar.Schema")` so the validator can access `Policy.Ast` (which is `internal`), `PolicyAst`, `INode`, scope types, and node types:

- `src/Cedar.Ast/Properties/InternalsVisibleTo.cs` -- add `[assembly: InternalsVisibleTo("Cedar.Schema")]`
- `src/Cedar.Ast/Cedar.Ast.csproj` -- add `<InternalsVisibleTo Include="Cedar.Schema" />`

**Direction:** `Cedar.Ast` grants visibility *to* `Cedar.Schema`. Not the reverse. `Cedar.Ast` has no project reference to `Cedar.Schema`.

### 3.4 Internal CedarType Hierarchy (`src/Cedar.Schema/Internal/Validate/CedarType.cs`)

Used only by the typechecker, not exposed publicly. The Go `cedarType` interface is a 10-variant sum type. It is **not** the same as the resolved type hierarchy -- it adds `Never`, `True`, `False` singleton types and replaces `EntityType` with `EntityLub` (a sorted list of entity types representing the least upper bound).

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Cedar.Types;

namespace Cedar.Schema.Internal.Validate;

internal abstract record CedarType;

internal sealed record CedarNever     : CedarType;      // bottom type
internal sealed record CedarTrue      : CedarType;      // singleton true
internal sealed record CedarFalse     : CedarType;      // singleton false
internal sealed record CedarBool      : CedarType;      // Bool
internal sealed record CedarLong      : CedarType;      // Long
internal sealed record CedarString    : CedarType;      // String
internal sealed record CedarSetType(CedarType Element) : CedarType;
internal sealed record CedarExtType(Ident Name)        : CedarType;

internal sealed record CedarEntityType(EntityLub Lub) : CedarType;
internal sealed record CedarRecordType(
    IReadOnlyDictionary<string, CedarAttr> Attrs) : CedarType;

// Supporting types
internal readonly record struct CedarAttr(CedarType Type, bool Required);

// EntityLub must have structural equality. Override Equals/GetHashCode
// to compare Elements by sequence, not by reference.
internal sealed class EntityLub : IEquatable<EntityLub>
{
    // Elements must be sorted and unique for deterministic equality
    public ImmutableArray<EntityType> Elements { get; }

    public EntityLub(ImmutableArray<EntityType> elements) => Elements = elements;

    public static EntityLub Single(EntityType et)
        => new(ImmutableArray.Create(et));

    public EntityLub Union(EntityLub other)
    {
        // merge-sort, deduplicate -- implementation in CedarTypeOps
        throw new NotImplementedException();
    }

    public bool IsDisjoint(EntityLub other)
    {
        // sorted intersection check -- implementation in CedarTypeOps
        throw new NotImplementedException();
    }

    public bool Equals(EntityLub? other)
    {
        if (other is null) return false;
        return Elements.SequenceEqual(other.Elements);
    }

    public override bool Equals(object? obj) => Equals(obj as EntityLub);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var e in Elements) hash.Add(e);
        return hash.ToHashCode();
    }
}
```

**Note on CedarType naming:** The internal type names (`CedarNever`, `CedarTrue`, etc.) avoid the `CedarType` prefix to reduce verbosity in switch expressions. They live under `namespace Cedar.Schema.Internal.Validate` and are `internal`, so there is no conflict with the public resolved type names.

### 3.5 SchemaValidator Entry Point (`src/Cedar.Schema/SchemaValidator.cs`)

```csharp
using System;
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema;

public enum ValidationMode { Strict, Permissive }

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success { get; }
        = new(true, Array.Empty<string>());

    public static ValidationResult Failure(params string[] errors)
        => new(false, errors);

    public static ValidationResult Failure(IReadOnlyList<string> errors)
        => new(false, errors);
}

public sealed partial class SchemaValidator
{
    private readonly ResolvedSchema _schema;
    private readonly bool _strict;

    public SchemaValidator(ResolvedSchema schema,
        ValidationMode mode = ValidationMode.Strict)
    {
        ArgumentNullException.ThrowIfNull(schema);
        _schema = schema;
        _strict = mode == ValidationMode.Strict;
    }

    public ValidationResult ValidatePolicy(string policyId, Policy policy);
    public ValidationResult ValidateEntity(Entity entity);
    public ValidationResult ValidateEntities(EntityMap entities);
    public ValidationResult ValidateRequest(Request request);

    internal bool IsKnownEntityType(EntityType entityType)
    {
        return _schema.Entities.ContainsKey(entityType)
            || _schema.Enums.ContainsKey(entityType);
    }
}
```

### 3.6 Dependency Graph

```
Cedar.Types  <--  Cedar.Core  <--  Cedar.Ast
                                      |
                                      v (InternalsVisibleTo)
                                  Cedar.Schema
                                  (validator lives here)
```

No circular dependency: `Types <- Core <- Ast` (linked source), `Types <- Ast <- Schema` (project reference). The `Ast -> Schema` direction is IVT only (compile-time friend access), not a project reference. `Cedar.Schema` references `Cedar.Ast` which transitively provides `Cedar.Types` and the types compiled from `Cedar.Core`.

### 3.7 File Layout

```
src/Cedar.Schema/
    ResolvedSchema.cs              -- expanded (add Entities, Enums, Namespaces, ResolvedAppliesTo)
    ResolvedTypes.cs               -- new: ResolvedType hierarchy + ResolvedAttribute
    SchemaResolver.cs              -- expanded (5-phase resolver with ResolverState)
    SchemaValidator.cs             -- new: public SchemaValidator class + ValidationResult + ValidationMode
    SchemaValidator.Entity.cs      -- new: partial class, entity/entities validation
    SchemaValidator.Request.cs     -- new: partial class, request validation
    Internal/
        Validate/
            CedarType.cs           -- internal type system (10 variants + EntityLub)
            CedarTypeOps.cs        -- LUB, subtype, name formatting, comparison, conversion
            CapabilitySet.cs       -- has-guard tracking
            ExtensionFunctions.cs  -- 23 extension func signatures
            RequestEnvironment.cs  -- env generation + filtering
            TypeChecker.cs         -- expression type inference (28+ node dispatch)
            ValueChecker.cs        -- runtime value conformance
            ScopeValidator.cs      -- principal/action/resource scope validation
            PolicyValidator.cs     -- orchestrates scope + typecheck + error merge
```

## 4. Implementation

### Phase 1: Expand ResolvedSchema

**Goal:** `SchemaDocument.Resolve()` produces a `ResolvedSchema` with `Entities`, `Enums`, `Namespaces`, and `Actions` with fully resolved `AppliesTo` (including resolved context `ResolvedRecordType`). Additionally, investigate the `NodeAccess.Attribute` type discrepancy.

**Task 0 -- Investigate `NodeAccess.Attribute` (MANDATORY before Phase 4)**

The C# AST defines `NodeAccess(INode Arg, INode Attribute)` where the attribute is an `INode`. The Go `NodeTypeAccess` uses `StrOpNode` with a plain `Value types.String` field. This is the single highest-risk implementation detail in the sprint.

Investigation steps:
1. Read `src/Cedar.Ast/Internal/CedarParser.cs` (or equivalent parser code) to determine whether `NodeAccess.Attribute` is always constructed as `NodeValue(CedarString(...))`.
2. Read `src/Cedar.Ast/Internal/Operators.cs` to check if `AccessNode(Node lhs, Node attributeExpr)` accepts arbitrary `Node` expressions.
3. Check `src/Cedar.Ast/Internal/NodeJsonModel.cs` to see how the JSON serializer handles `NodeAccess.Attribute`.
4. Document findings: if `Attribute` is always a `NodeValue(CedarString)` at parse time, the typechecker pattern-matches on it and emits a diagnostic error for non-literal cases. If it can be a computed expression, the typechecker design must accommodate dynamic attribute lookup.
5. Record the decision as a comment in `src/Cedar.Schema/Internal/Validate/TypeChecker.cs` once created.

**Task 1 -- Create `src/Cedar.Schema/ResolvedTypes.cs`**

Define the `ResolvedType` sealed abstract record hierarchy as specified in section 3.1: `ResolvedStringType`, `ResolvedLongType`, `ResolvedBoolType`, `ResolvedExtensionType`, `ResolvedSetType`, `ResolvedEntityType`, `ResolvedRecordType`, and `ResolvedAttribute`.

**Task 2 -- Update `src/Cedar.Schema/ResolvedSchema.cs`**

Add `ResolvedEntity`, `ResolvedEnum`, `ResolvedNamespace`, `ResolvedAppliesTo` records. Add `Entities`, `Enums`, `Namespaces` properties to `ResolvedSchema`. Change `ResolvedAction.AppliesTo` from `AppliesToDecl?` to `ResolvedAppliesTo?`.

**Task 3 -- Rewrite `src/Cedar.Schema/SchemaResolver.cs`**

The current `SchemaResolver` is a `public static class` with a `public static ResolvedSchema Resolve(SchemaDocument document)` method. It implements only action resolution and action membership validation (2 of 5 phases).

**Migration strategy:** Keep `SchemaResolver` as a `public static class` with the same `Resolve()` facade. Introduce a `private sealed class ResolverState` that holds mutable state and is created within `Resolve()`:

```csharp
public static class SchemaResolver
{
    public static ResolvedSchema Resolve(SchemaDocument document)
    {
        var state = new ResolverState();
        state.RegisterDeclarations(document);
        state.CheckShadowing(document);
        state.DetectCommonTypeCycles();
        state.ResolveAllDeclarations(document);
        state.ValidateActionMembership();
        return state.BuildResult();
    }

    private sealed class ResolverState
    {
        public HashSet<EntityType> EntityTypes { get; } = new();
        public HashSet<EntityType> EnumTypes { get; } = new();
        public Dictionary<string, SchemaType> CommonTypes { get; }
            = new(StringComparer.Ordinal);
        // ... resolved output accumulators ...
    }
}
```

Port the Go `resolve.go` 5-phase pipeline:

- **Phase 1 -- Registration.** `RegisterDeclarations(SchemaDocument doc)`: Walk global namespace and all named namespaces. Build entity types set, enum types set, and common types dictionary. Detect duplicate entity/enum declarations.
- **Phase 2 -- Shadowing check.** `CheckShadowing(SchemaDocument doc)`: Reject namespaced declarations that shadow bare (empty-namespace) declarations per RFC 70. Port Go's `checkShadowing()`.
- **Phase 3 -- Cycle detection.** `DetectCommonTypeCycles()`: Build a dependency graph from `TypeRef` references in common types. Topological sort via Kahn's algorithm. Error on cycle with participating type name.
- **Phase 4 -- Resolve all declarations.**
  - `ResolveEntities(string nsName, IReadOnlyDictionary<Ident, EntityDecl> entities)`: resolve parent types via `ResolveEntityTypeRef()`, shape via `ResolveRecordType()`, tags via `ResolveType()`.
  - `ResolveEnums(string nsName, IReadOnlyDictionary<Ident, EnumDecl> enums)`: build `ResolvedEnum` with qualified names and `EntityUid` values.
  - `ResolveActions(string nsName, IReadOnlyDictionary<string, ActionDecl> actions)`: expand existing action resolution to populate `ResolvedAppliesTo` with resolved principal types, resource types, and context as `ResolvedRecordType`. Port Go's context handling.
  - `ResolveNamespaces(SchemaDocument doc)`: build `ResolvedNamespace` entries with annotations.
- **Phase 5 -- Action membership validation.** Keep existing `ValidateActionMembership()` logic. DFS cycle detection on action hierarchy.

Key type-resolution methods on `ResolverState`:
- `ResolvedType ResolveType(string nsName, SchemaType type)` -- dispatches on `SchemaType` subtype (recursive).
- `ResolvedRecordType ResolveRecordType(string nsName, RecordType record)` -- resolves each attribute's type.
- `EntityType ResolveEntityTypeRef(string nsName, EntityTypeRef ref)` -- tries `NS::Name`, then bare `Name`.
- `ResolvedType ResolveTypeRef(string nsName, TypeRef ref)` -- 6-step Cedar disambiguation: NS::N common type, NS::N entity type, bare N common type, bare N entity type, `__cedar::` prefix, built-in type, error.

**Implementation note (linked-source pattern):** The `Cedar.Core`/`Cedar.Ast` linked-source pattern means that types like `Policy`, `PolicySet`, and `Request` are compiled into `Cedar.Ast`, not `Cedar.Core`. The `Cedar.Schema.csproj` must reference only `Cedar.Ast` (which transitively provides `Cedar.Types`). Do NOT add a separate reference to `Cedar.Core` -- this would cause type identity conflicts.

**Task 4 -- Add project reference**

Update `src/Cedar.Schema/Cedar.Schema.csproj`: add `<ProjectReference>` to `src/Cedar.Ast/Cedar.Ast.csproj`. Verify no circular dependency. Verify `dotnet build cedar-dotnet.sln` produces zero warnings.

**Task 5 -- Write unit tests** in `test/Cedar.Schema.Tests/SchemaResolverTests.cs`:
- Entity resolution with parent types from same and different namespaces.
- Enum resolution with qualified names and UID values.
- Common type inlining (TypeRef resolution) and cycle detection error.
- Shadowing rejection per RFC 70.
- Namespace-qualified vs. bare type resolution.
- Extension type resolution (`ipaddr`, `decimal`, `datetime`, `duration`).
- Action context resolution from `ContextRecord` and `ContextPath`.
- Multi-namespace schema with cross-namespace entity references.
- Backward compatibility: existing action resolution tests still pass.

**Estimated test count:** 15-20 new tests.

**Definition of Done:**
- `SchemaDocument.Resolve()` populates `Entities`, `Enums`, `Namespaces` on the returned `ResolvedSchema`.
- `ResolvedAction.AppliesTo` is `ResolvedAppliesTo?` with resolved `Context` as `ResolvedRecordType`.
- `SchemaResolver` implements all 5 Go resolver phases via `ResolverState`.
- `ResolveTypeRef` follows the 6-step disambiguation rules.
- Common type cycles and shadowing violations produce clear error messages.
- `NodeAccess.Attribute` investigation is complete with documented findings.
- All existing tests pass (`dotnet test cedar-dotnet.sln` -- zero failures, zero warnings).
- New unit tests cover entity/enum/common type resolution, shadowing, and cycle detection.

**Recommended commit decomposition:**
1. Commit 1a: `ResolvedTypes.cs` + `ResolvedSchema.cs` expansion (types only, no resolver changes). Must pass `dotnet build`.
2. Commit 1b: Resolver phases 1-3 (registration, shadowing, cycles). Must pass `dotnet build` + `dotnet test`.
3. Commit 1c: Resolver phases 4-5 (entity/enum/action resolution, membership validation) + unit tests. Must pass full suite.
4. Commit 1d: `NodeAccess.Attribute` investigation findings documented.

---

### Phase 2: Entity & Request Validation

**Goal:** `SchemaValidator.ValidateEntity()`, `ValidateEntities()`, and `ValidateRequest()` work correctly.

**Task 1 -- Create `src/Cedar.Schema/Internal/Validate/ValueChecker.cs`** porting `check_value.go`:

```csharp
using System;
using Cedar.Types;

namespace Cedar.Schema.Internal.Validate;

internal static class ValueChecker
{
    internal static (bool IsDeserError, string? Error)
        CheckValue(ICedarData value, ResolvedType expected);

    internal static (bool IsDeserError, string? Error)
        CheckRecord(CedarRecord record, ResolvedRecordType expected);

    internal static (bool IsDeserError, string? Error)
        CheckExtensionValue(ICedarData value, ResolvedExtensionType expected);
}
```

Type dispatch: `ResolvedStringType` -> expect `CedarString`, `ResolvedLongType` -> expect `CedarLong`, `ResolvedBoolType` -> expect `CedarBool`, `ResolvedEntityType` -> expect `EntityUid` with matching type, `ResolvedSetType` -> expect `CedarSet` with recursive element check, `ResolvedRecordType` -> expect `CedarRecord` with `CheckRecord`, `ResolvedExtensionType` -> `CheckExtensionValue` (`ipaddr` -> `CedarIpAddress`, `decimal` -> `CedarDecimal`, `datetime` -> `CedarDatetime`, `duration` -> `CedarDuration`).

Error classification: deserialization errors (structural type mismatch, e.g., expected Record got Long) vs. conformance errors (semantic mismatch, e.g., wrong entity type). Port Go's `entityDeserError` pattern.

`CheckRecord`: verify required attributes present, no unexpected attributes (closed record), each attribute value matches its declared type recursively.

**Task 2 -- Create `src/Cedar.Schema/SchemaValidator.cs`** with constructor, `IsKnownEntityType`, and `ValidationResult`/`ValidationMode` as shown in section 3.5.

**Task 3 -- Create `src/Cedar.Schema/SchemaValidator.Entity.cs`** (partial class) porting `entity.go`:

```csharp
namespace Cedar.Schema;

public sealed partial class SchemaValidator
{
    public ValidationResult ValidateEntity(Entity entity);
    public ValidationResult ValidateEntities(EntityMap entities);
    private ValidationResult ValidateActionEntity(Entity entity);
    private ValidationResult ValidateRegularEntity(
        Entity entity, ResolvedEntity schemaEntity);

    private static bool IsActionEntityType(EntityType type);
}
```

- `ValidateEntity`: check if action entity type -> `ValidateActionEntity`; check `_schema.Entities` -> `ValidateRegularEntity`; check `_schema.Enums` -> accept (return Success, matching Go behavior); otherwise -> error ("unknown type").
- `ValidateActionEntity`: verify action UID exists in `_schema.Actions`, no attributes, no tags, parent transitive closure matches schema.
- `ValidateRegularEntity`: verify each parent's type is in `schemaEntity.ParentTypes`, attributes conform via `ValueChecker.CheckRecord(entity.Attributes, schemaEntity.Shape)`, tags conform to `schemaEntity.Tags` type (if declared). If `schemaEntity.Tags` is null and `entity.Tags.Count > 0`, return a deserialization error.
- `ValidateEntities`: iterate, return first failure with Rust-compatible message ("entity does not conform to the schema" for conformance errors, "error during entity deserialization" for structural errors).
- `IsActionEntityType`: returns true if the type string equals `"Action"` or contains `"::Action"`.

**Task 4 -- Create `src/Cedar.Schema/SchemaValidator.Request.cs`** (partial class) porting `request.go`:

```csharp
namespace Cedar.Schema;

public sealed partial class SchemaValidator
{
    public ValidationResult ValidateRequest(Request request);
    private ValidationResult ValidateRequestEntityType(
        EntityUid uid, string role);
}
```

- Validate action exists in `_schema.Actions`.
- Validate principal type is known and in `action.AppliesTo.Principals`.
- Validate resource type is known and in `action.AppliesTo.Resources`.
- Validate context via `ValueChecker.CheckRecord(request.Context, action.AppliesTo.Context)`.
- Handle null `AppliesTo`: Go's `Action.AppliesTo` is a pointer -- `nil` means "no applicability constraint." Match this: if `ResolvedAppliesTo` is null, skip principal/resource/context validation.

**Task 5 -- Add InternalsVisibleTo**

Add `[assembly: InternalsVisibleTo("Cedar.Schema")]` to `src/Cedar.Ast/Properties/InternalsVisibleTo.cs` and `<InternalsVisibleTo Include="Cedar.Schema" />` to `src/Cedar.Ast/Cedar.Ast.csproj`.

**Task 6 -- Write unit tests:**
- `test/Cedar.Schema.Tests/ValueCheckerTests.cs`: String/Long/Bool/EntityUid/Set/Record/Extension type matching; missing required attribute; unexpected attribute; recursive set element checking; deserialization vs conformance error distinction.
- `test/Cedar.Schema.Tests/SchemaValidatorEntityTests.cs`: Action entity parent closure; action entity with unexpected attributes; regular entity valid; regular entity missing attribute; regular entity wrong parent type; regular entity with tags when not allowed; enum entity accepted; unknown entity type; entity with unknown type returns deserialization error.
- `test/Cedar.Schema.Tests/SchemaValidatorRequestTests.cs`: Valid request; unknown action; principal type not in AppliesTo; resource type not in AppliesTo; invalid context shape; unknown principal type; action with null AppliesTo.

**Estimated test count:** 30-40 new tests.

**Definition of Done:**
- `ValidateEntity`, `ValidateEntities`, `ValidateRequest` return correct `ValidationResult` for valid and invalid inputs.
- `ValueChecker` correctly classifies deserialization vs. conformance errors.
- Entity validation handles action/regular/enum/unknown entity types.
- `ValidateEntities` returns Rust-compatible category errors.
- `ValidateRequest` checks action, principal, resource, and context.
- All existing tests pass.
- New unit tests cover each validation path.

---

### Phase 3: CedarType Hierarchy & Capabilities

**Goal:** Internal type system infrastructure for the expression typechecker.

**Task 1 -- Create `src/Cedar.Schema/Internal/Validate/CedarType.cs`** porting `cedar_type.go` (~582 lines):

Define the 10-variant `CedarType` hierarchy as specified in section 3.4. Include:
- `CedarAttr` record struct.
- `EntityLub` class with structural equality (sorted `ImmutableArray<EntityType>`), `Single`, `Union`, `IsDisjoint`.
- `CedarTypeName(CedarType)` -- display name matching Rust format (e.g., `"Set<Long>"`, `"__cedar::internal::Never"`).
- `CedarTypeKindRank(CedarType)` -- ordering rank for deterministic error message sorting.
- `CompareCedarType(CedarType a, CedarType b)` -- structural comparison for consistent output.

**Task 2 -- Create `src/Cedar.Schema/Internal/Validate/CedarTypeOps.cs`** porting LUB and subtype operations:

- `LeastUpperBound(CedarType a, CedarType b, bool strict)` -- returns `(CedarType?, string? error)`. Key cases: `CedarNever` + X = X; `CedarTrue` + `CedarFalse` = `CedarBool`; entity LUB merges sorted elements; record LUB in strict mode requires identical keys, permissive allows width subtyping.
- `LubRecord(CedarRecordType a, CedarRecordType b, bool strict)` -- strict: different keys -> error; permissive: width subtype (intersection of keys, LUB of shared attribute types).
- `IsSubtype(CedarType a, CedarType b)` -- only needed for extension function arg checking.
- `SchemaRecordToCedarRecord(ResolvedRecordType)` -- convert resolved record to internal record.
- `ResolvedTypeToCedarType(ResolvedType)` -- convert resolved type to internal type: `ResolvedStringType` -> `CedarString`, `ResolvedLongType` -> `CedarLong`, `ResolvedBoolType` -> `CedarBool`, `ResolvedExtensionType` -> `CedarExtType`, `ResolvedSetType` -> `CedarSetType`, `ResolvedRecordType` -> `CedarRecordType`, `ResolvedEntityType` -> `CedarEntityType`.
- `LookupAttributeType(CedarType, string attr, ResolvedSchema)` -- entity attribute lookup via schema shape.
- `LookupEntityAttr(EntityLub, string attr, ResolvedSchema)` -- LUB across all entity shapes.
- `EntityHasTags(EntityLub, ResolvedSchema)` -- all entities in LUB must have tags.
- `EntityTagType(EntityLub, ResolvedSchema)` -- LUB of tag types.
- `IsEntityDescendant(EntityType child, EntityType ancestor, ResolvedSchema)` -- transitive parent walk.
- `AnyEntityDescendantOf(EntityLub lhs, EntityLub rhs, ResolvedSchema)` -- cross-LUB descendancy.
- `CheckStrictEntityLUB(CedarType a, CedarType b)` -- strict mode entity LUB rejection.
- `TypeIncompatErr(CedarType a, CedarType b)` -- sorted type names, "the types A and B are not compatible".
- `TypeIncompatErrMulti(CedarType[])` -- for 3+ types.
- `IsActionEntity(EntityType et)` -- `"Action"` or `"*::Action"` suffix check.

**Task 3 -- Create `src/Cedar.Schema/Internal/Validate/CapabilitySet.cs`** porting `capability.go`:

```csharp
using System.Collections.Generic;

namespace Cedar.Schema.Internal.Validate;

internal readonly record struct Capability(string VarName, string Attr);

internal sealed class CapabilitySet
{
    private readonly HashSet<Capability> _caps;

    private CapabilitySet(HashSet<Capability> caps) => _caps = caps;

    public static CapabilitySet Create() => new(new HashSet<Capability>());

    public CapabilitySet Clone()
        => new(new HashSet<Capability>(_caps));

    public CapabilitySet Add(Capability cap)
    {
        CapabilitySet result = Clone();
        result._caps.Add(cap);
        return result;
    }

    public bool Has(Capability cap) => _caps.Contains(cap);

    public CapabilitySet Merge(CapabilitySet other)
    {
        CapabilitySet result = Clone();
        result._caps.UnionWith(other._caps);
        return result;
    }

    public CapabilitySet Intersect(CapabilitySet other)
    {
        HashSet<Capability> result = new(_caps);
        result.IntersectWith(other._caps);
        return new(result);
    }
}
```

**Task 4 -- Create `src/Cedar.Schema/Internal/Validate/ExtensionFunctions.cs`** porting `ext_funcs.go`:

```csharp
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema.Internal.Validate;

internal sealed record ExtFuncSig(
    bool IsConstructor,
    IReadOnlyList<CedarType> ArgTypes,
    CedarType ReturnType);

internal static class ExtensionFunctions
{
    public static readonly IReadOnlyDictionary<string, ExtFuncSig> All
        = new Dictionary<string, ExtFuncSig>(StringComparer.Ordinal)
    {
        // Constructors (4)
        ["ip"] = new(true, new CedarType[] { new CedarString() },
            new CedarExtType(new Ident("ipaddr"))),
        ["decimal"] = new(true, new CedarType[] { new CedarString() },
            new CedarExtType(new Ident("decimal"))),
        ["datetime"] = new(true, new CedarType[] { new CedarString() },
            new CedarExtType(new Ident("datetime"))),
        ["duration"] = new(true, new CedarType[] { new CedarString() },
            new CedarExtType(new Ident("duration"))),
        // Decimal comparisons (4)
        ["lessThan"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("decimal")),
                              new CedarExtType(new Ident("decimal")) },
            new CedarBool()),
        ["lessThanOrEqual"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("decimal")),
                              new CedarExtType(new Ident("decimal")) },
            new CedarBool()),
        ["greaterThan"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("decimal")),
                              new CedarExtType(new Ident("decimal")) },
            new CedarBool()),
        ["greaterThanOrEqual"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("decimal")),
                              new CedarExtType(new Ident("decimal")) },
            new CedarBool()),
        // IP methods (5)
        ["isIpv4"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("ipaddr")) },
            new CedarBool()),
        ["isIpv6"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("ipaddr")) },
            new CedarBool()),
        ["isLoopback"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("ipaddr")) },
            new CedarBool()),
        ["isMulticast"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("ipaddr")) },
            new CedarBool()),
        ["isInRange"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("ipaddr")),
                              new CedarExtType(new Ident("ipaddr")) },
            new CedarBool()),
        // Datetime methods (4)
        ["toDate"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("datetime")) },
            new CedarExtType(new Ident("datetime"))),
        ["toTime"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("datetime")) },
            new CedarExtType(new Ident("datetime"))),
        ["offset"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("datetime")),
                              new CedarExtType(new Ident("duration")) },
            new CedarExtType(new Ident("datetime"))),
        ["durationSince"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("datetime")),
                              new CedarExtType(new Ident("datetime")) },
            new CedarExtType(new Ident("duration"))),
        // Duration methods (5)
        ["toDays"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("duration")) },
            new CedarLong()),
        ["toHours"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("duration")) },
            new CedarLong()),
        ["toMinutes"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("duration")) },
            new CedarLong()),
        ["toSeconds"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("duration")) },
            new CedarLong()),
        ["toMilliseconds"] = new(false,
            new CedarType[] { new CedarExtType(new Ident("duration")) },
            new CedarLong()),
    };
}
```

Total: 22 entries. Verify against Go `ext_funcs.go` at implementation time -- the Go file is 43 lines and should be the single source of truth. (Earlier drafts claimed 23 but double-counted `offset`.)

**Task 5 -- Create `src/Cedar.Schema/Internal/Validate/RequestEnvironment.cs`** porting `request_env.go`:

```csharp
using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Schema.Internal.Validate;

internal sealed record RequestEnvironment(
    EntityType PrincipalType,
    EntityUid ActionUid,
    EntityType ResourceType,
    CedarRecordType ContextType);
```

Methods (implemented as `internal static` helpers or on `PolicyValidator`):
- `GenerateRequestEnvironments(ResolvedSchema schema)` -- iterate all actions with non-null `AppliesTo`, build cross-product of principals x resources x action UID, converting `AppliesTo.Context` via `CedarTypeOps.SchemaRecordToCedarRecord()`.
- `FilterForPolicy(List<RequestEnvironment> envs, EntityType[]? principalTypes, EntityType[]? resourceTypes, EntityUid[]? actionUids)` -- filter by constraints. `null` constraint means "all" (ScopeAll).

**Task 6 -- Write unit tests:**
- `test/Cedar.Schema.Tests/CedarTypeTests.cs`: EntityLub union/disjoint/single/structural equality; CedarTypeName for all 10 variants; CedarTypeKindRank ordering; CompareCedarType structural ordering.
- `test/Cedar.Schema.Tests/CedarTypeOpsTests.cs`: LUB of Bool variants (True+False=Bool, True+True=True); LUB of entity unions; LUB of records (strict: different keys -> error; permissive: width subtype); LUB of sets; ResolvedTypeToCedarType for each variant; IsSubtype for extensions.
- `test/Cedar.Schema.Tests/CapabilitySetTests.cs`: Add/Has/Clone/Merge/Intersect basic operations.
- `test/Cedar.Schema.Tests/RequestEnvironmentTests.cs`: Generation from multi-action schema; filtering by principal/resource/action constraints.

**Estimated test count:** 35-45 new tests.

**Definition of Done:**
- All 10 `CedarType` variants defined with display names matching Rust format.
- `EntityLub` with sorted invariant, union, disjoint check, and correct structural equality.
- LUB computation for all type combinations (10x10 matrix -- not all combinations meaningful, but all must be handled without exceptions).
- Strict vs permissive LUB behavior for records and entity types.
- `CapabilitySet` with immutable-style operations (clone-on-write).
- All 22 extension function signatures registered (verify against Go source).
- `RequestEnvironment` generation and filtering operational.
- `dotnet build cedar-dotnet.sln` -- zero warnings.

---

### Phase 4: Policy Typechecking

**Goal:** `SchemaValidator.ValidatePolicy()` performs scope validation and full expression typechecking. Address `NodeAccess.Attribute` based on the Phase 1 investigation findings.

**Task 1 -- Create `src/Cedar.Schema/Internal/Validate/ScopeValidator.cs`** porting scope validation from `policy.go` (lines 108-346):

- `ValidatePrincipalScope(IScope scope, SchemaValidator validator)` -> `(EntityType[]?, List<string> errors)`. Switch on `ScopeAll`, `ScopeEq`, `ScopeIn`, `ScopeIs`, `ScopeIsIn`.
- `ValidateAndGetActionUids(IScope scope, SchemaValidator validator)` -> `(EntityUid[]?, List<string> errors)`. Switch on `ScopeAll`, `ScopeEq`, `ScopeIn`, `ScopeInSet`.
- `ValidateResourceScope(IScope scope, SchemaValidator validator)` -> `(EntityType[]?, List<string> errors)`. Same pattern as principal.
- `ValidateActionApplication(EntityType[]?, EntityType[]?, EntityUid[]?, SchemaValidator validator)` -> `string? error`. Check at least one action's `AppliesTo` intersects constraints.
- `GetActionsInSet(EntityUid[] uids, SchemaValidator validator)` -- expand action descendants.
- `GetEntityTypesIn(EntityType target, SchemaValidator validator)` -- find all types that are transitively `in` the target type via `ParentTypes`.

**Note on `CedarPath` conversion:** `NodeIs(INode Left, CedarPath EntityType)` and `NodeIsIn` use `CedarPath` for the entity type, not `EntityType`. The scope validator and typechecker must convert `CedarPath` to `EntityType` for all entity type lookups. Implement a conversion helper: `EntityType CedarPathToEntityType(CedarPath path)`.

**Task 2 -- Create `src/Cedar.Schema/Internal/Validate/TypeChecker.cs`** porting `typechecker.go` (1,547 lines). Central method:

```csharp
namespace Cedar.Schema.Internal.Validate;

internal sealed class TypeChecker
{
    private readonly SchemaValidator _validator;

    internal TypeChecker(SchemaValidator validator)
        => _validator = validator;

    internal (CedarType? Type, CapabilitySet Caps, List<string> Errors)
        TypeOfExpr(RequestEnvironment env, INode expr, CapabilitySet caps);

    internal List<string> TypecheckConditions(
        List<RequestEnvironment> envs, ImmutableArray<INode> conditions);
}
```

Switch on `INode` concrete type (28 cases matching Go exactly):

| C# Node Type | Method | Key Behavior |
|-------------|--------|-------------|
| `NodeValue` | `TypeOfValue` | `CedarBool` -> True/False, `CedarLong` -> Long, `CedarString` -> String, `EntityUid` -> Entity |
| `NodeVariable` | `TypeOfVariable` | principal/resource -> Entity(Single), action -> Entity(Single), context -> Record |
| `NodeAnd` | `TypeOfAnd` | Both args Bool, **intersect** capabilities (both branches must guard) |
| `NodeOr` | `TypeOfOr` | Both args Bool, **merge** capabilities (either branch suffices) |
| `NodeNot` | `TypeOfNot` | Arg Bool, return Bool |
| `NodeIfThenElse` | `TypeOfIfThenElse` | Condition Bool, LUB of then/else, capabilities: if-caps for then, intersect then+else for output |
| `NodeEquals` / `NodeNotEquals` | `TypeOfEquality` | Type compatibility check, return Bool |
| `NodeLessThan` / `NodeLessThanOrEqual` / `NodeGreaterThan` / `NodeGreaterThanOrEqual` | `TypeOfComparison` | Both Long, return Bool |
| `NodeAdd` / `NodeSub` / `NodeMult` | `TypeOfArith` | Both Long, return Long |
| `NodeNegate` | `TypeOfNegate` | Arg Long, return Long |
| `NodeIn` | `TypeOfIn` | Left Entity, right Entity or Set<Entity>, return Bool |
| `NodeContains` | `TypeOfContains` | Left Set, right any, return Bool |
| `NodeContainsAll` / `NodeContainsAny` | `TypeOfContainsAllAny` | Both Set, return Bool |
| `NodeIsEmpty` | `TypeOfIsEmpty` | Arg Set, return Bool |
| `NodeLike` | `TypeOfLike` | Arg String, return Bool |
| `NodeIs` | `TypeOfIs` | Arg Entity, return Bool. Convert `CedarPath` to `EntityType`. |
| `NodeIsIn` | `TypeOfIsIn` | Arg Entity, entity arg Entity, return Bool. Convert `CedarPath` to `EntityType`. |
| `NodeHas` | `TypeOfHas` | Arg Record/Entity, return Bool, **adds capability** |
| `NodeAccess` | `TypeOfAccess` | **See NodeAccess handling below** |
| `NodeHasTag` | `TypeOfHasTag` | Entity must have tags declared. Return Bool. |
| `NodeGetTag` | `TypeOfGetTag` | Entity must have tags. Return tag type. Emit `unsafeTagAccess` if key is dynamic. |
| `NodeRecord` | `TypeOfRecord` | Infer each element's type. Return Record. |
| `NodeSet` | `TypeOfSet` | LUB of all elements. Return Set. |
| `NodeExtensionCall` | `TypeOfExtensionCall` | Lookup in extension registry, validate arg count and types. Return declared return type. |

**NodeAccess handling (based on Phase 1 investigation):**

The C# `NodeAccess(INode Arg, INode Attribute)` stores the attribute name as an `INode`. Based on the Phase 1 investigation findings:
- **If `Attribute` is always `NodeValue(CedarString)` at parse time:** Pattern-match to extract the string. If the match fails (e.g., from a programmatically-constructed AST), emit a diagnostic error: "attribute access requires a string literal attribute name".
- **If `Attribute` can be a computed expression:** Implement a dynamic lookup path that returns `CedarNever` with a diagnostic (matching Go's behavior for unknown attribute types).

The typechecker must extract the attribute name string, check if the attribute is in the capability set (guarded by a prior `has` check) or is a required attribute on the entity/record type, and return the attribute's type. If unguarded and optional, emit "unsafe optional attribute access" error.

**Task 3 -- Create `src/Cedar.Schema/Internal/Validate/PolicyValidator.cs`** porting policy orchestration from `policy.go`:

```csharp
namespace Cedar.Schema.Internal.Validate;

internal static class PolicyValidator
{
    internal static ValidationResult ValidatePolicy(
        string policyId, PolicyAst ast, SchemaValidator validator)
    {
        List<string> errors = new();

        // 1. Validate principal scope
        (EntityType[]? principalTypes, List<string> pErrs)
            = ScopeValidator.ValidatePrincipalScope(ast.PrincipalScope, validator);
        errors.AddRange(pErrs);

        // 2. Validate action scope
        (EntityUid[]? actionUids, List<string> aErrs)
            = ScopeValidator.ValidateAndGetActionUids(ast.ActionScope, validator);
        errors.AddRange(aErrs);

        // 3. Validate resource scope
        (EntityType[]? resourceTypes, List<string> rErrs)
            = ScopeValidator.ValidateResourceScope(ast.ResourceScope, validator);
        errors.AddRange(rErrs);

        // 4. Check action application
        string? actionAppErr = ScopeValidator.ValidateActionApplication(
            principalTypes, resourceTypes, actionUids, validator);
        if (actionAppErr != null)
            errors.Add(actionAppErr);

        // 5. Strict mode: empty set literals
        if (validator.IsStrict && ast.ActionScope is ScopeInSet sis
            && sis.Entities.Length == 0)
            errors.Add("empty set literals are forbidden in policies");

        // 6. Generate and filter request environments
        List<RequestEnvironment> allEnvs =
            RequestEnvironment.Generate(validator.Schema);
        List<RequestEnvironment> envs =
            RequestEnvironment.FilterForPolicy(
                allEnvs, principalTypes, resourceTypes, actionUids);

        // 7. Typecheck conditions
        // Permissive mode: skip condition typecheck when action
        // applicability already failed.
        if (envs.Count > 0 && ast.Conditions.Length > 0
            && (validator.IsStrict || actionAppErr == null))
        {
            TypeChecker tc = new(validator);
            List<string> condErrors = tc.TypecheckConditions(
                envs, ast.Conditions);
            errors.AddRange(condErrors);
        }

        // 8. Prefix errors
        if (errors.Count == 0)
            return ValidationResult.Success;

        List<string> prefixed = new();
        foreach (string e in errors)
        {
            if (IsTypeIncompatError(e) || string.IsNullOrEmpty(policyId))
                prefixed.Add(e);
            else
                prefixed.Add($"for policy `{policyId}`, {e}");
        }
        return ValidationResult.Failure(prefixed);
    }
}
```

**`TypecheckConditions` error multiset merging:** For each condition, for each environment, call `TypeOfExpr`. Collect errors per-env as multisets (msg -> count). Merge across envs using element-wise max count. Special handling for `unsafeTagAccess` errors: aggregate by principal or resource entity type, then sum across types. Port Go's `typecheckConditions()` (policy.go lines 348-448). Finally check that inferred type is Bool; if not, emit "unexpected type: expected Bool but saw {type}".

**Task 4 -- Wire `SchemaValidator.ValidatePolicy`**

In `SchemaValidator.cs`, the `ValidatePolicy` method delegates to `PolicyValidator`:

```csharp
public ValidationResult ValidatePolicy(string policyId, Policy policy)
{
    PolicyAst ast = policy.Ast;  // accessible via InternalsVisibleTo
    return PolicyValidator.ValidatePolicy(policyId, ast, this);
}
```

Expose `internal ResolvedSchema Schema => _schema;` and `internal bool IsStrict => _strict;` for use by `PolicyValidator` and `TypeChecker`.

**Task 5 -- Write unit tests:**
- `test/Cedar.Schema.Tests/ScopeValidatorTests.cs`: Each scope variant with valid/invalid entity types and actions; action descendant expansion; entity type `in` expansion; `CedarPath` to `EntityType` conversion.
- `test/Cedar.Schema.Tests/TypeCheckerTests.cs`: At least one test per node type (28 tests minimum). Focus on:
  - `has` guard capability propagation through `And`/`Or`/`IfThenElse`.
  - Entity LUB across multiple environments.
  - Extension function argument type validation.
  - Tag access with/without `hasTag` guard.
  - Set literal with mixed element types.
  - Record literal type inference.
  - `NodeAccess` with string literal attribute (normal case).
  - `NodeAccess` with non-literal attribute (error case, based on investigation).
- `test/Cedar.Schema.Tests/PolicyValidatorTests.cs`: Full policy validation with scope + typecheck; error merging across multiple environments; strict vs permissive mode differences; empty set literal rejection in strict mode; permissive mode skips condition typecheck on action applicability failure.

**Estimated test count:** 50-70 new tests.

**Definition of Done:**
- `TypeChecker` dispatches to all 28+ node types.
- Capability tracking works correctly through `And`/`Or`/`IfThenElse`.
- `ScopeValidator` handles all 6 scope types.
- `CedarPath` to `EntityType` conversion is implemented and tested.
- Action application validation matches Go behavior.
- Error multiset merging with element-wise max counts.
- Tag access error aggregation by entity type (`unsafeTagAccess` with `usesPrincipal`/`usesResource` flags).
- Error prefixing with policy ID (except type-incompat errors).
- Strict mode rejects empty action sets.
- Permissive mode skips condition typecheck when action applicability fails.
- `NodeAccess.Attribute` handling matches investigation findings.
- `dotnet build cedar-dotnet.sln` -- zero warnings.

---

### Phase 5: Conformance Integration & Semport Fix

**Goal:** Corpus validation tests assert pass/fail parity with Rust. Semport pipeline cannot silently skip features.

**Task 1 -- Upgrade `test/Cedar.Conformance/CorpusValidationTests.cs`**

Replace the current structural-only assertions with real validation calls:

```csharp
[Theory]
[MemberData(nameof(CorpusTestData.ValidationScenarios),
    MemberType = typeof(CorpusTestData))]
public void StrictPolicyValidationMatchesRust(CorpusScenarioCase scenario)
{
    SchemaDocument schema = SchemaDocument.UnmarshalCedar(scenario.SchemaText!);
    ResolvedSchema resolved = schema.Resolve();
    SchemaValidator strict = new(resolved, ValidationMode.Strict);

    foreach ((string policyId, CorpusPolicyValidationResult expected)
        in scenario.Validation!.PolicyValidation.PerPolicy)
    {
        // Note: verify actual PolicySet API -- may need scenario.Policies.All()
        // iteration rather than indexer access. Adjust based on actual
        // CorpusScenarioCase data model.
        ValidationResult result = strict.ValidatePolicy(policyId, /* policy */);
        Assert.Equal(expected.Strict, result.IsValid);
    }
}
```

Analogous tests for:
- `PermissivePolicyValidationMatchesRust` -- same structure with `ValidationMode.Permissive`.
- `EntityValidationMatchesRust` -- validate each entity against the schema.
- `RequestValidationMatchesRust` -- validate each request against the schema.

**Implementation note:** The `CorpusValidationEntityResult` uses `[JsonExtensionData]` -- meaning the actual validation result schema may not be strongly typed. The conformance test upgrade may need to parse extension data properties to extract pass/fail flags. Verify the actual `CorpusScenarioCase` and `CorpusPolicyValidationResult` data structures before writing test code.

Keep the existing structural test as a sanity check (rename to `ValidationPayloadsHaveExpectedCounts`).

**Task 2 -- Track parity metrics**

Corpus parity targets:
- **Entity validation:** 100% pass/fail parity with Rust. Entity validation is structurally simpler and should match exactly.
- **Request validation:** 100% pass/fail parity with Rust. Request validation is also structurally simpler.
- **Policy validation (strict + permissive):** 95%+ pass/fail parity with Rust. The expected 5% gap comes from: (a) error multiset merging edge cases involving `unsafeTagAccess` aggregation by entity type, and (b) potential LUB computation differences for complex nested record/entity types. These are the most intricate parts of the Go typechecker to port and may require iterative debugging against specific corpus scenarios.

If any scenarios systematically disagree, document them as known divergences. Use a mismatch counter pattern:
```csharp
int mismatches = 0;
// ... count mismatches ...
_output.WriteLine($"Validation parity: {total - mismatches}/{total} match");
Assert.True(mismatches <= threshold,
    $"Too many mismatches: {mismatches} (threshold: {threshold})");
```

**Task 3 -- Semport pipeline gate**

The semport pipeline must hard-fail on "out of scope" without a linked tracking issue. Implementation:

- Update `.ai/semport_plan.md` with the rule: semport commits must not contain "out of scope" for any sprint-declared feature without an explicit linked tracking issue (format: `Tracking: #<issue-number>` or `Tracking: <URL>`) in the commit body.
- Add a CI check (script in `.github/scripts/semport_guard.sh` invoked from `.github/workflows/ci.yml`) that:
  1. Scans commit messages in the PR for "out of scope" (case-insensitive).
  2. If found, checks for a `Tracking:` line in the same commit body.
  3. Fails the build if "out of scope" appears without a tracking reference.
  4. No bypass flag -- the only way to use "out of scope" is to provide a tracking issue.

**Task 4 -- Full regression**

`dotnet test cedar-dotnet.sln` must pass with zero failures. All 77,501+ existing tests must continue to pass.

**Definition of Done:**
- Corpus validation tests assert pass/fail parity for policies (strict + permissive), entities, and requests.
- Entity and request validation: 100% parity with Rust.
- Policy validation: 95%+ parity with Rust, with remaining gaps tracked and justified.
- Semport guard blocks "out of scope" commits without a linked tracking issue.
- All 77,501+ existing tests continue to pass.
- `dotnet build cedar-dotnet.sln` produces zero warnings.
- `dotnet test cedar-dotnet.sln` -- zero failures.

## 5. Files Summary

### New Files

| File | Lines Est. | Phase | Purpose |
|------|----------:|-------|---------|
| `src/Cedar.Schema/ResolvedTypes.cs` | ~80 | 1 | `ResolvedType` sealed hierarchy + `ResolvedAttribute` |
| `src/Cedar.Schema/SchemaValidator.cs` | ~80 | 2 | Public `SchemaValidator` + `ValidationResult` + `ValidationMode` |
| `src/Cedar.Schema/SchemaValidator.Entity.cs` | ~160 | 2 | Partial class: entity/entities validation |
| `src/Cedar.Schema/SchemaValidator.Request.cs` | ~110 | 2 | Partial class: request validation |
| `src/Cedar.Schema/Internal/Validate/ValueChecker.cs` | ~150 | 2 | Runtime value vs resolved type checking |
| `src/Cedar.Schema/Internal/Validate/CedarType.cs` | ~120 | 3 | Internal 10-variant type system + `EntityLub` |
| `src/Cedar.Schema/Internal/Validate/CedarTypeOps.cs` | ~530 | 3 | LUB, subtype, name formatting, comparison, conversion |
| `src/Cedar.Schema/Internal/Validate/CapabilitySet.cs` | ~60 | 3 | Has-guard capability tracking |
| `src/Cedar.Schema/Internal/Validate/ExtensionFunctions.cs` | ~50 | 3 | Extension function signatures |
| `src/Cedar.Schema/Internal/Validate/RequestEnvironment.cs` | ~80 | 3 | Env generation + filtering |
| `src/Cedar.Schema/Internal/Validate/TypeChecker.cs` | ~1,800 | 4 | Expression type inference (28+ node dispatch) |
| `src/Cedar.Schema/Internal/Validate/ScopeValidator.cs` | ~300 | 4 | Scope checking for principal/action/resource |
| `src/Cedar.Schema/Internal/Validate/PolicyValidator.cs` | ~250 | 4 | Orchestration + error multiset merge |
| `test/Cedar.Schema.Tests/SchemaResolverTests.cs` | ~200 | 1 | Entity/enum/common-type resolution tests |
| `test/Cedar.Schema.Tests/ValueCheckerTests.cs` | ~200 | 2 | Value checking unit tests |
| `test/Cedar.Schema.Tests/SchemaValidatorEntityTests.cs` | ~180 | 2 | Entity validation unit tests |
| `test/Cedar.Schema.Tests/SchemaValidatorRequestTests.cs` | ~120 | 2 | Request validation unit tests |
| `test/Cedar.Schema.Tests/CedarTypeTests.cs` | ~150 | 3 | Type system unit tests |
| `test/Cedar.Schema.Tests/CedarTypeOpsTests.cs` | ~200 | 3 | LUB/subtype unit tests |
| `test/Cedar.Schema.Tests/CapabilitySetTests.cs` | ~60 | 3 | Capability set unit tests |
| `test/Cedar.Schema.Tests/RequestEnvironmentTests.cs` | ~80 | 3 | Env generation unit tests |
| `test/Cedar.Schema.Tests/TypeCheckerTests.cs` | ~500 | 4 | Expression typechecker unit tests |
| `test/Cedar.Schema.Tests/ScopeValidatorTests.cs` | ~200 | 4 | Scope validation unit tests |
| `test/Cedar.Schema.Tests/PolicyValidatorTests.cs` | ~200 | 4 | Policy validation unit tests |
| `.github/scripts/semport_guard.sh` | ~30 | 5 | Semport pipeline guard |

### Modified Files

| File | Phase | Change |
|------|-------|--------|
| `src/Cedar.Schema/ResolvedSchema.cs` | 1 | Add `ResolvedEntity`, `ResolvedEnum`, `ResolvedNamespace`, `ResolvedAppliesTo`; expand `ResolvedSchema`; change `ResolvedAction.AppliesTo` type |
| `src/Cedar.Schema/SchemaResolver.cs` | 1 | 5-phase resolver with `ResolverState` -- entity/enum/common-type resolution, shadowing, cycles |
| `src/Cedar.Schema/Cedar.Schema.csproj` | 1 | Add `<ProjectReference>` to `Cedar.Ast.csproj` |
| `src/Cedar.Ast/Properties/InternalsVisibleTo.cs` | 2 | Add `[assembly: InternalsVisibleTo("Cedar.Schema")]` |
| `src/Cedar.Ast/Cedar.Ast.csproj` | 2 | Add `<InternalsVisibleTo Include="Cedar.Schema" />` |
| `test/Cedar.Conformance/CorpusValidationTests.cs` | 5 | Replace structural assertions with semantic validation pass/fail comparison |
| `test/Cedar.Schema.Tests/SchemaResolverTests.cs` | 1 | Add tests for expanded resolution (if file exists; otherwise listed under New) |
| `.ai/semport_plan.md` | 5 | Add mandatory build gate and "out of scope" rejection rule with tracking issue requirement |
| `.github/workflows/ci.yml` | 5 | Add semport guard step |

### Estimated Totals

- **New production code:** ~3,770 lines across 13 source files
- **New test code:** ~2,090 lines across 11 test files
- **New unit tests:** 130-175
- **Modified files:** 9

## 6. Definition of Done

1. `dotnet build cedar-dotnet.sln` produces zero warnings.
2. `dotnet test cedar-dotnet.sln` passes with zero failures (all 77,501+ existing tests plus new tests).
3. `ResolvedSchema` includes `Entities`, `Enums`, `Namespaces` with fully resolved types.
4. `ResolvedAction.AppliesTo` carries `ResolvedAppliesTo` with resolved `Context` as `ResolvedRecordType`.
5. `SchemaResolver` implements all 5 Go resolver phases (register, shadowing, cycles, resolve, action membership) via internal `ResolverState`.
6. `ResolvedType` sealed hierarchy with 7 variants -- no unresolved `TypeRef` or `EntityTypeRef` in the output.
7. `SchemaValidator` exposes `ValidatePolicy`, `ValidateEntity`, `ValidateEntities`, `ValidateRequest`.
8. Both `ValidationMode.Strict` and `ValidationMode.Permissive` are supported with correct behavioral differences (including permissive mode condition skip).
9. Expression typechecker covers all 28+ AST node types in `Cedar.Ast.Internal.NodeTypes`.
10. Extension function registry covers all entries from Go's `ext_funcs.go` (verify count at implementation time).
11. Capability tracking correctly handles `has` guards through `&&`, `||`, and `if-then-else`.
12. Error multiset merging across request environments matches Go's element-wise max-count semantics.
13. `NodeAccess.Attribute` handling documented and implemented based on Phase 1 investigation.
14. `EntityLub` has correct structural equality (not reference equality).
15. Corpus validation tests assert pass/fail parity: 100% for entity/request, 95%+ for policy.
16. Semport pipeline guard hard-fails on "out of scope" without linked tracking issue.

## 7. Risks

| Risk | Severity | Likelihood | Mitigation |
|------|----------|-----------|------------|
| **`NodeAccess.Attribute` INode mismatch.** C#'s `NodeAccess` uses `INode` for the attribute; Go uses a plain string. If the attribute can be a computed expression, the entire typechecker attribute-lookup strategy changes. | High | High | Mandatory investigation in Phase 1 Task 0 before any typechecker design. Pattern-match on `NodeValue(CedarString)` and handle the non-literal case with a diagnostic error. |
| **Error multiset merging complexity.** Go's `policy.go` lines 348-448 implement per-environment error counts with element-wise max and special-case `unsafeTagAccess` aggregation by entity type. Off-by-one in merge logic causes corpus test failures. | High | High | Dedicate focused unit tests for error merging before corpus integration. Corpus conformance tests provide >15K data points. |
| **`SchemaResolver` static-to-stateful conversion.** The current `public static class SchemaResolver` needs 5-phase pipeline state. Converting to instance state while preserving the static `Resolve()` facade could introduce subtle bugs in the most-used public API. | High | Medium | Private `ResolverState` class created and disposed within `Resolve()`. Run full 77,501 test suite after each resolver sub-phase commit. Decompose into 3 independently-testable commits. |
| **`Cedar.Core`/`Cedar.Ast` linked-source pattern.** `Policy.cs` is compiled into `Cedar.Ast` via linked source, not `Cedar.Core`. Adding `Cedar.Schema -> Cedar.Core` alongside `Cedar.Schema -> Cedar.Ast` could cause type identity conflicts. | High | Medium | Reference only `Cedar.Ast` from `Cedar.Schema`. Do NOT add a separate `Cedar.Core` reference. Verify with `dotnet build` that types resolve unambiguously. |
| **`EntityLub` structural equality.** `IReadOnlyList<EntityType>` gets reference equality by default. If `EntityLub` equality is wrong, capability set lookups, LUB comparisons, and type deduplication fail silently. | Medium | Medium | Use `ImmutableArray<EntityType>` with custom `Equals`/`GetHashCode` override using sequence comparison. Explicit unit tests for equality with identical-but-different-instance lists. |
| **Backward compatibility.** Changing `ResolvedAction.AppliesTo` from `AppliesToDecl?` to `ResolvedAppliesTo?` is a breaking API change. | Medium | Low | Codebase audit confirms only `SchemaResolver` and conformance test assertions use `AppliesToDecl`. Both modified in this sprint. No external NuGet consumers yet (pre-release). |
| **LUB computation fidelity.** Go `leastUpperBound` has complex entity-type LUB merging with fallback to null (incompatible). Subtle differences cause divergent typechecking results. | Medium | Medium | Dedicated unit tests for every LUB case. Compare against Go test fixtures. |
| **Corpus test data model assumptions.** Conformance test code assumes specific property paths (`scenario.Validation!.PolicyValidation.PerPolicy`, `scenario.Policies[...]`). If the data structures do not match, Phase 5 stalls on test infrastructure work. | Medium | Medium | Verify `CorpusScenarioCase`, `CorpusPolicyValidationResult`, and `PolicySet` API surface before writing Phase 5 test code. Check for `[JsonExtensionData]` on entity validation result types. |
| **Circular project reference risk.** `Cedar.Schema` -> `Cedar.Ast` is new. | Low | Low | Dependency graph is acyclic: `Types <- Core <- Ast` (linked source), `Ast <- Schema`. The `Ast -> Schema` direction is IVT only, not a project reference. Verified no cycle. |
| **Error message ordering.** Rust/Go sort type names in incompatibility errors. | Low | Low | Implement `CompareCedarType` to match Go's `cedarTypeKindRank` ordering. Affects future message-level parity only; pass/fail is unaffected. |

## 8. Security

- **No new external dependencies.** All validation logic uses BCL types only.
- **No user input in executable paths.** Error messages may contain entity type names and policy IDs from untrusted input, but these are used only in diagnostic strings, never as dictionary keys or format strings that could enable injection.
- **Validation does not execute policies.** The typechecker performs static analysis only; it never evaluates Cedar expressions against runtime data.
- **Schema resolution rejects cycles.** Common type cycle detection (Kahn's algorithm) and action hierarchy cycle detection (DFS) prevent DoS via deeply nested or recursive schemas.
- **Bounded recursion.** `ResolveType` is recursive (for `SetType`, `RecordType`, `TypeRef`), but cycle detection runs first, ensuring the recursion terminates. For defense-in-depth against very deep (non-cyclic) nesting, add a depth limit of 100 levels with a clear error message.
- **Immutable resolved schema.** All `Resolved*` types are sealed records with `IReadOnlyDictionary`/`IReadOnlyList` properties, preventing post-resolution mutation.
- **Bounded corpus extraction.** `CorpusTestData.cs` already enforces `MaxArchiveBytes` (512MB) and `MaxEntryBytes` (16MB) limits. No changes needed.
- **Semport guard fails closed.** The pipeline guard defaults to failure and requires an explicit tracking issue reference. It cannot be bypassed by commit message formatting tricks or environment variables.

## 9. Dependencies

### Assembly References

| Dependency | Direction | Status | Reason |
|------------|-----------|--------|--------|
| `Cedar.Types` | `Cedar.Schema` -> `Cedar.Types` | Existing (transitive via `Cedar.Ast`) | `EntityType`, `EntityUid`, `Entity`, `EntityMap`, `CedarRecord`, `CedarSet`, `CedarLong`, `CedarBool`, `CedarString`, `Ident`, `CedarPath`, `CedarIpAddress`, `CedarDecimal`, `CedarDatetime`, `CedarDuration`, `ICedarData` |
| `Cedar.Ast` | `Cedar.Schema` -> `Cedar.Ast` | **NEW** | `Policy`, `PolicySet`, `PolicyId`, `Request`, `PolicyAst`, `INode`, 28 node types, `IScope`, 6 scope types |
| `Cedar.Ast` IVT | `Cedar.Ast` grants IVT to `Cedar.Schema` | **NEW** | Access `Policy.Ast` (internal property), `PolicyAst` record, all `INode` implementations |

**Critical:** Do NOT add a separate `Cedar.Schema -> Cedar.Core` project reference. `Cedar.Core` types (`Policy`, `Request`, etc.) are compiled into `Cedar.Ast` via the linked-source pattern. Adding both references would cause type identity conflicts.

### No New NuGet Packages

All implementation uses BCL types only. No new external dependencies.

## 10. Open Questions

These are questions that remain open and should be resolved during implementation:

1. **`ValidatePolicies(PolicySet)` convenience method.** The corpus tests validate all policies in a set. Should `SchemaValidator` expose a `ValidatePolicies` method that returns per-policy results? **Recommendation:** Add if corpus test integration reveals the need, but do not block on it.

2. **`ResolvedRecordType` key type.** Go uses `types.String` (their `CedarString`) as record attribute keys. C# uses `string` with `StringComparer.Ordinal`. This is a deliberate deviation. If `ValueChecker.CheckRecord` key conversion causes issues, revisit. **Recommendation:** Keep `string` keys.

3. **Corpus parity fallback.** If initial policy parity is below 95%, should we block the sprint or ship with tracked gaps? **Recommendation:** Ship with tracked gaps. Any gaps below 90% should trigger a P1 follow-up investigation.

### Resolved Questions (incorporated as decisions)

- **`AppliesToDecl` backward compatibility:** Replace entirely with `ResolvedAppliesTo`. Codebase audit confirms no external consumers. (Source: interview + codebase audit)
- **Error message fidelity:** Target pass/fail parity first. Match Rust's format opportunistically. Full message parity is a follow-up. (Source: intent document + all three drafts agree)
- **`ValidationResult` vs exceptions:** Return `ValidationResult` for the public API. Throw `InvalidOperationException` only for internal invariant violations. (Source: all three drafts agree)
- **Validator location:** `Cedar.Schema` assembly, not a new `Cedar.Schema.Validate` assembly. (Source: intent document + all three drafts agree)
- **Resolved type hierarchy pattern:** Sealed abstract record + subtypes. (Source: Codex draft design evaluation)
- **Permissive mode condition skip:** Match Go behavior exactly -- skip condition typechecking when action applicability fails. (Source: Codex critique + Go reference)
- **Enum entity validation:** Match Go behavior -- accept with no further checks (return Success). (Source: Claude draft open question 6)
- **Tag validation on entities with no tags declared:** Match Go exactly -- if `schemaEntity.Tags` is null and entity has tags, return deserialization error. (Source: Claude draft open question 7)
- **`NodeAccess.Attribute` investigation:** Mandatory Phase 1 task, must be resolved before Phase 4. (Source: interview decision)
- **Semport gate:** Hard fail on "out of scope" without linked tracking issue. No bypass flag. (Source: interview decision)
- **`Namespaces` in `ResolvedSchema`:** Include. Go has it; useful for downstream consumers. (Source: Claude critique, adopted)
- **`EntityLub` equality:** Override `Equals`/`GetHashCode` with sequence comparison using `ImmutableArray`. (Source: Gemini critique, adopted)
- **`SchemaResolver` statefulness:** Keep `static` class with private `ResolverState`. (Source: Codex draft + all three critiques)

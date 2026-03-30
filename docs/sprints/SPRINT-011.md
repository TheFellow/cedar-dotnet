# Sprint 011: Schema Validation Idiom Hardening

## 1. Overview

Address non-idiomatic C# patterns, correctness risks, and performance issues identified in code review of Sprint 010 (schema validation). This is a hardening sprint — no new features, no API changes visible to callers, no test modifications unless a test was depending on errant behavior.

## 2. Items

### High Priority (Correctness / Safety)

#### H1: Entity parent cycle guard

**Files:** `src/Cedar.Schema/SchemaResolver.cs`, `src/Cedar.Schema/Internal/Validate/CedarTypeOps.cs`

**Problem:** Action hierarchy cycles and common-type cycles are detected during `Resolve()`, but entity parent cycles are not. `IsEntityDescendant` in `CedarTypeOps.cs` recurses without a visited set. A malformed schema with circular entity parents causes `StackOverflowException`.

**Fix:** Add entity parent cycle detection in `SchemaResolver.ResolverState` (after `ResolveEntities`, before `BuildResult`). Use the same DFS visited-state pattern as `ValidateActionMembership`. Additionally, add a `HashSet<EntityType>` visited guard to `IsEntityDescendant` as defense-in-depth.

#### H2: Verify and fix `TypeOfLiteralSet` LUB computation

**Files:** `src/Cedar.Schema/Internal/Validate/TypeChecker.cs`

**Problem:** Review flagged that `TypeOfLiteralSet` may use the last element's type instead of computing a proper LUB across all elements. If confirmed, the inferred set element type depends on iteration order — a correctness bug.

**Fix:** Read the method carefully. If it does not fold elements through `LeastUpperBound`, fix it to do so. If it already does (false positive), document why it's correct.

#### H3: Unsafe string slicing in `UnsafeOptionalAccessError`

**Files:** `src/Cedar.Schema/Internal/Validate/TypeChecker.cs`

**Problem:** `varName["context.".Length..]` slices without verifying the prefix is `"context."`. If `varName` is `"principal.foo"` or similar, the slice produces a garbage string.

**Fix:** Add `varName.StartsWith("context.", StringComparison.Ordinal)` guard before the slice. Fall through to a safe default if the prefix doesn't match.

### Medium Priority (Immutability / Performance)

#### M1: Freeze collections in `BuildResult()`

**Files:** `src/Cedar.Schema/SchemaResolver.cs`

**Problem:** `BuildResult()` passes mutable `Dictionary<,>` and `List<>` instances directly into `ResolvedSchema` record properties typed as `IReadOnlyDictionary`/`IReadOnlyList`. The resolver retains no reference, but a cast-back could mutate them. Record equality is also reference-based for these collections.

**Fix:** Call `.ToFrozenDictionary()` and `.ToFrozenSet()` / `.ToImmutableArray()` in `BuildResult()` and in entity/enum/action resolution where lists are built and stored into `IReadOnlyList` properties. Add `using System.Collections.Frozen;` where needed.

#### M2: Replace `CapabilitySet` clone-on-write with `ImmutableHashSet`

**Files:** `src/Cedar.Schema/Internal/Validate/CapabilitySet.cs`

**Problem:** Every `Add`, `Merge`, and `Intersect` clones the entire `HashSet`. The typechecker calls these in recursive expression traversal — significant allocation churn.

**Fix:** Replace the internal `HashSet<Capability>` with `ImmutableHashSet<Capability>`. Use its built-in `Add`, `Union`, and `Intersect` methods which share structure. Keep the `CapabilitySet` wrapper for API clarity.

#### M3: Use `HashSet<EntityType>` in `GetEntityTypesIn`

**Files:** `src/Cedar.Schema/Internal/Validate/ScopeValidator.cs`

**Problem:** `GetEntityTypesIn` uses `List<EntityType>` with `List.Contains` inside nested loops — O(n^2).

**Fix:** Use a `HashSet<EntityType>` for the working set. Convert to array at the end for the return type.

#### M4: Cache request environments on `SchemaValidator`

**Files:** `src/Cedar.Schema/SchemaValidator.cs`, `src/Cedar.Schema/Internal/Validate/PolicyValidator.cs`, `src/Cedar.Schema/Internal/Validate/RequestEnvironment.cs`

**Problem:** `RequestEnvironment.Generate()` computes the full Cartesian product of principals x resources x actions on every `ValidatePolicy` call. This is pure overhead for repeated validation against the same schema.

**Fix:** Precompute and cache the environment list in `SchemaValidator` constructor (or as a lazy field). Pass the cached list to `PolicyValidator` instead of regenerating.

#### M5: Rename `CedarString` to avoid shadowing `Cedar.Types.CedarString`

**Files:** `src/Cedar.Schema/Internal/Validate/CedarType.cs` and all files that reference it

**Problem:** Internal `CedarString : CedarType` has the same name as the public `Cedar.Types.CedarString`, forcing fully-qualified references throughout the typechecker and value checker.

**Fix:** Rename to `CedarStringType`. For consistency, also rename `CedarBool` → `CedarBoolType`, `CedarLong` → `CedarLongType`, `CedarTrue` → `CedarTrueType`, `CedarFalse` → `CedarFalseType`, `CedarNever` → `CedarNeverType`. Update all references. (The types with parameters — `CedarSetType`, `CedarExtType`, `CedarEntityType`, `CedarRecordType` — already follow this convention.)

#### M6: Add static singleton instances for parameterless CedarType variants

**Files:** `src/Cedar.Schema/Internal/Validate/CedarType.cs`, `src/Cedar.Schema/Internal/Validate/CedarTypeOps.cs`, `src/Cedar.Schema/Internal/Validate/TypeChecker.cs`, `src/Cedar.Schema/Internal/Validate/ExtensionFunctions.cs`

**Problem:** `new CedarBool()`, `new CedarTrue()`, `new CedarNever()`, etc. are instantiated repeatedly in hot paths. Each `new` allocates a heap object for a type with no data.

**Fix:** Add `internal static readonly` singleton on each parameterless type (e.g., `CedarBoolType.Instance`). Replace all `new CedarBool()` (or post-rename `new CedarBoolType()`) with the singleton. Records provide value equality, so identity doesn't matter.

### Low Priority (Style / Clarity)

#### L1: `ExtensionFunctions.All` visibility

**Files:** `src/Cedar.Schema/Internal/Validate/ExtensionFunctions.cs`

**Fix:** Change `public static readonly` to `internal static readonly`.

#### L2: Magic integers in `ValidateActionMembership`

**Files:** `src/Cedar.Schema/SchemaResolver.cs`

**Fix:** Define `private enum VisitState { InProgress = 1, Complete = 2 }` and use it instead of raw `1`/`2`.

#### L3: `ResolverState` method visibility

**Files:** `src/Cedar.Schema/SchemaResolver.cs`

**Fix:** Change `public` methods on the `private sealed class ResolverState` to have no explicit access modifier (or `internal`).

#### L4: Duplicate `request.Context ?? new CedarRecord()` in `ValidateRequest`

**Files:** `src/Cedar.Schema/SchemaValidator.Request.cs`

**Fix:** Hoist to a local variable.

#### L5: Eager error string allocation in `CheckExtensionValue`

**Files:** `src/Cedar.Schema/Internal/Validate/ValueChecker.cs`

**Fix:** Move `$"expected {expectedName}, got ..."` after the success check so it's only allocated on failure.

## 3. Definition of Done

1. `dotnet build cedar-dotnet.sln` — zero warnings.
2. `dotnet test cedar-dotnet.sln` — zero failures. All 109,947 existing tests pass.
3. **No test file modifications** unless a test was asserting errant behavior (document any such case).
4. All High items (H1-H3) resolved.
5. All Medium items (M1-M6) resolved.
6. All Low items (L1-L5) resolved.
7. Entity parent cycles detected during `Resolve()` with a clear error message.
8. `IsEntityDescendant` has defense-in-depth cycle guard.
9. `TypeOfLiteralSet` computes proper LUB (or is documented as correct).
10. No `varName[...]` slicing without prefix verification.
11. All resolved schema collections are frozen/immutable after construction.
12. `CapabilitySet` uses `ImmutableHashSet` internally.
13. Request environments cached per `SchemaValidator` instance.
14. No `CedarString`/`Cedar.Types.CedarString` naming collision.
15. Parameterless `CedarType` variants use static singletons.

PORT

## Commit
e796ce2 — types: ensure Entity marshals to JSON with a consistent ordering

## Semantic Analysis
The upstream Go change sorts an Entity's `parents` collection by `(Type, ID)` before JSON marshaling, ensuring deterministic output regardless of the underlying set's iteration order. This is a **behavioral contract change**: any consumer comparing or round-tripping Entity JSON can now rely on a stable ordering of the parents array.

The refactor also extracts a shared `JSONMarshalsTo` test helper (Go-specific, no C# action needed there).

## What Needs Porting
The C# `Entity` type serializes its parents collection to JSON. We must ensure that parents are emitted in a consistent `(Type, ID)` lexicographic order.

### Go source reference
- `inspiration/cedar-go/types/entity.go` lines ~21-28: `slices.SortFunc(parents, ...)` by `(Type, ID)` added inside `MarshalJSON`.

### C# target
1. **Find the Entity JSON serialization path.**
   - Likely in `src/Cedar.Types/` — look for `Entity` record/class and any `JsonConverter` or `MarshalJSON`-equivalent.
   - Search for `parents` or `Parents` near JSON serialization logic.

2. **Sort parents before writing to JSON.**
   - When serializing `Parents` (the `EntityUIDSet` / `ImmutableHashSet<EntityUID>`), order by `EntityUID.Type` then `EntityUID.Id` (both string, ordinal comparison) before emitting the array.
   - Use `.OrderBy(p => p.Type).ThenBy(p => p.Id)` (or `StringComparer.Ordinal`) on the parents sequence inside the JSON write path.

3. **Add / update a test.**
   - In `test/Cedar.Tests/` (or `test/Cedar.Types.Tests/` if it exists), add a test that constructs an `Entity` with parents added in non-alphabetical order and asserts the serialized JSON array is sorted `(Type, ID)`.
   - Mirror the Go test: parents `BazType:1`, `BarType:2`, `BarType:1`, `QuuxType:30`, `QuuxType:3` → expect `BarType:1`, `BarType:2`, `BazType:1`, `QuuxType:3`, `QuuxType:30`.

## Files to Investigate (in order)
1. `src/Cedar.Types/Entity.cs` (or wherever Entity is defined)
2. Any `EntityJsonConverter` or `[JsonConverter]` referencing Entity
3. `test/Cedar.Tests/` for existing Entity JSON tests to extend

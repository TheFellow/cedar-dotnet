PORT

## Commit
432ab3e — 2024-08-23T13:41:25-06:00
cedar-go/types: Change type of EntityUID.Type to EntityType

## Semantic Analysis
In Go, `EntityUID.Type` was `string`; it is now `EntityType` (a distinct named type `type EntityType string`).
This strengthens type safety: code that accepted a raw string for an entity type namespace now requires an explicit `EntityType(...)` cast in Go.

In our C# codebase the parallel concept is `EntityUid` (or `EntityUid.Type`). We likely represent the type portion as a plain `string`. We should introduce (or confirm the existence of) an `EntityType` value type — a strongly-typed wrapper around `string` — and ensure `EntityUid.Type` uses it, mirroring the upstream intent.

Also removed upstream: the helper `EntityValueFromSlice([]string)` — check whether we have an equivalent and remove/deprecate it.

## Port Tasks

### 1. Introduce `EntityType` as a strongly-typed wrapper in `Cedar.Types`
- **Go source:** `types/value.go` line ~393 — `type EntityType string`
- **C# target:** `src/Cedar.Types/` — add a new file `EntityType.cs`
  - `public sealed record EntityType(string Value)` with implicit conversion from `string` and `ToString()` override returning `Value`.
  - Alternatively, if it already exists as a `record struct`, confirm it matches this shape.

### 2. Change `EntityUid.Type` from `string` to `EntityType`
- **Go source:** `types/value.go` — `EntityUID.Type EntityType`
- **C# target:** `src/Cedar.Types/EntityUid.cs` (or wherever `EntityUid` is defined)
  - Change the `Type` property from `string` to `EntityType`.
  - Update `NewEntityUID` / constructors to wrap the raw string in `EntityType(...)`.
  - Update `Cedar()` / `ToString()` to call `.Value` (or use implicit conversion) when building the Cedar string representation.

### 3. Update JSON serialization
- **Go source:** `types/json.go` — `extEntity.Type EntityType` and `entityValueJSON.Type *EntityType`
- **C# target:** wherever `EntityUid` JSON (de)serialization lives (likely `src/Cedar.Types/` or a converter in `Cedar.Ast`)
  - Ensure the `JsonConverter` for `EntityUid` reads/writes the `Type` field through `EntityType`, not raw `string`.

### 4. Remove or deprecate `EntityValueFromSlice` equivalent
- **Go source:** removed `EntityValueFromSlice([]string)` from `types/value.go`
- **C# target:** search for any helper that builds an `EntityUid` from a `string[]` path segments and remove or mark obsolete.

### 5. Update all call sites
- Parser, evaluator, test helpers that construct `EntityUID{Type: someString}` must now pass `EntityType(someString)`.
- **C# targets:** `src/Cedar.Ast/`, `src/Cedar.Core/`, `test/Cedar.Tests/` — any `new EntityUid(type: "Foo", ...)` or similar.

### 6. Add/update tests
- **C# target:** `test/Cedar.Tests/`
  - Add a test asserting `EntityType` implicit conversion from `string` works.
  - Add a test asserting `EntityUid.Type` is of type `EntityType`, not `string`.
  - Ensure existing `EntityUid` round-trip JSON tests still pass.

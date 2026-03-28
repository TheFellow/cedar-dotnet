PORT

## Commit
- **SHA:** d3f7472
- **Date:** 2024-11-06T14:15:42-07:00
- **Subject:** types: add EntityLoader interface for cases where a simple map isn't adequate for entity storage

## Semantic Analysis
The upstream cedar-go commit introduces an `EntityLoader` interface with a single method `Load(EntityUID) (Entity, bool)` and makes the concrete `Entities` map type implement it. All authorization and evaluation entry-points (`PolicySet.IsAuthorized`, `eval.Env`, `batch.Authorize`) are widened to accept `EntityLoader` instead of the concrete `Entities` map. This is a pure abstraction-widening change: callers who already pass `Entities` continue to work, but callers can now supply any custom entity store.

This is directly relevant to C#: the same abstraction should exist so consumers can implement lazy/remote/database-backed entity storage without materializing a full dictionary.

## Port Tasks

### 1. Add `IEntityLoader` interface — `src/Cedar.Types`
- **Target file:** `src/Cedar.Types/Entities.cs` (or a new `src/Cedar.Types/IEntityLoader.cs`)
- Define:
  ```csharp
  public interface IEntityLoader
  {
      bool TryLoad(EntityUid uid, out Entity entity);
  }
  ```
- Go source: `types/entities.go` lines 10-13 (`EntityLoader` interface with `Load(EntityUID) (Entity, bool)`)

### 2. Implement `IEntityLoader` on the `Entities` type — `src/Cedar.Types`
- **Target file:** `src/Cedar.Types/Entities.cs`
- The existing `Entities` type (dictionary wrapper or `ImmutableDictionary<EntityUid, Entity>`) should implement `IEntityLoader.TryLoad` by delegating to the dictionary lookup.
- Handle `null`/empty case gracefully (return `false`), mirroring the Go nil-map guard.
- Go source: `types/entities.go` lines 15-22 (`Entities.Load` method)

### 3. Update `IsAuthorized` signature — `src/Cedar.Ast`
- **Target file:** wherever `IsAuthorized(Entities entityMap, Request req)` is declared (search for `IsAuthorized` in `src/Cedar.Ast`).
- Change parameter type from `Entities` (or `IReadOnlyDictionary<EntityUid, Entity>`) to `IEntityLoader`.
- Go source: `authorize.go` line 21 (`func (p PolicySet) IsAuthorized(entities EntityLoader, req Request)`)

### 4. Update the internal eval `Env` struct — `src/Cedar.Core` or `src/Cedar.Ast`
- **Target file:** `src/Cedar.Core/Internal/Eval/Env.cs` (or equivalent linked file)
- Change the `Entities` field/property type from `Entities` / `IReadOnlyDictionary<EntityUid, Entity>` to `IEntityLoader`.
- Go source: `internal/eval/evalers.go` line 25 (`Entities types.EntityLoader`)

### 5. Update all eval call-sites that do `env.Entities[uid]` — `src/Cedar.Core/Internal/Eval` (linked into `Cedar.Ast`)
- Replace direct dictionary index access with `env.Entities.TryLoad(uid, out var entity)`.
- Go source sites:
  - `evalers.go` line 857 (attribute access)
  - `evalers.go` line 895 (`has` eval)
  - `evalers.go` lines 964, 968 (`entityInOne`)
  - `evalers.go` lines 993, 997 (`entityInSet`)
  - `partial.go` line 507 (partial `has` eval)

### 6. Update `Cedar.Batch` — `src/Cedar.Batch`
- **Target file:** wherever `Authorize(…, Entities entityMap, …)` is declared in `src/Cedar.Batch`.
- Change parameter type to `IEntityLoader`.
- Go source: `x/exp/batch/batch.go` line 105

### 7. Add tests — `test/Cedar.Tests` (and/or `test/Cedar.Batch.Tests`)
- Add a test that implements `IEntityLoader` with a custom backing store (e.g., a `Dictionary` behind a class) and passes it to `IsAuthorized`, verifying the result is identical to passing the equivalent `Entities` value.
- This validates the abstraction boundary works end-to-end.

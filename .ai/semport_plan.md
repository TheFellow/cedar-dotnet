PORT

## Commit Summary
**SHA:** 7dde92c  
**Message:** `types: remove EntityLoader interface`

The `EntityLoader` interface (with a single `Load(EntityUID) (Entity, bool)` method) is deleted from Go's `types` package. All call sites that previously accepted `EntityLoader` now accept the concrete `types.EntityMap` directly. This removes an abstraction layer and simplifies the API surface.

Affected Go files:
- `types/entities.go` — interface deleted
- `authorize.go` — `IsAuthorized` signature changed from `EntityLoader` → `types.EntityMap`
- `internal/eval/evalers.go` — `Env.Entities` field type changed
- `types.go` — re-export alias `EntityLoader = types.EntityLoader` removed
- `x/exp/batch/batch.go` — `Authorize` signature changed

## Semantic Analysis
The removal of `EntityLoader` is a deliberate simplification: the interface had only one implementation (`EntityMap`) and no external implementors needed. Removing it closes extension points that were never intended to be public.

In C#, if we have an `IEntityLoader` interface (or similar), the analogous change is:
1. Delete the interface.
2. Replace any parameter/field typed as `IEntityLoader` with `EntityMap` (our concrete dictionary type).
3. Update `IsAuthorized` / `Authorize` signatures accordingly.

## Concrete Port Tasks

### 1. Locate and identify the C# EntityLoader interface
- Search `src/` for any interface named `IEntityLoader` or `EntityLoader`.
- Expected locations: `src/Cedar.Types/` or `src/Cedar.Core/`.

### 2. Delete the interface
- If `IEntityLoader` (or equivalent) exists as a file, delete the file.
- If it is inline in another file, remove the interface declaration.

### 3. Update `IsAuthorized` signature
- File: likely `src/Cedar.Ast/Authorization.cs` or similar.
- Change parameter type from `IEntityLoader` → `EntityMap` (or our concrete entity collection type).

### 4. Update internal `Env` / evaluator struct
- File: likely `src/Cedar.Core/Internal/Eval/` (linked source).
- Change `Entities` field/property type from `IEntityLoader` → concrete `EntityMap`.

### 5. Update `Batch.Authorize` signature
- File: `src/Cedar.Batch/` — change parameter from interface to concrete type.

### 6. Update any re-export / type alias in public API surface
- Remove any `EntityLoader` type alias or re-export.

### 7. Fix all call sites (callers, tests)
- Search for `IEntityLoader` in test projects and update to pass `EntityMap` directly.

### 8. Verify build and tests pass
- `dotnet build cedar-dotnet.sln`
- `dotnet test cedar-dotnet.sln`

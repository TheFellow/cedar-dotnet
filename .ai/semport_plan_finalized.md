# Semport Finalized Plan — 7dde92c
## `types: remove EntityLoader interface`

---

## Decision: ACKNOWLEDGE (no C# changes needed)

### Rationale

The Go change removes `EntityLoader` because it had **only one implementation** (`EntityMap`) and no external implementors needed the interface. The interface was an unnecessary abstraction.

In our C# codebase, the equivalent is `IEntityGetter` (`src/Cedar.Types/IEntityGetter.cs`). However, **our situation is different**:

- `CountingEntityGetter` in `test/Cedar.Tests/Eval/EvaluatorTests.cs:40` is a test-only implementation of `IEntityGetter` that instruments `TryGet` call counts. Three tests rely on it:
  - `InEvaluator_InSet_ReevaluatesHierarchyWithoutCache` (line 445)
  - `InEvaluator_DeepHierarchy_ReevaluatesWithoutCache` (line 469)
  - `InEvaluator_Evaluations_AreIsolatedPerEnvironment` (line 486)
- These tests **verify behavioral semantics** (entity lookup isolation, no caching) that require a custom implementation — they cannot be replaced by `EntityMap` alone.

Therefore, keeping `IEntityGetter` is the correct C# design. Removing it would require restructuring these tests significantly with no behavioral benefit.

---

## C# ↔ Go Mapping (for reference)

| Go | C# | Location |
|---|---|---|
| `types.EntityLoader` interface | `IEntityGetter` interface | `src/Cedar.Types/IEntityGetter.cs:3` |
| `types.EntityMap` | `EntityMap` | `src/Cedar.Types/EntityMap.cs:8` |
| `authorize.go IsAuthorized(EntityLoader, ...)` | `Authorization.cs Authorize(IPolicyIterator, IEntityGetter, ...)` | `src/Cedar.Core/Authorization.cs:11` |
| `eval/evalers.go Env.Entities EntityLoader` | `EvalEnv.Entities IEntityGetter` | `src/Cedar.Core/Internal/Eval/EvalEnv.cs:5` |
| `batch.go Authorize(EntityLoader, ...)` | `BatchAuthorization.cs` (5 overloads, `IEntityGetter?`) | `src/Cedar.Batch/BatchAuthorization.cs:21,30,43,53,69` |

---

## Files Surveyed (no changes needed)

- `src/Cedar.Types/IEntityGetter.cs` — interface with single `TryGet(EntityUid, out Entity)` method
- `src/Cedar.Types/EntityMap.cs:8` — implements `IEntityGetter, IReadOnlyCollection<Entity>`
- `src/Cedar.Core/Authorization.cs:11` — `Authorize(IPolicyIterator, IEntityGetter, Request)`
- `src/Cedar.Core/Internal/Eval/EvalEnv.cs:5` — `EvalEnv(IEntityGetter Entities, ...)`
- `src/Cedar.Batch/BatchAuthorization.cs:21,30,43,53,69` — 5 overloads accept `IEntityGetter?`
- `src/Cedar.Experimental/EvalEnv.cs:9,22` — uses `IEntityGetter?`
- `test/Cedar.Tests/Eval/EvaluatorTests.cs:40` — `CountingEntityGetter : IEntityGetter` (test instrumentation)
- `test/Cedar.Tests/Types/EntityMapTests.cs:86` — typed as `IEntityGetter` (polymorphism check)
- `test/Cedar.Batch.Tests/BatchAuthorizationTests.cs:674` — helper uses `IEntityGetter?`

---

## Action Required

Run the following to acknowledge this commit:

```bash
python3 semport/ledger.py update 7dde92c acknowledged && python3 semport/ledger.py sort
git add semport/ledger.tsv && git commit -m "semport: acknowledge 7dde92c - IEntityGetter kept for test instrumentation"
rm -f .ai/semport_new_commits.md
```

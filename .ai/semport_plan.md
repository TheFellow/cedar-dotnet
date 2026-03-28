PORT

## Commit: 8a95a23 — `internal/eval: remove the inCache`
**Timestamp:** 2024-10-01T10:42:01-07:00

## Semantic Analysis

The upstream commit removes `inCache` — a per-evaluation `map[(EntityUID, EntityUID)]bool` — from Go's `Env` struct. The motivation:
- Benchmarks show the cache **hurts** performance for large, shallow entity graphs due to map allocation overhead.
- For deep graphs, a future transitive-closure cache on the entity store itself would be more effective (shared across authorizations).
- The cache was only hit within a single `Authorize` call, so cross-request sharing was never possible anyway.

Our C# codebase has an **exact analogue**: `EvalEnv.InCache` (`Dictionary<(EntityUid Lhs, EntityUid Rhs), bool>`) in `src/Cedar.Core/Internal/Eval/EvalEnv.cs:8`, used in `EntityInOne` in `src/Cedar.Core/Internal/Eval/Evaluators/MembershipEvaluators.cs:54-62`.

The same performance reasoning applies: `Dictionary` allocation on every `EvalEnv` construction hurts shallow graphs. Remove it.

## Port Tasks

### 1. `src/Cedar.Core/Internal/Eval/EvalEnv.cs` — Remove `InCache` property
- **Line 1**: Remove `using System.Collections.Generic;` (no longer needed after cache removal — verify no other usages remain in this file).
- **Line 8**: Delete `internal Dictionary<(EntityUid Lhs, EntityUid Rhs), bool> InCache { get; } = [];`

### 2. `src/Cedar.Core/Internal/Eval/Evaluators/MembershipEvaluators.cs` — Remove cache lookup/store in `EntityInOne`
- **Lines 53-62**: Simplify `EntityInOne` to remove the cache check (`env.InCache.TryGetValue`) and cache write (`env.InCache[key] = result`). The method body should just call through directly to `EntityInEntity`.
- Since `EntityInOne` no longer needs `EvalEnv` (only `env.Entities` is needed), change its signature to accept `IEntityGetter` instead of `EvalEnv`, matching the pattern already used by `EntityInEntity`.
- Update callers (`Contains` at line 46 and `EntityInSet` at line 70) to pass `env.Entities` instead of `env`.
- **Line 1**: Remove `using System.Collections.Generic;` if it was only needed for the `Dictionary` — but `HashSet` and `Stack` in `EntityInEntity` still need it, so keep it.

### 3. Verify tests still pass
- Run `dotnet test cedar-dotnet.sln` — all conformance and unit tests covering `in` expressions must remain green.
- No new tests needed; this is a pure removal of an optimization, not a behavioral change.

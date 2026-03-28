PORT

## Commit
d4f2002 — internal/eval: fix handling of entity pointers (2024-09-12)

## Semantic Analysis

This commit fixes a bug in entity hierarchy traversal used by the `in` operator. Two functions are affected:

### `entityInOne` and `entityInSet` (evalers.go lines ~933 and ~966)

**Bug**: When looking up an entity in `ctx.Entities[candidate]`, if the entity key was missing, Go returned a zero-value struct (empty `Parents` slice). This silently treated missing entities as leaf nodes with no parents — but crucially, the inner loop `ctx.Entities[k]` also had the same silent zero-value behavior, causing incorrect traversal decisions for parent nodes not in the entity store.

**Fix**: Both functions now use `if fe, ok := ctx.Entities[candidate]; ok { ... }` — if the candidate is not found in the entity store, the traversal block is skipped entirely. Similarly, for each parent `k`, the fix checks `p, ok := ctx.Entities[k]` and treats a missing parent as `!ok` (i.e., skip it) in the pruning condition.

**C# Semantic Impact**: In C#, dictionary lookups that miss a key throw `KeyNotFoundException` or return false from `TryGetValue`. The C# implementation likely already uses `TryGetValue`, but we must verify that:
1. Missing candidates are skipped (not treated as having zero parents).
2. Missing parent entries in the entity store short-circuit the traversal (treated same as "no parents"), rather than throwing or returning wrong results.

### `fold.go` — `string(pair.Key)` → `pair.Key`
This is a Go-specific type alias fix (the key type was already a string alias; explicit cast was unnecessary/wrong). No C# analog.

## Port Tasks

### 1. Locate C# entity `in` traversal logic
Find the C# equivalent of `entityInOne` and `entityInSet`:
- Search `src/Cedar.Ast/` and `src/Cedar.Core/Internal/Eval/` for methods implementing `in` operator evaluation (likely named something like `EntityIn`, `IsInSet`, or similar, possibly in an `Evalers` or `Authorization` file).
- Target files: likely `src/Cedar.Core/Internal/Eval/` linked files or `src/Cedar.Ast/Eval/`.

### 2. Verify missing-entity guard in candidate lookup
In the C# traversal loop, confirm that when `candidate` is not found in the entities dictionary:
- The traversal body is **skipped** (not executed with a default/empty entity).
- The loop continues to the next `todo` item or returns `false`.

If the code does `entities[candidate]` (indexer) without a guard, replace with `TryGetValue` and wrap the body in an `if (found)` block.

### 3. Verify missing-entity guard in parent lookup
Inside the traversal body, when iterating `fe.Parents` and looking up each parent `k` in the entity store:
- The pruning condition should treat a missing `k` entry the same as "has no parents" (i.e., skip adding `k` to `todo`).
- In Go: `p, ok := ctx.Entities[k]; if !ok || len(p.Parents) == 0 || ...`
- In C#: `if (!entities.TryGetValue(k, out var p) || p.Parents.Count == 0 || ...)`

If the C# code uses `entities[k].Parents` directly (which would throw on missing key), or `entities.ContainsKey(k)` followed by indexer (two lookups), replace with `TryGetValue` and update the condition.

### 4. Add regression test
Add an xUnit test in `test/Cedar.Tests/` that evaluates a policy using the `in` operator where the entity store contains an entity referencing a parent UID that is **not itself present** in the store. The authorization should not throw and should return the correct result (deny, since the missing parent cannot match).

Example scenario:
- Entity `User::"alice"` with parent `Group::"admins"`
- Entity store does NOT contain `Group::"admins"`
- Policy: `permit(principal in Group::"admins", action, resource);`
- Expected: Deny (alice's parent chain cannot be resolved to admins, but no crash)

PORT

## Commit Summary
**efe5690** — Merge PR #34: "General performance improvements and experimental batch mode"
**Date:** 2024-09-13
**Scope:** 26 files changed across `types/`, `internal/eval/`, `authorize.go`, `x/exp/batch/`

---

## Semantic Analysis

This large merge PR introduces several distinct, semantically meaningful changes:

### 1. Type Restructuring: `Reason`/`Error` → `DiagnosticReason`/`DiagnosticError`
Go renamed `cedar.Reason` → `types.DiagnosticReason` and `cedar.Error` → `types.DiagnosticError`. These are part of our public API surface (`Diagnostic.Reasons`, `Diagnostic.Errors`).
- **Go source:** `types/authorize.go` (new), `authorize.go` (type aliases)
- **C# target:** `src/Cedar.Core/` or `src/Cedar.Ast/` — check existing `Diagnostic`, `DiagnosticReason`, `DiagnosticError` types

### 2. `eval.Context` → `eval.Env` Rename
Internal eval context struct renamed for clarity. Constructors changed: `PrepContext` → `InitEnv`, new `NewEnv()` added. Cache extracted into struct field.
- **Go source:** `internal/eval/evalers.go`
- **C# target:** `src/Cedar.Core/Internal/Eval/` — our equivalent eval context type

### 3. `BoolEvaler` Wrapper + Compile Integration
`Compile(p)` now returns a `BoolEvaler` (typed bool eval wrapper) instead of raw `Evaler`. This simplifies call sites — no more `ValueToBool` calls at the authorization layer.
- **Go source:** `internal/eval/compile.go`
- **C# target:** Wherever `Compile()` is called in our eval pipeline

### 4. Compile-time Scope Optimization (Skip `all` scopes)
`Compile()` now skips generating scope-check nodes for principal/action/resource when the scope is `ScopeTypeAll` — avoids evaluating always-true conditions.
- **Go source:** `internal/eval/compile.go`
- **C# target:** Our equivalent policy compilation/scope-eval

### 5. `inCache` Memoization in `Env`
An `inKey { a, b EntityUID }` → bool cache is added to `Env` to memoize entity-in-set/in-hierarchy lookups during a single authorization call.
- **Go source:** `internal/eval/evalers.go` (inKey, inCache field)
- **C# target:** Our `in` evaluator — add a `Dictionary<(EntityUid, EntityUid), bool>` cache per-evaluation

### 6. Constant Folding (`fold.go`) — NEW FILE
`foldPolicy()` pre-processes an AST before evaluation to constant-fold pure sub-expressions (arithmetic, set membership, extension calls like `Decimal("42")`). Called by `Compile()`.
- **Go source:** `internal/eval/fold.go` (253 lines, new)
- **C# target:** `src/Cedar.Ast/` — new `PolicyFolder` or `AstFolder` class; called from `PolicyCompiler`

### 7. Partial Evaluation (`partial.go`) — NEW FILE
`PartialPolicy(env, policy)` partially evaluates a policy against an environment that may contain `Variable` or `Ignore` sentinel values. Core mechanism for batch mode.
- `Variable(name)` — sentinel EntityUID of type `__cedar::variable`
- `Ignore()` — sentinel EntityUID of type `__cedar::ignore`
- Permit policies drop conditions that reference ignored values; Forbid policies are dropped entirely when they reference ignored values
- **Go source:** `internal/eval/partial.go` (541 lines, new)
- **C# target:** `src/Cedar.Experimental/` — new `PartialEvaluator` class

### 8. Experimental Batch Authorization (`x/exp/batch/batch.go`) — NEW FILE
`Authorize(ctx, policySet, entities, batchRequest, callback)` — iterates over variable substitutions, partially evaluates policies per substitution, and invokes a callback per result.
- **Go source:** `x/exp/batch/batch.go` (373 lines, new)
- **C# target:** `src/Cedar.Batch/` — implement `BatchAuthorizer.Authorize()`

### 9. `authorize.go` Decision Logic Simplification
Old: accumulated `gotForbid`/`gotPermit` booleans, single return at end with complex `Decision(gotPermit && !gotForbid)`.
New: accumulates `forbids`/`permits` slices, early-returns `Deny` if any forbids, then `Allow` if any permits, else `Deny`.
- **Go source:** `authorize.go`
- **C# target:** Our `PolicySet.IsAuthorized()` — verify our logic matches the new early-return pattern

---

## Concrete Port Tasks

### Task A — Verify/Fix `DiagnosticReason` and `DiagnosticError` naming
- Read `src/Cedar.Core/` for existing diagnostic types
- Confirm our types are named `DiagnosticReason` and `DiagnosticError` (not `Reason`/`Error`)
- If misnamed, rename in all usages

### Task B — Verify `authorize` decision logic
- Read our `PolicySet.IsAuthorized()` in `src/Cedar.Ast/`
- Confirm it uses early-return forbid-wins logic rather than boolean accumulation
- Fix if it still uses old boolean accumulation

### Task C — Add `inCache` to eval environment
- Read `src/Cedar.Core/Internal/Eval/` for our eval env type
- Add a `Dictionary<(EntityUid a, EntityUid b), bool>` cache field to the eval env
- Read our `in` evaluator and thread the cache through it (check before recursing, store result)

### Task D — Implement constant folding (`fold.go` port)
- Create `src/Cedar.Ast/Evaluation/AstFolder.cs` (or equivalent)
- Implement `FoldPolicy(Policy p)` that traverses the AST and constant-folds pure sub-expressions
- Call `FoldPolicy` inside policy compilation before generating evalers
- Add xUnit tests in `test/Cedar.Tests/` covering: arithmetic folding, set folding, extension call folding, non-foldable expressions left intact

### Task E — Implement partial evaluation (`partial.go` port)
- Create `src/Cedar.Experimental/PartialEvaluator.cs`
- Implement `Variable(string name)` and `Ignore()` sentinel constructors
- Implement `PartialPolicy(EvalEnv env, Policy p)` returning `(Policy? reduced, bool keep)`
- Implement partial scope evaluation (principal/action/resource scope reduction)
- Add xUnit tests in `test/Cedar.Experimental.Tests/`

### Task F — Implement batch authorization (`x/exp/batch/batch.go` port)
- Read `src/Cedar.Batch/` for existing batch structure
- Implement `BatchRequest` with `Variables` (Dictionary<string, IReadOnlyList<Value>>) support
- Implement `BatchAuthorizer.Authorize(CancellationToken, PolicySet, Entities, BatchRequest, Action<BatchResult>)`
- Validate bound/unbound variables before iterating
- Add xUnit tests in `test/Cedar.Batch.Tests/`

### Task G — Scope optimization in compilation
- Read our policy compiler
- Skip generating scope-check eval nodes when scope is `ScopeAll`
- Add test: policy with `permit (principal, action, resource)` (all scopes) should evaluate without scope nodes

---

## Priority Order
1. **Task A** (naming correctness — public API)
2. **Task B** (decision logic correctness)
3. **Task C** (inCache performance — straightforward)
4. **Task D** (constant folding — prerequisite for E/F)
5. **Task G** (scope optimization — small)
6. **Task E** (partial eval — foundation for batch)
7. **Task F** (batch — builds on E)

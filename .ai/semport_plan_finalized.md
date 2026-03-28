# Semport Finalized Plan: cae58f3 — internal/eval: add constant folding

## Decision: ACKNOWLEDGE — Already Fully Implemented

### Summary of Go Commit
`cae58f3` adds compile-time constant folding to cedar-go's eval pipeline:
- Renames `bake`→`fold`, extends folding from container-literal-only to all operator types
- Introduces `tryFold`/`tryFoldBinary`/`tryFoldUnary` helpers that trial-evaluate a node and substitute a `NodeValue` if successful
- Adds `newIsInEval` (dedicated `is ... in ...` evaluator, replacing 3-node and+is+in composition)
- Widens constructor return types from concrete types to `Evaler` interface

### C# Status: 100% Already Implemented

Every semantic change in this Go commit exists in our C# codebase. Evidence:

| Go Change | C# Implementation | File |
|---|---|---|
| `foldPolicy` / `fold` / `FoldNode` | `ConstantFolder.FoldPolicy` / `FoldNode` | `src/Cedar.Core/Internal/Eval/ConstantFolder.cs:14,32` |
| `tryFold` with `CanEvaluate` guard + `ConstantEnv` trial-eval | `TryFold(INode)` using `ConstantEnv` + `CanEvaluate(INode)` | `src/Cedar.Core/Internal/Eval/ConstantFolder.cs:103,126` |
| Folding all operators: Add, Sub, Mult, Negate, Equals, …, And, Or, Not, Contains, ContainsAll, ContainsAny | All node types handled in `FoldNode` switch + `CanEvaluate` switch | `src/Cedar.Core/Internal/Eval/ConstantFolder.cs:34–68,126–211` |
| In/IsIn/Is/Has/NodeAccess blocked from folding (entity-runtime-dependent) | `CanEvaluate` returns `false` for `NodeIn`, `NodeIsIn`, `NodeIs`, `NodeAccess`, `NodeHas`, `NodeHasTag`, `NodeGetTag` | `src/Cedar.Core/Internal/Eval/ConstantFolder.cs:133–140` |
| `newIsInEval` — dedicated `is ... in ...` evaluator | `IsInEvaluator` sealed class with `is`-check short-circuit before `in` | `src/Cedar.Core/Internal/Eval/Evaluators/MembershipEvaluators.cs:25–39` |
| `Compile` calls `foldPolicy` | `Compiler.Compile` calls `ConstantFolder.FoldPolicy` | `src/Cedar.Core/Internal/Eval/Compiler.cs:12` |
| `fold_test.go` test cases | `ConstantFolderTests` with 18 test methods covering all cases | `test/Cedar.Tests/Eval/ConstantFolderTests.cs` |

### Key Architectural Notes (C# vs Go)

| Go Pattern | C# Equivalent | Notes |
|---|---|---|
| `Evaler` interface | `IEvaluator` interface | `src/Cedar.Core/Internal/Eval/IEvaluator.cs` |
| `literalEval` / `NodeValue` | `NodeValue` (AST node) / `LiteralEvaluator` | C# uses `NodeValue` at fold-time, `LiteralEvaluator` at eval-time |
| `EvalEnv` with dummy entity | `ConstantEnv` static field | `new EntityUid("__constant","__constant")` used as dummy PARC |
| `tryFold` returns `NodeValue` on success | `TryFold` returns `new NodeValue(value)` | Identical semantics |
| Error → return unfoldable node | `catch (EvalException) { return node; }` | Identical semantics |
| `tryFoldBinary`/`tryFoldUnary` helpers | Inlined into `FoldNode` switch + `TryFold` call | C# approach is slightly simpler: fold children in switch, then call `TryFold(folded)` unconditionally |
| Constructor return types widened to `Evaler` | N/A — C# never exposed concrete types | C# factory constructors already use primary constructors returning `IEvaluator` implicitly via interface |

### Acceptance Criteria: ALL MET ✅

- [x] `ConstantFolder.FoldPolicy` exists and is called from `Compiler.Compile`
- [x] All arithmetic/logical/comparison operators fold when all operands are constant
- [x] `NodeIn`, `NodeIsIn`, `NodeIs`, `NodeAccess`, `NodeHas`, `NodeHasTag`, `NodeGetTag` do NOT fold
- [x] `NodeVariable` does NOT fold
- [x] `NodeSet` and `NodeRecord` with all-constant elements fold to `NodeValue`
- [x] Partial folds (some children constant, some not) are preserved as partially-folded nodes
- [x] Eval errors during trial evaluation return the unfolded node (no exception propagation)
- [x] `IsInEvaluator` exists as a single dedicated evaluator (not 3-node composition)
- [x] Tests cover record-bake, set-bake, record-fold, set-fold, blocked cases

### Action
Mark `cae58f3` as **acknowledged** — no code changes required.

# Finalized Port Plan: efe5690

## Status: ALL TASKS ALREADY IMPLEMENTED

After scanning the C# codebase, every semantic change from this upstream merge commit has already been implemented. The C# codebase is **ahead of or equivalent to** the Go changes in efe5690.

---

## Task-by-Task Verification

### Task A — `DiagnosticReason` / `DiagnosticError` naming ✅ DONE
**Go change:** `cedar.Reason` → `types.DiagnosticReason`, `cedar.Error` → `types.DiagnosticError`
- `src/Cedar.Core/DiagnosticReason.cs:3` — `public sealed record DiagnosticReason(PolicyId PolicyId, Position Position);`
- `src/Cedar.Core/DiagnosticError.cs:3` — `public sealed record DiagnosticError(PolicyId PolicyId, Position Position, string Message)`
- `src/Cedar.Core/Diagnostic.cs:5` — uses `ImmutableArray<DiagnosticReason>` and `ImmutableArray<DiagnosticError>`
- Tests: `test/Cedar.Tests/Core/DiagnosticTests.cs`, `test/Cedar.Tests/Eval/DiagnosticTests.cs`
- **No action needed.**

### Task B — Authorization decision logic ✅ DONE
**Go change:** `Decision(gotPermit && !gotForbid)` → early-return forbid-wins
- `src/Cedar.Core/Authorization.cs:51-61` — already uses early-return pattern:
  ```csharp
  if (forbidReasons.Count > 0) return (Decision.Deny, ...);
  if (permitReasons.Count > 0) return (Decision.Allow, ...);
  return (Decision.Deny, ...);
  ```
- Tests: `test/Cedar.Tests/Eval/AuthorizeTests.cs`
- **No action needed.**

### Task C — `inCache` memoization in eval environment ✅ DONE
**Go change:** Added `inKey { a, b EntityUID }` → bool cache to `Env`
- `src/Cedar.Core/Internal/Eval/EvalEnv.cs:8` — `internal Dictionary<(EntityUid Lhs, EntityUid Rhs), bool> InCache { get; } = [];`
- `src/Cedar.Core/Internal/Eval/Evaluators/MembershipEvaluators.cs:52-62` — `EntityInOne` checks `env.InCache` before recursing and stores result
- **No action needed.**

### Task D — Constant folding ✅ DONE
**Go change:** New `fold.go` — `foldPolicy()` constant-folds pure AST sub-expressions before compilation
- `src/Cedar.Core/Internal/Eval/ConstantFolder.cs` (215 lines) — full implementation of `FoldPolicy(PolicyAst)` with `FoldNode`, `TryFold`, `CanEvaluate`
- `src/Cedar.Core/Internal/Eval/Compiler.cs:12` — `Compile()` calls `ConstantFolder.FoldPolicy(policy)` before `ScopeCompiler.CompilePolicy()`
- Tests: `test/Cedar.Tests/Eval/ConstantFolderTests.cs`
- **No action needed.**

### Task E — Partial evaluation ✅ DONE
**Go change:** New `partial.go` — `Variable()`, `Ignore()`, `PartialPolicy()` sentinels and partial evaluation
- `src/Cedar.Core/Internal/Eval/PartialEvaluator.cs` — `Variable()`, `Ignore()`, `IsVariable()`, `IsIgnore()`, `TryGetVariableName()`, `PartialPolicy()`, `PartialErrorExtensionName`, scope partial evaluation
- `src/Cedar.Experimental/PartialEvaluation.cs` — public façade: `Variable()`, `Ignore()`, `PartialError()`, `Evaluate()`, `ToNode()`
- `src/Cedar.Experimental/EvalEnv.cs` — public `EvalEnv` class with optional PARC (defaults to Variable sentinels)
- Tests: `test/Cedar.Experimental.Tests/PartialEvaluationTests.cs`
- **No action needed.**

### Task F — Batch authorization ✅ DONE
**Go change:** New `x/exp/batch/batch.go` — `Authorize()` with `Variable`/`Ignore` substitution, partial eval per substitution, callback
- `src/Cedar.Batch/BatchAuthorization.cs` — full implementation with `Authorize()` overloads, `Execute()` recursion, `PartialPolicies()`, `EmitResult()`, lazy `PolicyBatch.EnsureCompiled()`
- `src/Cedar.Batch/BatchRequest.cs` — `BatchRequest(ICedarData? Principal, ...) { Variables: IReadOnlyDictionary<string, IReadOnlyList<ICedarData>> }`
- `src/Cedar.Batch/BatchResult.cs` — `BatchResult(Request, IReadOnlyDictionary<string, ICedarData> Values, Decision, Diagnostic)`
- `src/Cedar.Batch/BatchVariable.cs` — `Variable()`, `Ignore()`, `IsVariable()`, `IsIgnore()`, `TryGetName()`
- `src/Cedar.Batch/BatchOption.cs` — `WithIgnoreForbid()`, `WithIgnorePermit()`, `WithCallback()`, `WithDiagnosticCallback()`
- `src/Cedar.Batch/BatchExceptions.cs` — `BatchMissingPartException`, `BatchInvalidPartException`
- Tests: `test/Cedar.Batch.Tests/BatchAuthorizationTests.cs`
- **No action needed.**

### Task G — Skip `ScopeAll` in compilation ✅ DONE
**Go change:** `Compile()` skips generating scope-check nodes when scope is `ScopeTypeAll`
- `src/Cedar.Core/Internal/Eval/ScopeCompiler.cs:54-62` — `AddScope()` returns early when `scope is ScopeAll`; the `Compile(variableName, scope)` method maps `ScopeAll => new NodeValue(CedarBool.True)` (used for standalone scope compile, not `CompilePolicy`)
- `src/Cedar.Core/Internal/Eval/ScopeCompiler.cs:11-36` — `CompilePolicy()` only adds scope nodes when scope is not `ScopeAll`
- Tests: `test/Cedar.Tests/Ast/ScopeTests.cs`, `test/Cedar.Tests/Eval/CompilerTests.cs`
- **No action needed.**

---

## Go → C# Pattern Mapping (for reference)

| Go Pattern | C# Equivalent |
|---|---|
| `type Env struct { ... }` | `internal sealed record EvalEnv(...)` |
| `type BoolEvaler struct { eval Evaler }` | `internal sealed class BoolEvaluator(IEvaluator inner)` |
| `type Evaler interface { Eval(*Env) }` | `internal interface IEvaluator { ICedarData Eval(EvalEnv env); }` |
| `map[inKey]bool` cache field | `Dictionary<(EntityUid Lhs, EntityUid Rhs), bool> InCache` |
| `foldPolicy(*ast.Policy) *ast.Policy` | `static PolicyAst FoldPolicy(PolicyAst policy)` |
| `PartialPolicy(env, p) (policy, keep)` | `static PolicyAst? PartialPolicy(EvalEnv env, PolicyAst policy, out bool keep, ...)` |
| `Callback func(Result)` | `Action<BatchResult>` |
| Go `error` return | `throw new EvalException(...)` / `catch (EvalException)` |
| `x/exp/batch` package | `Cedar.Batch` project |
| `cedar.Error` / `cedar.Reason` | `DiagnosticError` / `DiagnosticReason` sealed records |
| `Decision(gotPermit && !gotForbid)` | Early-return forbid-wins in `Authorization.Authorize()` |
| `Context.cancellation` via `ctx` param | `CancellationToken cancellationToken` param |

---

## Recommendation

**This commit should be marked `acknowledged`** — not `implemented`, because there are no changes to make. The C# implementation already contains all of these features, correctly implemented with .NET idioms (sealed records, `ImmutableArray`, `Dictionary` cache, `CancellationToken`, xUnit tests).

The ledger entry for `efe5690` should be updated:
```
python3 semport/ledger.py update efe5690 acknowledged
python3 semport/ledger.py sort
```

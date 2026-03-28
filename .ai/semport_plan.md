PORT

## Commit: cae58f3 — internal/eval: add constant folding

### Summary
Extends the compile-time optimization pass from "bake" (only pre-compute literal Sets, Records, and extension values) to full **constant folding**: recursively evaluate any sub-expression whose operands are all statically known values (no variables, no entity lookups). The result replaces the compound AST node with a single `literalEval` node. This is a meaningful runtime performance improvement — arithmetic, equality, comparisons, boolean logic, contains, etc. are all pre-computed once at policy compile time rather than on every authorization call.

### Semantic Changes

1. **Rename bake → fold** — `bakePolicy`/`bake` become `foldPolicy`/`fold`. Pure rename, but reflects the broader scope.

2. **Recursive constant folding for all operators** — `fold()` now handles every node type:
   - Arithmetic: Add, Sub, Mult, Negate
   - Comparison: Equals, NotEquals, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual
   - Logical: And, Or, Not
   - Set membership: Contains, ContainsAll, ContainsAny
   - Container literals: Record, Set (already existed in bake, now folds inner expressions too)
   - Variables: returned as-is (cannot fold)
   - In/IsIn: special-cased — cannot fold entity membership at compile time (runtime data needed)

3. **`tryFold` / `tryFoldBinary` / `tryFoldUnary` helpers** — Generic helpers that:
   - Recursively fold children
   - Attempt to evaluate the result using the existing evaler
   - If evaluation succeeds with a concrete value, return `NodeValue` (constant)
   - If it errors or children are not fully constant, return the partially-folded node

4. **`newIsInEval` introduced** — Consolidates the `IsIn` case (was: 3 eval nodes — `isEval`, `inEval`, `andEval`; now: 1 dedicated `isInEval` struct).

5. **Constructor return types broadened** — `newOrEval`, `newAndEval`, `newNotEval`, `newAddEval`, `newSubtractEval`, etc. now return `Evaler` (interface) instead of concrete pointer types. This enables fold helpers to substitute a `literalEval` without a type mismatch.

### Port Tasks

#### Task 1 — Rename Bake → Fold in C#
- **Go source:** `inspiration/cedar-go/internal/eval/bake.go` (now `fold.go`)
- **C# target:** `src/Cedar.Ast/Internal/Eval/` — find the file implementing `BakePolicy`/`Bake` (likely `Bake.cs` or similar)
- Rename the class/method from `Bake`/`BakePolicy` to `Fold`/`FoldPolicy` (or keep both as aliases if needed for back-compat)

#### Task 2 — Implement `TryFold` / `TryFoldBinary` / `TryFoldUnary` helpers
- **Go source:** `inspiration/cedar-go/internal/eval/fold.go` — `tryFold`, `tryFoldBinary`, `tryFoldUnary`
- **C# target:** `src/Cedar.Ast/Internal/Eval/Fold.cs` (new or renamed file)
- Implement static helper methods:
  ```csharp
  // Attempt to fold a unary node: fold child, try to evaluate, return NodeValue or reconstructed node
  static INode TryFoldUnary(INode child, Func<IEvaler, IEvaler> makeEvaler, Func<INode, INode> makeNode)
  
  // Attempt to fold a binary node: fold both children, try to evaluate, return NodeValue or reconstructed node
  static INode TryFoldBinary(INode left, INode right, Func<IEvaler, IEvaler, IEvaler> makeEvaler, Func<INode, INode, INode> makeNode)
  
  // General fold: fold N children, if all are NodeValue run the evaler, else reconstruct
  static INode TryFold(IReadOnlyList<INode> children, Func<IReadOnlyList<Value>, IEvaler> makeEvaler, Func<IReadOnlyList<INode>, INode> makeNode)
  ```
- Use a dummy `EvalContext` (empty env) for the compile-time trial evaluation
- Catch any eval errors and return the unfolded node

#### Task 3 — Extend `Fold()` dispatch to all node types
- **Go source:** `inspiration/cedar-go/internal/eval/fold.go` — the `fold()` switch statement
- **C# target:** `src/Cedar.Ast/Internal/Eval/Fold.cs` — the `Fold(INode)` method
- Add cases for every operator node type, wiring to `TryFoldBinary`/`TryFoldUnary`:
  - `NodeTypeAdd`, `NodeTypeSub`, `NodeTypeMult`, `NodeTypeNegate`
  - `NodeTypeEquals`, `NodeTypeNotEquals`
  - `NodeTypeGreaterThan`, `NodeTypeGreaterThanOrEqual`, `NodeTypeLessThan`, `NodeTypeLessThanOrEqual`
  - `NodeTypeAnd`, `NodeTypeOr`, `NodeTypeNot`
  - `NodeTypeContains`, `NodeTypeContainsAll`, `NodeTypeContainsAny`
  - `NodeTypeVariable` → return as-is
  - `NodeTypeIn` → fold children but do NOT evaluate (entity membership needs runtime data); return partially-folded node
  - `NodeTypeRecord` and `NodeTypeSet` → fold each element; if all yield `NodeValue`, collapse to single `NodeValue`

#### Task 4 — Introduce `IsInEval` (dedicated evaluator for `is ... in ...`)
- **Go source:** `inspiration/cedar-go/internal/eval/evalers.go` — `newIsInEval` (new struct combining is+in+and)
- **C# target:** `src/Cedar.Ast/Internal/Eval/Evalers.cs` or `IsInEval.cs`
- Add sealed class `IsInEval : IEvaler` with fields: `IEvaler Obj`, `EntityType EntityUid`, `IEvaler Entity`
- Implement `Eval(EvalContext)`: evaluate obj, check type matches `EntityType`, evaluate entity target, check `in` membership — short-circuit appropriately
- Wire `Convert.cs` (the `ToEval` switch) to use `new IsInEval(...)` for `NodeTypeIsIn`

#### Task 5 — Widen constructor return types (C# equivalent)
- **Go source:** `inspiration/cedar-go/internal/eval/evalers.go` — constructors now return `Evaler` interface
- **C# target:** `src/Cedar.Ast/Internal/Eval/Evalers.cs` (or individual evaler files)
- Change factory methods `NewOrEval`, `NewAndEval`, `NewNotEval`, `NewAddEval`, `NewSubtractEval`, etc. to return `IEvaler` instead of the concrete type
- This is required so `TryFold*` helpers can substitute a `LiteralEval` transparently

#### Task 6 — Wire `FoldPolicy` into `Compile`
- **Go source:** `inspiration/cedar-go/internal/eval/compile.go`
- **C# target:** `src/Cedar.Ast/Internal/Eval/Compile.cs` (or wherever `Compile(Policy)` lives)
- Replace call to `BakePolicy(p)` with `FoldPolicy(p)` in `Compile`

#### Task 7 — Tests
- **Go source:** `inspiration/cedar-go/internal/eval/fold_test.go`
- **C# target:** `test/Cedar.Tests/` — add `FoldTests.cs`
- Port the test cases:
  - `record-bake`: `Record({"key": true})` → `Value(Record{"key": True})`
  - `set-bake`: `Set(true)` → `Value(Set{True})`
  - `record-fold`: `Record({"key": 6*7})` → `Value(Record{"key": Long(42)})` ← NEW: arithmetic folded
  - `set-fold`: `Set(6*7)` → `Value(Set{Long(42)})` ← NEW
  - `record-blocked`: `Record({"key": 6 * context})` → unchanged (variable blocks folding)
  - `set-blocked`: `Set(6 * context)` → unchanged

### Notes
- The Go `In` case has commented-out code (fold attempted but reverted) — do not attempt to fold `In` in C# either; just fold children and reconstruct
- The `tryFold` trial evaluation uses a no-entity, no-context eval environment; any error aborts the fold and returns the node unchanged — match this behavior exactly
- This is a compile-time-only optimization; authorization semantics are unchanged

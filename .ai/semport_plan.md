PORT

## Commit Summary
- **SHA:** c4f7cd6
- **Date:** 2024-11-19T11:01:18-08:00
- **Title:** `internal/eval: use comparable value evalers in partial`

## Semantic Analysis
In Go's partial evaluation (`internal/eval/partial.go`), the four comparison operators (`>`, `>=`, `<`, `<=`) were using long-specific evaluators (`newLongGreaterThanEval`, etc.) when trying to fold/reduce nodes during partial evaluation. This meant that if a comparable non-long value (e.g. `datetime`) appeared in a comparison, partial evaluation would produce a spurious type error `"type error: expected long, got ..."` instead of either folding the result (if both operands are known) or leaving the node as-is (if an operand is unknown/variable).

The fix switches all four comparison operator cases to the generic comparable-value evaluators (`newComparableValueGreaterThanEval`, etc.), which accept `long`, `datetime`, `duration`, etc.

The secondary effect is that error message text changes from `"type error: expected long, got string"` to `"type error: expected comparable value, got string"` — this is a user-visible error message change.

## Relevant C# Architecture
Partial evaluation lives in `src/Cedar.Experimental`. The analogous dispatch is wherever the partial evaluator handles comparison AST nodes (`GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`).

## Concrete Port Tasks

### 1. Locate the partial evaluator comparison-node dispatch
- File: `src/Cedar.Experimental/PartialEvaluator.cs` (or similar — search for `GreaterThan` handling in Cedar.Experimental)
- Look for the switch/dispatch that handles `NodeType.GreaterThan`, `NodeType.LessThan`, etc.
- Confirm whether the evaluator called is long-only or comparable-value-generic.

### 2. Fix comparison dispatch in partial evaluator
- For `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual` cases: replace any calls to a long-specific comparison evaluator with the generic comparable-value evaluator (the same one used in full evaluation).
- The comparable evaluator must accept `CedarLong`, `CedarDatetime`, `CedarDuration` — i.e. any `IComparableCedarValue` (or however the C# codebase expresses that interface).

### 3. Update error message text (if applicable)
- If the C# evaluator produces the message `"type error: expected long, got ..."` for comparison type mismatches, update it to `"type error: expected comparable value, got ..."` to stay in sync with Cedar semantics.

### 4. Add tests in Cedar.Experimental.Tests
- Mirror the new Go test cases:
  - `opLessThanComparableKeep`: `Datetime(42ms) < Context()` → node kept as-is (partial, not folded)
  - `opLessThanComparableFold`: `Datetime(42ms) < Datetime(43ms)` → folds to `true`
  - `opLessThanOrEqualComparableKeep` / `opLessThanOrEqualComparableFold`
  - `opGreaterThanComparableKeep` / `opGreaterThanComparableFold`
  - `opGreaterThanOrEqualComparableKeep` / `opGreaterThanOrEqualComparableFold`
- Also verify the error message change: `42L > "bananas"` in partial eval should produce `"type error: expected comparable value, got string"`.

### Go Source References
- `inspiration/cedar-go/internal/eval/partial.go` lines ~343–350 (the four comparison cases in the `partial()` switch)
- `inspiration/cedar-go/internal/eval/partial_test.go` lines ~806–914 (new comparable keep/fold test cases)
- Error message string: `"type error: expected comparable value, got string"` (replaces `"type error: expected long, got string"`)

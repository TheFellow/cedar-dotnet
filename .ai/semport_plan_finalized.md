# Finalized Port Plan: c4f7cd6
## `internal/eval: use comparable value evalers in partial`

## Status: ALREADY IMPLEMENTED — ACKNOWLEDGE

The C# codebase already correctly implements the fix from this Go commit. No code changes are needed.

---

## Evidence

### Go fix (what changed upstream)
`inspiration/cedar-go/internal/eval/partial.go` lines 343–350:
- Changed four comparison-operator cases in the `partial()` switch from long-specific evaluators
  (`newLongGreaterThanEval`, etc.) to the generic comparable-value evaluators
  (`newComparableValueGreaterThanEval`, etc.).

### C# — PartialEvaluator already uses generic evaluators
**File:** `src/Cedar.Core/Internal/Eval/PartialEvaluator.cs` lines 344–367

All four comparison cases already dispatch to the generic evaluators:
```
NodeGreaterThan        → new GreaterThanEvaluator(left, right)       // line 348
NodeGreaterThanOrEqual → new GreaterThanOrEqualEvaluator(left, right) // line 354
NodeLessThan           → new LessThanEvaluator(left, right)           // line 360
NodeLessThanOrEqual    → new LessThanOrEqualEvaluator(left, right)    // line 366
```

### C# — Generic evaluators use ComparableValues.Compare (not long-only)
**File:** `src/Cedar.Core/Internal/Eval/Evaluators/ComparisonEvaluators.cs`
- `LessThanEvaluator.Eval` → `ComparableValues.Compare(...) < 0`  (line 26)
- `LessThanOrEqualEvaluator.Eval` → `ComparableValues.Compare(...) <= 0` (line 34)
- `GreaterThanEvaluator.Eval` → `ComparableValues.Compare(...) > 0`  (line 42)
- `GreaterThanOrEqualEvaluator.Eval` → `ComparableValues.Compare(...) >= 0` (line 50)
- `ComparableValues.Compare` handles `CedarLong`, `CedarString`, `CedarDecimal`,
  `CedarDatetime`, `CedarDuration` (lines 57–81).

### C# — Error message already matches Go post-fix wording
**File:** `src/Cedar.Core/Internal/Eval/Evaluators/ComparisonEvaluators.cs` lines 73, 78:
```csharp
throw new EvalException($"type error: expected comparable value, got {EvalErrors.TypeName(left)}");
throw new EvalException($"type error: expected comparable value, got {EvalErrors.TypeName(right)}");
```
This matches the updated Go error message exactly.

### C# — All 8 new test cases already present and passing
**File:** `test/Cedar.Experimental.Tests/PartialEvaluationTests.cs` lines 537–667

| Go test name                        | C# test method                                                              | Result |
|-------------------------------------|-----------------------------------------------------------------------------|--------|
| opLessThanComparableKeep            | DatetimeComparisonLessThan_WithVariableContext_PreservesResidualCondition    | ✅ PASS |
| opLessThanComparableFold            | DatetimeComparisonLessThan_FoldsToTrue                                      | ✅ PASS |
| opLessThanOrEqualComparableKeep     | DatetimeComparisonLessThanOrEqual_WithVariableContext_PreservesResidualCondition | ✅ PASS |
| opLessThanOrEqualComparableFold     | DatetimeComparisonLessThanOrEqual_FoldsToTrue                               | ✅ PASS |
| opGreaterThanComparableKeep         | DatetimeComparisonGreaterThan_WithVariableContext_PreservesResidualCondition | ✅ PASS |
| opGreaterThanComparableFold         | DatetimeComparisonGreaterThan_FoldsToFalse                                  | ✅ PASS |
| opGreaterThanOrEqualComparableKeep  | DatetimeComparisonGreaterThanOrEqual_WithVariableContext_PreservesResidualCondition | ✅ PASS |
| opGreaterThanOrEqualComparableFold  | DatetimeComparisonGreaterThanOrEqual_FoldsToFalse                           | ✅ PASS |

Full suite: 54 passed, 0 failed (`dotnet test test/Cedar.Experimental.Tests/` confirmed).

---

## Action Required

**ACKNOWLEDGE this commit.** Run:
```
python3 semport/ledger.py update c4f7cd6 acknowledged && python3 semport/ledger.py sort
git add semport/ledger.tsv && git commit -m "semport: acknowledge c4f7cd6 - already implemented in C# (comparable partial eval)"
rm -f .ai/semport_new_commits.md
```

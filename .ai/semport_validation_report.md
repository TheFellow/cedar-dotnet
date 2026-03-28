FAIL

## Build
dotnet build cedar-dotnet.sln: PASS (0 warnings, 0 errors)

## Test Results

| Project                  | Passed | Failed | Skipped | Total | Result |
|--------------------------|--------|--------|---------|-------|--------|
| Cedar.Tests              | 1034   | 1      | 0       | 1035  | FAIL   |
| Cedar.Schema.Tests       | 103    | 0      | 0       | 103   | PASS   |
| Cedar.Batch.Tests        | 34     | 0      | 0       | 34    | PASS   |
| Cedar.Experimental.Tests | 54     | 0      | 0       | 54    | PASS   |

## Failing Test

**Test:** `Cedar.Tests.Eval.EvaluatorTests.LessThanEvaluator_IncompatibleTypes_ThrowsEvalException`
**File:** `test/Cedar.Tests/Eval/EvaluatorTests.cs:237`

```
Assert.Equal() Failure: Strings differ
Expected: "incompatible types in comparison"
Actual:   "type error: expected comparable value, go"···
```

The test expects the error message `"incompatible types in comparison"` but the implementation produces `"type error: expected comparable value, ..."`. This is a pre-existing failure unrelated to the semport commit 9740663 (which introduced no code changes).

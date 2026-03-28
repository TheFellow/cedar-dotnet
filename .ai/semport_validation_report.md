FAIL

## Build
dotnet build cedar-dotnet.sln: PASS (0 warnings, 0 errors)

## Test Results

| Project                   | Passed | Failed | Skipped | Total | Result |
|---------------------------|--------|--------|---------|-------|--------|
| Cedar.Tests               |   1034 |      1 |       0 |  1035 | FAIL   |
| Cedar.Schema.Tests        |    103 |      0 |       0 |   103 | PASS   |
| Cedar.Batch.Tests         |     34 |      0 |       0 |    34 | PASS   |
| Cedar.Experimental.Tests  |     54 |      0 |       0 |    54 | PASS   |

## Failure Detail

**Cedar.Tests** — 1 failure:

- `Cedar.Tests.Eval.EvaluatorTests.LessThanEvaluator_IncompatibleTypes_ThrowsEvalException`
  - File: `test/Cedar.Tests/Eval/EvaluatorTests.cs:237`
  - Expected: `"incompatible types in comparison"`
  - Actual:   `"type error: expected comparable value, got ..."`
  - The test expects the old error message string. The `ComparisonEvaluators.cs` implementation was updated to emit `"type error: expected comparable value, got <type>"` (matching upstream Go), but this test was not updated to match.

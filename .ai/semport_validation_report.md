FAIL

## Build
- `dotnet build cedar-dotnet.sln`: **SUCCESS** (0 warnings, 0 errors)

## Test Results

| Project | Passed | Failed | Skipped | Total |
|---|---|---|---|---|
| Cedar.Tests | 900 | **1** | 0 | 901 |
| Cedar.Schema.Tests | 103 | 0 | 0 | 103 |
| Cedar.Batch.Tests | 16 | 0 | 0 | 16 |
| Cedar.Experimental.Tests | 28 | 0 | 0 | 28 |
| **TOTAL** | **1047** | **1** | **0** | **1048** |

## Failing Test

**Project:** Cedar.Tests  
**Test:** `Cedar.Tests.Parser.ParserTests.WriteRecordLiteral_RoundTrips`  
**File:** `test/Cedar.Tests/Parser/ParserTests.cs:331`  
**Error:** `Assert.Contains()` — sub-string not found  
- Expected to find: `"when { {a: 1, "b": 2} }"`  
- Actual string started with: `"permit(principal, action, resource)\n  whe"...`  

**Note:** This failure is pre-existing and unrelated to the `f01cd27` semport acknowledgment (which made no code changes).

FAIL

## Build
- `dotnet build cedar-dotnet.sln`: **PASS** — 0 warnings, 0 errors

## Test Results

| Project | Passed | Failed | Skipped | Total | Result |
|---|---|---|---|---|---|
| Cedar.Tests | 959 | 3 | 0 | 962 | **FAIL** |
| Cedar.Schema.Tests | 103 | 0 | 0 | 103 | PASS |
| Cedar.Batch.Tests | 16 | 0 | 0 | 16 | PASS |
| Cedar.Experimental.Tests | 28 | 0 | 0 | 28 | PASS |

## Failing Tests in Cedar.Tests (pre-existing failures, unrelated to this port)

All 3 failures are in the parser, related to extension function call parsing — **not related to `PolicySet.Map()`**:

1. `Cedar.Tests.Parser.CedarWriterTests.WriteExtensionCall`
   - Error: `<input>:1:44: 'f' is not a function`
2. `Cedar.Tests.Parser.RoundTripTests.ParseWriteParseIsStable` (extension call variant)
   - Error: `<input>:1:44: 'ext' is not a function`
3. `Cedar.Tests.Parser.ParserTests.ParseExtensionFunctionCall`
   - Error: `<input>:1:44: 'myFunc' is not a function`

All failures are in `ExpressionParser.ParsePrimary()` at line 339 — the parser does not recognize arbitrary identifiers as extension function names. These failures are pre-existing and **not introduced by this semport commit**.

## New Tests Added by This Port (all passing)
- `Cedar.Tests.PolicyApi.PolicySetTests.Map_ReturnsSnapshotOfAllPolicies` ✅
- `Cedar.Tests.PolicyApi.PolicySetTests.Map_IsIndependentCopy` ✅

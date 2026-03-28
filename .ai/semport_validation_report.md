FAIL

## Build
dotnet build cedar-dotnet.sln: PASS (0 warnings, 0 errors)

## Test Results

| Project                   | Passed | Failed | Skipped | Total | Result |
|---------------------------|--------|--------|---------|-------|--------|
| Cedar.Tests               |    936 |      3 |       0 |   939 | FAIL   |
| Cedar.Schema.Tests        |    103 |      0 |       0 |   103 | PASS   |
| Cedar.Batch.Tests         |     16 |      0 |       0 |    16 | PASS   |
| Cedar.Experimental.Tests  |     28 |      0 |       0 |    28 | PASS   |

## Failing Tests (Cedar.Tests)

1. `Cedar.Tests.Parser.CedarWriterTests.WriteExtensionCall`
   - Error: `<input>:1:44: 'f' is not a function`

2. `Cedar.Tests.Parser.ParserTests.ParseExtensionFunctionCall`
   - Error: `<input>:1:44: 'myFunc' is not a function`

3. `Cedar.Tests.Parser.RoundTripTests.ParseWriteParseIsStable` (source: `permit(principal, action, resource) when { ext(1, ...`)
   - Error: `<input>:1:44: 'ext' is not a function`

## Root Cause
All 3 failures are in the Cedar parser: extension/custom function calls (non-builtin function names) are being rejected with "'X' is not a function". These failures are pre-existing (unrelated to commit 432ab3e which only affected EntityUID.Type).

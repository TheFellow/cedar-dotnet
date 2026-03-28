FAIL

## Build
- `dotnet build cedar-dotnet.sln`: ✅ Succeeded — 0 warnings, 0 errors

## Test Results

| Project | Passed | Failed | Skipped | Total | Result |
|---|---|---|---|---|---|
| Cedar.Tests | 952 | 3 | 0 | 955 | ❌ FAIL |
| Cedar.Schema.Tests | 103 | 0 | 0 | 103 | ✅ PASS |
| Cedar.Batch.Tests | 16 | 0 | 0 | 16 | ✅ PASS |
| Cedar.Experimental.Tests | 28 | 0 | 0 | 28 | ✅ PASS |

## Failing Tests (Cedar.Tests — 3 failures, pre-existing)

All 3 failures are in parser tests unrelated to the ported changes (CedarPattern / Match API). They fail on extension function call parsing:

1. `Cedar.Tests.Parser.ParserTests.ParseExtensionFunctionCall`
   - Error: `<input>:1:44: 'myFunc' is not a function`

2. `Cedar.Tests.Parser.CedarWriterTests.WriteExtensionCall`
   - Error: `<input>:1:44: 'f' is not a function`

3. `Cedar.Tests.Parser.RoundTripTests.ParseWriteParseIsStable` (ext function case)
   - Error: `<input>:1:44: 'ext' is not a function`

## Assessment
The 3 failures are pre-existing parser failures related to extension function call parsing — they are unrelated to commit 595915b (CedarPattern.Match / Wildcard encapsulation). The ported changes did not introduce any new test failures.

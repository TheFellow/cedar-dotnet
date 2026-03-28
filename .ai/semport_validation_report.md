FAIL

## Build
dotnet build cedar-dotnet.sln: SUCCESS — 0 Warnings, 0 Errors

## Test Results

| Project | Passed | Failed | Skipped | Total |
|---------|--------|--------|---------|-------|
| Cedar.Tests | 960 | 3 | 0 | 963 |
| Cedar.Schema.Tests | 103 | 0 | 0 | 103 |
| Cedar.Batch.Tests | 16 | 0 | 0 | 16 |
| Cedar.Experimental.Tests | 28 | 0 | 0 | 28 |

## Failures in Cedar.Tests (3)

All 3 failures are **pre-existing** — they are unrelated to the `Position` JSON changes ported in this semport cycle. They all fail on extension function parsing (`is not a function`):

1. `Cedar.Tests.Parser.CedarWriterTests.WriteExtensionCall`
   - Error: `<input>:1:44: 'f' is not a function`

2. `Cedar.Tests.Parser.RoundTripTests.ParseWriteParseIsStable`
   - Source: `permit(principal, action, resource) when { ext(1, ...`
   - Error: `<input>:1:44: 'ext' is not a function`

3. `Cedar.Tests.Parser.ParserTests.ParseExtensionFunctionCall`
   - Error: `<input>:1:44: 'myFunc' is not a function`

All 3 failures occur in the expression parser at `ExpressionParser.cs:339` and are related to extension function call parsing — not affected by the `Position` serialization changes in this port.

## Conclusion
The 3 failures are pre-existing parser defects, not regressions introduced by the `Position` `[JsonPropertyName]` changes. The new `Position_JsonRoundTrip_UsesLowercaseKeys` test passed (included in the 960 passing tests).

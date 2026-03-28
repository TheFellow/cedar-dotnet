FAIL

## Build
dotnet build cedar-dotnet.sln: PASS (0 warnings, 0 errors)

## Test Results

| Project                  | Passed | Failed | Skipped | Total | Result |
|--------------------------|--------|--------|---------|-------|--------|
| Cedar.Tests              | 936    | 3      | 0       | 939   | FAIL   |
| Cedar.Schema.Tests       | 103    | 0      | 0       | 103   | PASS   |
| Cedar.Batch.Tests        | 16     | 0      | 0       | 16    | PASS   |
| Cedar.Experimental.Tests | 28     | 0      | 0       | 28    | PASS   |

## Failing Tests (Cedar.Tests — pre-existing failures unrelated to ace189d)

All 3 failures are in the parser, related to extension function call parsing:

1. `Cedar.Tests.Parser.CedarWriterTests.WriteExtensionCall`
   - Error: `<input>:1:44: \`f\` is not a function`

2. `Cedar.Tests.Parser.RoundTripTests.ParseWriteParseIsStable`
   - Source: `permit(principal, action, resource) when { ext(1, ...`
   - Error: `<input>:1:44: \`ext\` is not a function`

3. `Cedar.Tests.Parser.ParserTests.ParseExtensionFunctionCall`
   - Error: `<input>:1:44: \`myFunc\` is not a function`

All failures occur in `ExpressionParser.ParsePrimary()` at line 339 — the parser does not accept arbitrary extension function calls as free-standing identifiers. These failures are pre-existing and unrelated to the ace189d semport acknowledgment.

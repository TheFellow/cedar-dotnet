## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
Implemented the RFC 110 expanded-year datetime port and verified it with:

- `dotnet build cedar-dotnet.sln` ✅

### What changed

#### `src/Cedar.Types/CedarDatetime.cs`
- Reworked parsing to support:
  - standard 4-digit years
  - signed 9-digit expanded years (`+000010000`, `-000000001`)
- Removed dependence on `DateTimeOffset` for parse/format logic
- Added custom Gregorian civil-date ↔ epoch-milliseconds conversion
- Added canonical Cedar formatting for:
  - `0000..9999` as 4-digit years
  - `< 0` or `> 9999` as signed 9-digit years
- Added overflow handling for out-of-range timestamps

#### `src/Cedar.Core/Internal/DateTime/GregorianDateTime.cs`
- Added shared Gregorian calendar helper for:
  - leap years
  - days-in-month
  - day-of-year
  - ISO day-of-week
  - civil date conversion to/from Unix milliseconds

#### `src/Cedar.Core/Internal/Extensions/DatetimeExtensions.cs`
- Updated datetime component extension functions to use the new Gregorian helper instead of `ToDateTimeOffset()`
- This preserves semantics for expanded-year datetimes in:
  - `daysInMonth`
  - `year`
  - `month`
  - `day`
  - `dayOfWeek`
  - `dayOfYear`
  - `hour`
  - `minute`
  - `second`
  - `millisecond`

#### Tests
Updated / added focused xUnit coverage in:
- `test/Cedar.Tests/Types/CedarDatetimeTests.cs`
- `test/Cedar.Tests/Ast/VariableAndValueTests.cs`
- `test/Cedar.Tests/Eval/ExtensionTests.cs`

Coverage includes:
- expanded-year parse success
- expanded-year parse failures
- overflow boundaries
- canonical Cedar formatting
- JSON round-trip with `System.Text.Json`
- AST value creation from expanded-year strings
- extension-function behavior on expanded-year datetimes

### Notes
- `ToDateTimeOffset()` remains for existing in-range use, but formatting and semantic datetime operations no longer depend on it.
- I also handled a couple of build issues caused by assembly-internal visibility and duplicate test insertion during implementation.

If you want, I can next update the semport ledger for `c1177bf` and prepare the commit.

## Task
Validate the ported changes. Run these commands from repo root:
1. `dotnet build cedar-dotnet.sln` — must succeed with 0 warnings
2. `dotnet test test/Cedar.Tests/` — unit tests must pass
3. `dotnet test test/Cedar.Schema.Tests/` — schema tests must pass
4. `dotnet test test/Cedar.Batch.Tests/` — batch tests must pass
5. `dotnet test test/Cedar.Experimental.Tests/` — experimental tests must pass

NOTE: Do NOT run test/Cedar.Conformance/ — it has known failures unrelated to this port.

Write results to .ai/semport_validation_report.md. First line: PASS if all above pass, FAIL otherwise. Include test counts.

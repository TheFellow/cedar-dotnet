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
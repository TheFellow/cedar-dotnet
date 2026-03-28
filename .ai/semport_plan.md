PORT

## Commit: d1f59f4
**Date:** 2024-09-19T12:29:46-07:00
**Subject:** Add the extension types, datetime and duration (RFC 80)

---

## Semantic Analysis

This commit introduces two new Cedar extension types per [RFC 80](https://github.com/cedar-policy/rfcs/blob/main/text/0080-datetime-extension.md). These are **first-class Cedar values** with parsing, comparison, arithmetic, JSON serialization, and Cedar policy expression support. Full porting is required.

### New Types

#### `Datetime` (Go: `types/datetime.go`)
- Internally stores milliseconds since Unix epoch as `int64`
- Constructed via:
  - `datetime("ISO-8601 string")` — parses ISO-8601 date/datetime strings
  - `FromStdTime(time.Time)` — wraps a Go `time.Time`
- Methods:
  - `toDate()` → strips time component (truncates to midnight UTC)
  - `toTime()` → returns `Duration` representing time-of-day offset from midnight
  - `offset(Duration)` → returns new `Datetime` shifted by a `Duration`
  - `durationSince(Datetime)` → returns `Duration` between two `Datetime` values
- Comparisons: `<`, `<=`, `>`, `>=`, `==` (implements `ComparableValue` interface)
- JSON: serializes as `{ "__extn": { "fn": "datetime", "arg": "ISO-8601" } }`
- Cedar text: `datetime("ISO-8601")`

#### `Duration` (Go: `types/duration.go`)
- Internally stores total milliseconds as `int64`
- Valid units (in order, largest to smallest): `d` (days), `h` (hours), `m` (minutes), `s` (seconds), `ms` (milliseconds)
- Constructed via:
  - `duration("NdNhNmNsNms")` — parses Cedar duration string
  - `DurationFromMillis(int64)`
  - `FromStdDuration(time.Duration)`
- Parsing rules:
  - Units must appear in decreasing order (d > h > m > s > ms)
  - Each unit may appear at most once
  - No duplicate or out-of-order units
  - Supports negative prefix (e.g., `-1h`)
  - Overflow of int64 milliseconds is an error
- Methods:
  - `toMilliseconds()` → `Long` (total ms)
  - `toSeconds()` → `Long` (total seconds, truncated)
  - `toMinutes()` → `Long` (total minutes, truncated)
  - `toHours()` → `Long` (total hours, truncated)
  - `toDays()` → `Long` (total days, truncated)
- Comparisons: `<`, `<=`, `>`, `>=`, `==`
- JSON: serializes as `"NdNhNmNsNms"` string (Cedar extension JSON format)
- Cedar text: `duration("NdNhNmNsNms")`

### New Extension Functions (Go: `internal/eval/evalers.go`)
Registered in `newExtensionEval`:
- `datetime(str)` → `Datetime` literal constructor
- `duration(str)` → `Duration` literal constructor
- `toDate(datetime)` → `Datetime`
- `toTime(datetime)` → `Duration`
- `offset(datetime, duration)` → `Datetime`
- `durationSince(datetime, datetime)` → `Duration`
- `toMilliseconds(duration)` → `Long`
- `toSeconds(duration)` → `Long`
- `toMinutes(duration)` → `Long`
- `toHours(duration)` → `Long`
- `toDays(duration)` → `Long`

### New Comparison Infrastructure (Go: `internal/eval/comparable.go`)
- `ComparableValue` interface with `LessThan(Value) (bool, error)` and `LessThanOrEqual(Value) (bool, error)`
- Generic evaluators: `comparableValueLessThanEval`, `comparableValueGreaterThanEval`, `comparableValueLessThanOrEqualEval`, `comparableValueGreaterThanOrEqualEval`
- `ErrNotComparable` error sentinel (Go: `types/value.go`)

### New Constants (Go: `internal/consts/consts.go`)
- `MillisPerSecond = 1000`
- `MillisPerMinute = 60000`
- `MillisPerHour = 3600000`
- `MillisPerDay = 86400000`

### Extension Registration (Go: `internal/extensions/extensions.go`)
- `datetime` and `duration` extension functions registered with arity 1
- `toDate`, `toTime`, `offset`, `durationSince`, `toMilliseconds`, `toSeconds`, `toMinutes`, `toHours`, `toDays` registered with arities 1 or 2

---

## Port Tasks

### Task 1: Add `CedarDatetime` value type
**Target:** `src/Cedar.Types/CedarDatetime.cs` (new file)
- Sealed record wrapping `long` (milliseconds since Unix epoch)
- Implements `ICedarValue`, `IComparableCedarValue` (or equivalent pattern)
- Static `Parse(string iso8601)` — accept ISO-8601 date (`YYYY-MM-DD`) and datetime strings
- `FromDateTimeOffset(DateTimeOffset)` factory
- Instance methods: `ToDate()`, `ToTime()`, `Offset(CedarDuration)`, `DurationSince(CedarDatetime)`
- JSON: `{ "__extn": { "fn": "datetime", "arg": "..." } }` via `System.Text.Json`
- Cedar text rendering: `datetime("...")`
- `==`, comparison operators

### Task 2: Add `CedarDuration` value type
**Target:** `src/Cedar.Types/CedarDuration.cs` (new file)
- Sealed record wrapping `long` (total milliseconds)
- Implements `ICedarValue`, `IComparableCedarValue`
- Static `Parse(string durationStr)` — parse Cedar duration format (`NdNhNmNsNms`)
  - Enforce unit order: d → h → m → s → ms
  - Each unit at most once, no out-of-order, overflow detection
  - Support negative prefix
- `FromMilliseconds(long)` factory
- `FromTimeSpan(TimeSpan)` factory
- Instance methods: `ToMilliseconds()`, `ToSeconds()`, `ToMinutes()`, `ToHours()`, `ToDays()`
- JSON: `"NdNhNmNsNms"` string
- Cedar text: `duration("NdNhNmNsNms")`

### Task 3: Add parsing error types / sentinels
**Target:** `src/Cedar.Types/CedarErrors.cs` or existing error file
- `CedarDatetimeParseException` or error sentinel
- `CedarDurationParseException` or error sentinel
- `ErrNotComparable` equivalent (type mismatch in comparison)

### Task 4: Register extension functions in evaluator
**Target:** `src/Cedar.Ast/` — wherever extension functions are dispatched (likely `ExtensionEval` or similar)
- Register `datetime(str)` → `CedarDatetime`
- Register `duration(str)` → `CedarDuration`
- Register `toDate`, `toTime`, `offset`, `durationSince` (datetime methods)
- Register `toMilliseconds`, `toSeconds`, `toMinutes`, `toHours`, `toDays` (duration methods)
- Arity checking (1 or 2 args depending on function)

### Task 5: Add comparison evaluator support
**Target:** `src/Cedar.Ast/` — comparison eval logic
- `<`, `<=`, `>`, `>=` operators must work for `CedarDatetime` and `CedarDuration`
- These types are comparable with themselves but not with other types (return type error)

### Task 6: JSON serialization
**Target:** `src/Cedar.Types/` — JSON converters
- `CedarDatetime` → `{ "__extn": { "fn": "datetime", "arg": "ISO-8601" } }`
- `CedarDuration` → `{ "__extn": { "fn": "duration", "arg": "Nms" } }` (or canonical form)
- Deserialization support for both formats

### Task 7: Add constants
**Target:** `src/Cedar.Types/CedarDuration.cs` or a constants file
- `MillisPerSecond = 1000L`
- `MillisPerMinute = 60_000L`
- `MillisPerHour = 3_600_000L`
- `MillisPerDay = 86_400_000L`

### Task 8: Tests
**Target:** `test/Cedar.Tests/` — new test files
- `CedarDatetimeTests.cs`: parse valid/invalid ISO-8601, comparison, `toDate`, `toTime`, `offset`, `durationSince`, JSON round-trip
- `CedarDurationTests.cs`: parse valid/invalid duration strings, unit order enforcement, overflow, `toMilliseconds`–`toDays`, comparison, JSON round-trip
- `ExtensionEvalTests.cs` (or existing): policy eval tests exercising `datetime()` and `duration()` extension calls, arity errors

---

## Key Invariants to Preserve
- `Datetime` comparison only valid between two `Datetime` values (type error otherwise)
- `Duration` comparison only valid between two `Duration` values (type error otherwise)
- Duration parsing: units must be in strict order d→h→m→s→ms, each at most once
- Duration arithmetic is truncating (integer division), not rounding
- `toDate()` truncates to midnight UTC (strips time component)
- `toTime()` returns milliseconds elapsed since midnight UTC as a `Duration`
- Millisecond precision throughout (no sub-millisecond support)

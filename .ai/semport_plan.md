PORT

## Commit Summary
**SHA:** 537c4d8  
**Date:** 2024-09-18  
**Title:** Add `datetime` extension type to Cedar

Adds a `Datetime` Cedar value type backed by an `int64` (milliseconds since Unix epoch). Introduces:
- `Datetime` type with parsing from ISO 8601 strings (`datetime()` extension function)
- JSON marshaling/unmarshaling via `__extn` envelope (`{"__extn":{"fn":"datetime","arg":"..."}}`)
- `Lesser` interface (`Less`, `LessEqual`) implemented by `Long` and `Datetime` for operator overloading
- "Virtual" comparison evalers (`<`, `<=`, `>`, `>=`) that dispatch through `Lesser`
- `toDate()` extension method that truncates a datetime to midnight UTC (floors to day boundary)
- `ErrDatetime` sentinel error for parse failures

Does NOT add: `duration` type, `toTime()`, `offset()`, or `durationSince()` methods.

---

## Semantic Analysis

### New type: `Datetime`
- Go file: `types/datetime.go` (new)
- Backed by `int64` milliseconds since Unix epoch (UTC)
- Parsed from ISO 8601 strings via `time.Parse(time.RFC3339, s)` → convert to ms
- Implements `Value`, `Lesser` (with `Less` and `LessEqual`)
- Explicit JSON: `{"__extn":{"fn":"datetime","arg":"<iso8601>"}}`
- Implicit JSON: bare string `"<iso8601>"`
- `MarshalCedar()` → `datetime("<iso8601>")`

### `Lesser` interface
- Go file: `types/virtual.go` (new)
- Interface: `Value` + `Less(Value) bool` + `LessEqual(Value) bool`
- `Long` gains `Less` and `LessEqual` — Go file: `types/long.go`

### Virtual comparison evalers
- Go file: `internal/eval/evalers.go`
- `virtualLessThanEval`, `virtualGreaterThanEval`, `virtualLessThanOrEqualEval`, `virtualGreaterThanOrEqualEval`
- Each calls `evalLesser(evaler, env)` to assert both sides implement `Lesser`, then delegates to `Less`/`LessEqual`
- These are wired into the AST node builder alongside (not replacing) the existing long-only comparisons

### `toDate()` eval
- Go file: `internal/eval/evalers.go` — `toDateEval`
- Truncates datetime ms to day boundary: `value - (value % millisPerDay)` where `millisPerDay = 86_400_000`

### `ErrDatetime` sentinel
- Go file: `types/value.go`

---

## Port Tasks

### 1. Add `CedarDatetime` value type to `Cedar.Types`
**Target:** `src/Cedar.Types/CedarDatetime.cs` (new file)

- `public sealed record CedarDatetime(long MillisecondsSinceEpoch) : CedarValue`
- Parse from ISO 8601 string using `DateTimeOffset.Parse(s, null, DateTimeStyles.RoundtripKind)` → convert to ms
- Throw / return error for unparseable strings (analogous to `ErrDatetime`)
- Implement `ExplicitMarshalJson` → `{"__extn":{"fn":"datetime","arg":"<iso8601>"}}`
- Implement `UnmarshalJson` for both explicit and implicit forms
- Implement `MarshalCedar()` → `datetime("<iso8601>")`
- Implement comparison: `IComparable<CedarDatetime>` (for operator overloading support)

### 2. Add `ILesser` interface to `Cedar.Types`
**Target:** `src/Cedar.Types/ILesser.cs` (new file)

- `public interface ILesser : ICedarValue { bool Less(ICedarValue other); bool LessEqual(ICedarValue other); }`
- Implement on `CedarDatetime` and `CedarLong`

### 3. Implement `ILesser` on `CedarLong`
**Target:** `src/Cedar.Types/CedarLong.cs` (existing)

- Add `bool Less(ICedarValue other)` → cast to `CedarLong`, return `this.Value < other.Value`
- Add `bool LessEqual(ICedarValue other)` → `this.Value <= other.Value`

### 4. Add virtual comparison evalers to `Cedar.Ast`
**Target:** `src/Cedar.Core/Internal/Eval/Evalers.cs` (or equivalent linked file)

- Add `VirtualLessThanEval`, `VirtualGreaterThanEval`, `VirtualLessThanOrEqualEval`, `VirtualGreaterThanOrEqualEval`
- Each: evaluate both sides, assert they implement `ILesser`, call `Less`/`LessEqual`
- Wire into the comparison node builder (alongside existing long-only evalers)

### 5. Add `toDate()` eval
**Target:** `src/Cedar.Core/Internal/Eval/Evalers.cs` (or equivalent)

- `ToDateEval`: evaluate lhs as `CedarDatetime`, truncate to day: `ms - (ms % 86_400_000L)`
- Register `datetime.toDate` in the extension function dispatch table

### 6. Add `datetime()` extension function registration
**Target:** wherever `ip()` and `decimal()` extension functions are registered

- Parse string arg → `CedarDatetime` or error with `ErrDatetime`-equivalent

### 7. Add `ErrDatetime` equivalent
**Target:** `src/Cedar.Types/CedarErrors.cs` or similar

- `public static readonly Exception ErrDatetime = new FormatException("error parsing datetime value");`
- Or use a strongly-typed error pattern matching existing conventions

### 8. Tests
**Target:** `test/Cedar.Tests/CedarDatetimeTests.cs` (new file)

- Parse valid ISO 8601 strings → correct ms value
- Parse invalid strings → error
- JSON round-trip: explicit and implicit forms
- `Less`, `LessEqual` comparisons
- `datetime.toDate()` truncation
- Virtual comparison evalers via policy evaluation (e.g., `datetime("2024-01-02") > datetime("2024-01-01")`)
- `MarshalCedar()` output format

---

## Key Constants
- `millisPerDay = 1_000 * 60 * 60 * 24 = 86_400_000L`
- ISO 8601 / RFC 3339 format: `"yyyy-MM-dd'T'HH:mm:ssK"` (use `DateTimeOffset.Parse` with `RoundtripKind`)
- JSON fn name: `"datetime"`

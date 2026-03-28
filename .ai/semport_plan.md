PORT

## Commit Summary
**SHA:** e8728bb  
**Date:** 2024-09-18  
**Title:** Add a duration type, as per RFC 80

Introduces a new first-class Cedar extension type `duration` (stored as milliseconds in an `int64`/`long`), alongside a parser (`ParseDuration`), a canonical `String()` representation, JSON (de)serialization, evaluation of the `duration(...)` constructor, and eight new extension methods:

- `datetime.toTime()` → `Duration` (time-of-day component)
- `datetime.offset(Duration)` → `Datetime`
- `datetime.durationSince(Datetime)` → `Duration`
- `duration.toMilliseconds()` → `Long`
- `duration.toSeconds()` → `Long`
- `duration.toMinutes()` → `Long`
- `duration.toHours()` → `Long`
- `duration.toDays()` → `Long`

---

## Semantic Analysis

### New type: `Duration`
- Struct wrapping a single `int64` of milliseconds (signed — allows negative durations).
- Parser accepts: optional leading `-`, then one or more `<quantity><unit>` pairs where units are `d`, `h`, `m`, `s`, `ms` in strictly descending order. Returns `ErrDuration`-wrapped errors on invalid input.
- Canonical string form: omits zero-valued units, e.g. `"1d12h"`, always ends with at least one unit.
- `Equal(Value)` — type-checked equality by millisecond value.
- `MarshalCedar()` — emits `duration("<canonical-string>")`.
- JSON: deserialized from `{"__extn":{"fn":"duration","arg":"<string>"}}`.
- Error sentinel: `ErrDuration = errors.New("error parsing duration value")`.

### Evaluation additions
- `durationLiteralEval` — evaluates `duration(string-literal)` extension call → `Duration`.
- `toTimeEval` — `datetime.toTime()`: returns `Duration{ Value = datetime.Value % millisPerDay }`.
- `toMillisecondsEval` — `duration.toMilliseconds()`: returns `Long(duration.Value)`.
- `toSecondsEval` — `duration.toSeconds()`: returns `Long(duration.Value / 1000)`.
- `toMinutesEval` — `duration.toMinutes()`: returns `Long(duration.Value / 60_000)`.
- `toHoursEval` — `duration.toHours()`: returns `Long(duration.Value / 3_600_000)`.
- `toDaysEval` — `duration.toDays()`: returns `Long(duration.Value / 86_400_000)`.
- `offsetEval` — `datetime.offset(duration)`: returns `Datetime{ Value = datetime.Value + duration.Value }`.
- `durationSinceEval` — `datetime.durationSince(datetime2)`: returns `Duration{ Value = datetime.Value - datetime2.Value }`.

---

## Port Tasks

### 1. New value type `CedarDuration` in `src/Cedar.Types`
**Go source:** `types/duration.go` (new file in commit)  
**C# target:** `src/Cedar.Types/CedarDuration.cs` (new file)

- `public sealed record CedarDuration(long Milliseconds) : CedarValue`
- Implement `ParseDuration(string) : CedarDuration` (static) — parse optional `-`, then greedy `<qty><unit>` pairs with strict descending-unit ordering; throw/return `ErrDuration`-compatible error.
- Implement `ToString()` → canonical form (d/h/m/s/ms, skip zero units).
- Implement `Equal(CedarValue)` type-checked equality.
- Add `ErrDuration` / `CedarDurationParseException` error type or sentinel string.

### 2. JSON deserialization in `src/Cedar.Types` (or `src/Cedar.Core`)
**Go source:** `types/json.go` lines ~75-87 (new `"duration"` case in `UnmarshalJSON`)  
**C# target:** wherever Cedar extension-value JSON dispatch lives (look for `"datetime"` or `"decimal"` JSON case)

- Add `"duration"` case to the `__extn.fn` switch that calls `CedarDuration.ParseDuration(arg)`.

### 3. `MarshalCedar()` / JSON serialization for `CedarDuration`
**Go source:** `types/duration.go` — `MarshalCedar()` returns `duration("<canonical>")`.  
**C# target:** `CedarDuration.cs` — implement serialization to match `duration("1d12h")` format.

### 4. Evaluation — `durationLiteralEval` and new extension-method evals
**Go source:** `x/exp/eval/eval.go` — `newDurationLiteralEval`, `newToTimeEval`, `newToMillisecondsEval`, `newToSecondsEval`, `newToMinutesEval`, `newToHoursEval`, `newToDaysEval`, `newOffsetEval`, `newDurationSinceEval`  
**C# target:** wherever Cedar extension function dispatch and eval nodes live (look for `DatetimeLiteralEval`, `ToDateEval` in `src/Cedar.Ast` or `src/Cedar.Core/Internal/Eval`)

- Register `"duration"` constructor in extension eval dispatch (alongside `"datetime"`, `"decimal"`, `"ip"`).
- Add eval nodes for all 8 new methods; register them in the extension name→eval-node switch.
- Helper: `evalDuration(node, env)` — evaluate a node, assert result is `CedarDuration`, else type error.

### 5. `evalDatetime` helper may need extension
**Go source:** `x/exp/eval/eval.go` — `evalDatetime` helper used by `toTimeEval`, `offsetEval`, `durationSinceEval`.  
**C# target:** same eval file — ensure `EvalDatetime` helper exists (likely already added with datetime PR).

### 6. Tests in `test/Cedar.Tests`
**Go source:** `types/duration_test.go`, `x/exp/eval/eval_test.go` (duration section), `types/json_test.go`

- `CedarDurationParseTests` — round-trip normalization cases (e.g. `"60m"` → `"1h"`, `"36h"` → `"1d12h"`).
- Parse-error cases matching Go's error messages (string too short, unexpected unit order, overflow, etc.).
- `Equal()` tests.
- `MarshalCedar()` test (`duration("42ms")`).
- JSON deserialization test (`{"__extn":{"fn":"duration","arg":"1d12h30m30s500ms"}}`).
- Eval tests for all 8 new methods (toTime, offset, durationSince, toMilliseconds, toSeconds, toMinutes, toHours, toDays).

---

## Key Constants (from Go source)
```
millisPerDay    = 86_400_000
millisPerHour   =  3_600_000
millisPerMinute =     60_000
millisPerSecond =      1_000
```

## Parser Rules (from Go `ParseDuration`)
- Minimum valid input: at least one `<digit(s)><unit>` pair (or negative prefix + pair).
- Units in order (largest to smallest): `d`, `h`, `m`, `s`, `ms`.
- Each unit may appear at most once; must appear in strictly descending order.
- Overflow: if accumulated ms overflows `int64`, return overflow error.
- No whitespace permitted anywhere.
- After parsing all pairs, the string must be fully consumed.
- Special error: if a quantity is present but its unit is a repeat or out-of-order unit, emit "invalid duration".

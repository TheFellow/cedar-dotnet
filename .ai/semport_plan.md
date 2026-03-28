PORT

## Commit: a4e576e — "Address PR Feedback"
Date: 2024-09-18T16:18:37-07:00

## Semantic Analysis

This commit makes several meaningful behavioral and API changes that affect the C# implementation:

### 1. New `ErrNotComparable` sentinel error
- **Go**: `var ErrNotComparable = fmt.Errorf("incompatible types in comparison")` in `types/value.go`
- **C# target**: Add a corresponding `CedarTypeError` or exception/error value for incompatible comparison types. Check if `Cedar.Types` or `Cedar.Ast` already has a TypeError mechanism.
- **Impact**: Comparison operations must return an error (not just bool) when types are incompatible.

### 2. `Lesser` interface replaced by `ComparableValue` (moved to evalers only)
- **Go**: `types/virtual.go` deleted (had `Lesser` with `Less(Value) bool` and `LessEqual(Value) bool`). New `ComparableValue` interface in `internal/eval/` returns `(bool, error)`.
- **C# target**: If `Cedar.Types` or `Cedar.Core` has a `ILesser` or comparable interface, it should be removed/moved to `Cedar.Ast` internal evaluation. The evaluation layer (Cedar.Ast evalers) should define a comparable abstraction.
- **Impact**: Comparison failures return typed errors instead of panicking/returning false.

### 3. `LessThan`/`LessThanOrEqual` methods on `Long`, `Datetime`, `Duration`
- **Go**: Each type now has `LessThan(Value) (bool, error)` and `LessThanOrEqual(Value) (bool, error)` that return `ErrNotComparable` when the RHS is wrong type.
- **C# target**: 
  - `src/Cedar.Types/CedarLong.cs` (or equivalent) — add `LessThan(CedarValue)` / `LessThanOrEqual(CedarValue)` returning `(bool, CedarTypeError?)` or throwing on type mismatch
  - `src/Cedar.Types/CedarDatetime.cs` — same
  - `src/Cedar.Types/CedarDuration.cs` — same
- **Impact**: Evaluation of `<`, `<=`, `>`, `>=` operators must propagate type errors.

### 4. `FromStdTime` replaces `UnsafeDatetime`
- **Go**: `UnsafeDatetime(millis int64)` is removed; `FromStdTime(time.Time) Datetime` is added (wraps `time.UnixMilli`)
- **C# target**: If `Cedar.Types` has a `CedarDatetime.FromMilliseconds(long)` or unsafe constructor, ensure there's also a `FromDateTimeOffset(DateTimeOffset)` factory and the unsafe/raw constructor is not public API.
- **Impact**: Construction semantics are cleaner; raw milliseconds constructor may remain internal.

### 5. `FromStdDuration` constructor for Duration
- **Go**: `FromStdDuration(time.Duration) Duration` added
- **C# target**: `src/Cedar.Types/CedarDuration.cs` — add `FromTimeSpan(TimeSpan)` factory if not present.

### 6. Datetime error message renames
- "timezone indicator" → "time zone designator"
- "expected time offset" → "invalid time zone offset"  
- "unexpected trailer" → "unexpected trailer after time zone designator"
- **C# target**: Update error messages in datetime parser (likely in `src/Cedar.Types/CedarDatetime.cs` or parser file).

### 7. Magic constants extracted in evalers
- **Go**: Comparison evalers use named constants instead of inline literals
- **C# target**: Review evalers in `src/Cedar.Ast/` for inline magic values and extract to `const` fields.

## Concrete Port Tasks

### Task 1: Add ErrNotComparable equivalent
- File: `src/Cedar.Types/` — find where Cedar errors/exceptions are defined
- Add: `public static readonly CedarError NotComparable = ...` or appropriate .NET idiom (could be a specific exception type)

### Task 2: Move/remove comparable interface from Types layer
- Search for any `ILesser`, `IComparable`-like Cedar interface in `src/Cedar.Types/`
- If found, move it to `src/Cedar.Ast/Internal/Eval/` as an internal interface

### Task 3: Add `LessThan`/`LessThanOrEqual` with type-safety to value types
- `src/Cedar.Types/CedarLong.cs` — add methods returning `(bool success, bool result)` or `Result<bool>` pattern
- `src/Cedar.Types/CedarDatetime.cs` — same
- `src/Cedar.Types/CedarDuration.cs` — same
- Methods must return error/false when RHS is wrong Cedar type

### Task 4: `FromDateTimeOffset` factory on `CedarDatetime`
- `src/Cedar.Types/CedarDatetime.cs` — add `public static CedarDatetime FromDateTimeOffset(DateTimeOffset dt)`
- Ensure `UnsafeDatetime`-equivalent (raw millis) constructor is internal only if it exists

### Task 5: `FromTimeSpan` factory on `CedarDuration`
- `src/Cedar.Types/CedarDuration.cs` — add `public static CedarDuration FromTimeSpan(TimeSpan ts)`

### Task 6: Update datetime error messages
- Search for "timezone indicator", "time offset", "expected time offset", "unexpected trailer" in datetime parsing code
- Replace with "time zone designator", "time zone offset", "invalid time zone offset", "unexpected trailer after time zone designator"

### Task 7: Update evalers to use named constants
- `src/Cedar.Ast/Internal/Eval/` — extract any inline magic numeric/string literals to `const` fields

### Task 8: Tests
- `test/Cedar.Tests/` — add `LessThan`/`LessThanOrEqual` tests for Long, Datetime, Duration with type mismatch case returning ErrNotComparable equivalent
- Add `FromDateTimeOffset`/`FromTimeSpan` construction tests
- Add updated datetime parser error message tests

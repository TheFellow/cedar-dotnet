PORT

## Commit
**SHA:** a752ce1
**Date:** 2024-11-05T13:34:07-08:00
**Subject:** types: Replace UnsafeDecimal with three new, safe constructors

## Semantic Analysis

The upstream Go commit removes the single unsafe `UnsafeDecimal[T int|int64|float64](v T)` constructor (which blindly multiplied by `DecimalPrecision` with no bounds checking) and replaces it with three safe constructors:

1. **`NewDecimal(i int64, exponent int) (Decimal, error)`** — already exists in C# as `CedarDecimal.NewDecimal(long, int)` (throws on error rather than returning error, consistent with .NET idioms). ✅ Already ported.

2. **`NewDecimalFromInt[T constraints.Signed](i T) (Decimal, error)`** — convenience wrapper that calls `NewDecimal(int64(i), 0)`. **Missing in C#.**

3. **`NewDecimalFromFloat[T constraints.Float](f T) (Decimal, error)`** — multiplies float by `DecimalPrecision`, range-checks against `MaxInt64`/`MinInt64`, then calls `NewDecimal(int64(f), -4)`. **Missing in C#.**

4. **`DecimalMax` / `DecimalMin`** — sentinel `Decimal` values at `math.MaxInt64` / `math.MinInt64`. **Missing in C#.**

5. **`ToFloat()`** — a `double ToDouble()` equivalent already exists in C# (`ToDouble()`). ✅ Already ported.

6. The Go commit also fixed a variable-name bug in `MarshalCedar` and `String` (`v` → `d`). Our C# equivalent uses `this`/`Value` so not applicable.

The upstream also adds extensive tests for the three constructors covering normal cases, overflow, underflow, and float precision loss. Our test file has some `NewDecimal` tests but none for `NewDecimalFromInt` or `NewDecimalFromFloat`.

## Concrete Port Tasks

### Task 1 — Add `NewDecimalFromInt` to `CedarDecimal`
**Go source:** `types/decimal.go` lines ~40-42  
**C# target:** `src/Cedar.Types/CedarDecimal.cs`

Add after `NewDecimal`:
```csharp
public static CedarDecimal NewDecimalFromInt(long value)
{
    return NewDecimal(value, 0);
}
```

### Task 2 — Add `NewDecimalFromFloat` to `CedarDecimal`
**Go source:** `types/decimal.go` lines ~44-52  
**C# target:** `src/Cedar.Types/CedarDecimal.cs`

Add after `NewDecimalFromInt`. The Go logic multiplies by `DecimalPrecision`, checks against `MaxInt64`/`MinInt64`, then calls `NewDecimal(int64(f), -4)`. In C# use `double` and `checked` cast:
```csharp
public static CedarDecimal NewDecimalFromFloat(double value)
{
    double scaled = value * Precision;
    if (scaled > long.MaxValue)
        throw new ArgumentOutOfRangeException(nameof(value), "Decimal value would overflow.");
    if (scaled < long.MinValue)
        throw new ArgumentOutOfRangeException(nameof(value), "Decimal value would underflow.");
    return NewDecimal((long)scaled, -4);
}
```

### Task 3 — Add `DecimalMax` and `DecimalMin` constants
**Go source:** `types/decimal.go` lines ~20-21  
**C# target:** `src/Cedar.Types/CedarDecimal.cs`

Add as public static properties:
```csharp
public static CedarDecimal DecimalMax { get; } = new(long.MaxValue);
public static CedarDecimal DecimalMin { get; } = new(long.MinValue);
```

### Task 4 — Add tests for `NewDecimalFromInt`
**Go source:** `types/decimal_test.go` (`NewDecimalFromInt` section)  
**C# target:** `test/Cedar.Tests/Types/CedarDecimalTests.cs`

Add `[Theory]` test cases mirroring the upstream `NewDecimalFromInt` cases:
- `0` → `"0.0"`
- `1` → `"1.0"`
- `-1` → `"-1.0"`
- `922337203685477` → `"922337203685477.0"`
- `-922337203685477` → `"-922337203685477.0"`

And overflow case:
- `922337203685478` → throws `ArgumentOutOfRangeException`

### Task 5 — Add tests for `NewDecimalFromFloat`
**Go source:** `types/decimal_test.go` (`NewDecimalFromFloat`, `NewDecimalFromFloatPrecisionLoss`, `NewDecimalFromFloatOverflow` sections)  
**C# target:** `test/Cedar.Tests/Types/CedarDecimalTests.cs`

Normal cases:
- `0.0` → `"0.0"`, `1.0` → `"1.0"`, `-1.0` → `"-1.0"`
- `1.23451` → `"1.2345"` (truncates), `1.23456` → `"1.2345"`
- `922337203685477.5807` → `"922337203685477.5807"`
- `-922337203685477.5808` → `"-922337203685477.5808"`

Overflow cases (throw `ArgumentOutOfRangeException`):
- `922337203685477.6875`
- `-922337203685477.6876`
- `1000000000000000.0`
- `-1000000000000000.0`

### Task 6 — Add overflow/underflow tests for `NewDecimal` to match upstream matrix
**Go source:** `types/decimal_test.go` (`NewDecimalOverflow`, `NewDecimalUnderflow` sections)  
**C# target:** `test/Cedar.Tests/Types/CedarDecimalTests.cs`

The upstream enumerates specific (significand, exponent) pairs that overflow/underflow. We have one overflow test; add the full matrix:

Overflow cases: `(922337203685477581, -3)`, `(92233720368547759, -2)`, `(9223372036854776, -1)`, `(922337203685478, 0)`, `(92233720368548, 1)`, `(10, 14)`, `(1, 15)`  
Underflow cases: negations of above.

## Files to Change
- `src/Cedar.Types/CedarDecimal.cs` — Tasks 1, 2, 3
- `test/Cedar.Tests/Types/CedarDecimalTests.cs` — Tasks 4, 5, 6

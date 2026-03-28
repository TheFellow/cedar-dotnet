# Finalized Port Plan — a752ce1
**Subject:** types: Replace UnsafeDecimal with three new, safe constructors  
**Date:** 2024-11-05T13:34:07-08:00

---

## Go → C# Pattern Map

| Go pattern | C# equivalent used in this codebase |
|---|---|
| `func NewFoo(...) (T, error)` | `public static T NewFoo(...)` — throws `ArgumentOutOfRangeException` on bad input |
| `constraints.Signed` generic | Not needed — use `long` directly (C# has no need for generic int wrappers here) |
| `constraints.Float` generic | Not needed — use `double` directly |
| `var DecimalMax = Decimal{value: math.MaxInt64}` | `public static CedarDecimal DecimalMax { get; } = new(long.MaxValue);` |
| `math.MaxInt64` / `math.MinInt64` | `long.MaxValue` / `long.MinValue` |
| `testutil.ErrorIs(t, err, types.ErrDecimal)` | `Assert.Throws<ArgumentOutOfRangeException>(...)` |
| `testutil.Equals(t, d.String(), tt.want)` | `Assert.Equal(tt.want, d.MarshalCedar())` ... actually `CedarDecimal.Parse(want)` round-trip via `CedarAssert.Equal` |

---

## File 1: `src/Cedar.Types/CedarDecimal.cs`

**Current state (149 lines):**
- Line 6: `public sealed record CedarDecimal(long Value) : CedarValue`
- Lines 8–11: `private const long Precision`, `MaxIntegerPart`, `MaxFractionalPart`, `MinFractionalPart`
- Lines 13–44: `public static CedarDecimal NewDecimal(long value, int exponent)` ← insert after closing `}`
- Lines 46–79: `public static CedarDecimal Parse(string value)`
- Lines 81–84: `public double ToDouble()`

### Change 1a — Insert `DecimalMax`/`DecimalMin` after the private constants (after line 11, before line 13)

**Insert point:** after `private const short MinFractionalPart = -5_808;` (line 11), before `public static CedarDecimal NewDecimal`

```csharp
    public static CedarDecimal DecimalMax { get; } = new(long.MaxValue);
    public static CedarDecimal DecimalMin { get; } = new(long.MinValue);
```

**Acceptance criteria:** `CedarDecimal.DecimalMax.Value == long.MaxValue` and `CedarDecimal.DecimalMin.Value == long.MinValue`.

### Change 1b — Insert `NewDecimalFromInt` after `NewDecimal` closing `}` (after line 44)

```csharp
    public static CedarDecimal NewDecimalFromInt(long value)
    {
        return NewDecimal(value, 0);
    }
```

**Acceptance criteria:** `CedarDecimal.NewDecimalFromInt(42)` equals `CedarDecimal.Parse("42.0")`. `NewDecimalFromInt(922337203685478)` throws `ArgumentOutOfRangeException`.

### Change 1c — Insert `NewDecimalFromFloat` after `NewDecimalFromInt` closing `}`

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

**Logic notes:**
- `Precision` = 10_000 (private const already present at line 8)
- Multiply first, then range-check before casting to `long` — this matches the Go: `f = f * DecimalPrecision` then check vs `math.MaxInt64`/`math.MinInt64`
- The cast `(long)scaled` truncates toward zero, same as Go's `int64(f)` conversion
- Then delegates to existing `NewDecimal(long, int)` with exponent `-4`, which performs the final bounds check (covers the "surprising overflow/underflow" edge cases from the Go tests)

**Acceptance criteria:**
- `NewDecimalFromFloat(1.0)` equals `Parse("1.0")`
- `NewDecimalFromFloat(1.23451)` equals `Parse("1.2345")` (truncation)
- `NewDecimalFromFloat(1000000000000000.0)` throws `ArgumentOutOfRangeException`
- `NewDecimalFromFloat(-1000000000000000.0)` throws `ArgumentOutOfRangeException`

---

## File 2: `test/Cedar.Tests/Types/CedarDecimalTests.cs`

**Current state (128 lines):**
- Usings at lines 1–4: `System`, `Cedar.Tests.TestSupport`, `Cedar.Types`, `Xunit`
- Last test ends at line 127, closing `}` at line 128
- No existing tests for `NewDecimalFromInt` or `NewDecimalFromFloat`
- Existing `NewDecimalRejectsOverflow` at line 88 only covers `(922337203685478, 0)`

**Insert all new tests before the final closing `}` at line 128.**

### Change 2a — `NewDecimalFromInt` happy-path theory (insert before line 128)

```csharp
    [Theory]
    [InlineData(0L, "0.0")]
    [InlineData(1L, "1.0")]
    [InlineData(-1L, "-1.0")]
    [InlineData(922337203685477L, "922337203685477.0")]
    [InlineData(-922337203685477L, "-922337203685477.0")]
    public void NewDecimalFromIntProducesExpectedString(long input, string expected)
    {
        CedarAssert.Equal(CedarDecimal.Parse(expected), CedarDecimal.NewDecimalFromInt(input));
    }

    [Fact]
    public void NewDecimalFromIntRejectsOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CedarDecimal.NewDecimalFromInt(922337203685478L));
    }
```

### Change 2b — `NewDecimalFromFloat` happy-path theory (insert after 2a)

```csharp
    [Theory]
    [InlineData(0.0, "0.0")]
    [InlineData(1.0, "1.0")]
    [InlineData(-1.0, "-1.0")]
    [InlineData(1.23451, "1.2345")]
    [InlineData(1.23456, "1.2345")]
    [InlineData(922337203685477.5807, "922337203685477.5807")]
    [InlineData(-922337203685477.5808, "-922337203685477.5808")]
    public void NewDecimalFromFloatProducesExpectedString(double input, string expected)
    {
        CedarAssert.Equal(CedarDecimal.Parse(expected), CedarDecimal.NewDecimalFromFloat(input));
    }
```

### Change 2c — `NewDecimalFromFloat` overflow theory (insert after 2b)

```csharp
    [Theory]
    [InlineData(922337203685477.6875)]
    [InlineData(-922337203685477.6876)]
    [InlineData(1000000000000000.0)]
    [InlineData(-1000000000000000.0)]
    public void NewDecimalFromFloatRejectsOutOfRange(double input)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CedarDecimal.NewDecimalFromFloat(input));
    }
```

### Change 2d — Full `NewDecimal` overflow matrix (insert after 2c)

Replaces/expands on the single existing `NewDecimalRejectsOverflow` fact — keep the existing fact, add this theory:

```csharp
    [Theory]
    [InlineData(922337203685477581L, -3)]
    [InlineData(92233720368547759L, -2)]
    [InlineData(9223372036854776L, -1)]
    [InlineData(922337203685478L, 0)]
    [InlineData(92233720368548L, 1)]
    [InlineData(10L, 14)]
    [InlineData(1L, 15)]
    public void NewDecimalRejectsOverflowMatrix(long significand, int exponent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CedarDecimal.NewDecimal(significand, exponent));
    }

    [Theory]
    [InlineData(-922337203685477581L, -3)]
    [InlineData(-92233720368547759L, -2)]
    [InlineData(-9223372036854776L, -1)]
    [InlineData(-922337203685478L, 0)]
    [InlineData(-92233720368548L, 1)]
    [InlineData(-10L, 14)]
    [InlineData(-1L, 15)]
    public void NewDecimalRejectsUnderflowMatrix(long significand, int exponent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CedarDecimal.NewDecimal(significand, exponent));
    }
```

---

## Build & Test Validation

After changes, run:
```
dotnet build cedar-dotnet.sln
dotnet test test/Cedar.Tests/Cedar.Tests.csproj --filter "FullyQualifiedName~CedarDecimalTests"
```

Expected: all new tests pass, no new warnings (warnings-as-errors is enforced).

---

## Ledger Update (after implementation)

```
python3 semport/ledger.py update a752ce1 implemented
python3 semport/ledger.py sort
git add semport/ledger.tsv
git commit -m "semport: implement a752ce1 - add NewDecimalFromInt, NewDecimalFromFloat, DecimalMax/Min"
rm -f .ai/semport_new_commits.md .ai/semport_plan.md .ai/semport_plan_finalized.md
```

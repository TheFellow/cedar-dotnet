# Finalized Port Plan: a4e576e — "Address PR Feedback"

## Architecture Reality Check

After scanning the codebase, the C# implementation already has:
- `CedarDatetime.FromDateTimeOffset(DateTimeOffset)` — EXISTS at `src/Cedar.Types/CedarDatetime.cs:92` ✅ (Task 4 is already done)
- `ComparableValues` static class — EXISTS at `src/Cedar.Core/Internal/Eval/Evaluators/ComparisonEvaluators.cs:54` (internal to eval layer already) ✅
- `EvalException` — EXISTS at `src/Cedar.Core/Internal/Eval/EvalException.cs` for eval-layer errors ✅
- Datetime parser already uses "invalid time zone designator" at line 81 ✅ (partial - but needs full audit)
- `CedarDuration.ToTimeSpan()` — EXISTS at `src/Cedar.Types/CedarDuration.cs:106` but `FromTimeSpan()` does NOT exist

The key semantic gaps to close:

---

## Task 1: Add `ErrNotComparable` equivalent to `EvalErrors`

**Go source**: `types/value.go` — `var ErrNotComparable = fmt.Errorf("incompatible types in comparison")`

**C# target**: `src/Cedar.Core/Internal/Eval/EvalErrors.cs`

**What to do**: Add a const string `NotComparable` alongside the existing consts. The `CompareFailure` method in `ComparisonEvaluators.cs` already throws `EvalException` with ad-hoc messages — standardize it to use the new constant.

**Current code** (`ComparisonEvaluators.cs:73-76`):
```csharp
throw new EvalException($"cannot compare {EvalErrors.TypeName(left)} with {EvalErrors.TypeName(right)}");
// ...
throw new EvalException($"expected comparable value, got {EvalErrors.TypeName(!IsComparable(left) ? left : right)}");
```

**Change to** (`EvalErrors.cs` — add after line 12):
```csharp
public const string IncompatibleComparison = "incompatible types in comparison";
```

**Change** (`ComparisonEvaluators.cs:73-76`) — both `throw` branches collapse to:
```csharp
throw new EvalException(EvalErrors.IncompatibleComparison);
```

**Acceptance criteria**:
- `EvalErrors.IncompatibleComparison` constant exists
- Both throw sites in `CompareFailure` use it
- Existing comparison evaluator tests still pass

---

## Task 2: Add `FromTimeSpan` factory to `CedarDuration`

**Go source**: `types/duration.go` — `func FromStdDuration(d time.Duration) Duration`

**C# target**: `src/Cedar.Types/CedarDuration.cs` — after `ToTimeSpan()` at line 109

**What to do**: Add static factory:
```csharp
public static CedarDuration FromTimeSpan(TimeSpan value)
{
    return new CedarDuration((long)value.TotalMilliseconds);
}
```

**Note**: `ToTimeSpan()` already exists (line 106-109). This is the inverse.

**Acceptance criteria**:
- `CedarDuration.FromTimeSpan(TimeSpan.FromMilliseconds(42))` returns `new CedarDuration(42)`
- Round-trip: `CedarDuration.FromTimeSpan(d.ToTimeSpan()) == d` for any `CedarDuration d`

---

## Task 3: Update datetime error messages for incompatible separators

**Go source**: `types/datetime.go` and `types/datetime_test.go` — error messages updated to consistently use "time zone designator" and "time zone offset"

**C# target**: `src/Cedar.Types/CedarDatetime.cs`

**Audit results**:
- Line 81: `"invalid time zone designator"` — ALREADY CORRECT ✅
- Line 86: `"unexpected additional characters"` — needs check against Go's `"unexpected trailer after time zone designator"`
- Line 289: `$"unexpected character {value[cursor]}"` — Go uses `$"unexpected character '{value[cursor]}'"` (note single quotes around char)
- `ReadOffset` at line 329-337: errors from `ParseDigits` for offset components use component names "offset hours"/"offset minutes" — Go's new messages say `"invalid time zone offset"` for all offset parse failures

**Changes**:

1. `src/Cedar.Types/CedarDatetime.cs:86` — change:
   ```csharp
   // FROM:
   throw new FormatException("unexpected additional characters");
   // TO:
   throw new FormatException("unexpected trailer after time zone designator");
   ```

2. `src/Cedar.Types/CedarDatetime.cs:289` (`ExpectCharacter`) — change:
   ```csharp
   // FROM:
   throw new FormatException($"unexpected character {value[cursor]}");
   // TO:
   throw new FormatException($"unexpected character '{value[cursor]}'");
   ```

3. `src/Cedar.Types/CedarDatetime.cs:329-337` (`ReadOffset`) — change `ParseDigits` calls to use a unified error by wrapping or using a different component label. The Go implementation uses `"invalid time zone offset"` for all offset parse failures. Refactor `ReadOffset` to catch and rethrow:
   ```csharp
   private static long ReadOffset(string value, ref int cursor, int sign)
   {
       cursor++;
       try
       {
           int hours = ParseDigits(value, ref cursor, 2, 23, "hour");
           int minutes = ParseDigits(value, ref cursor, 2, 59, "minute");
           long magnitude = (hours * CedarConsts.MillisPerHour) + (minutes * CedarConsts.MillisPerMinute);
           return sign * magnitude;
       }
       catch (FormatException)
       {
           throw new FormatException("invalid time zone offset");
       }
   }
   ```

**Acceptance criteria**:
- `CedarDatetime.Parse("1995-01-01T00:00:00Zgarbage")` throws `FormatException` with message containing `"unexpected trailer after time zone designator"`
- `CedarDatetime.Parse("1995-01-01T00:00:00.000+")` throws `FormatException` with message `"invalid time zone offset"`
- `CedarDatetime.Parse("1995-01-01T00+00:00Z")` throws `FormatException` with message containing `"unexpected character '+'"`
- Existing passing datetime parse tests continue to pass

---

## Task 4: Tests

**Test files to update/add to**:

### `test/Cedar.Tests/Types/CedarDurationTests.cs`
Add at end of class:
```csharp
[Fact]
public void FromTimeSpanRoundTrips()
{
    CedarDuration original = new(42);
    CedarDuration roundTripped = CedarDuration.FromTimeSpan(original.ToTimeSpan());
    Assert.Equal(original, roundTripped);
}

[Fact]
public void FromTimeSpanCreatesFromMilliseconds()
{
    CedarDuration result = CedarDuration.FromTimeSpan(TimeSpan.FromMilliseconds(1000));
    Assert.Equal(1000, result.Value);
}
```

### `test/Cedar.Tests/Types/CedarDatetimeTests.cs`
Add error-message-specific tests:
```csharp
[Theory]
[InlineData("1995-01-01T00:00:00Zgarbage", "unexpected trailer after time zone designator")]
[InlineData("1995-01-01T00:00:00.000+", "invalid time zone offset")]
[InlineData("1995-01-01T00:00:00.000-", "invalid time zone offset")]
[InlineData("1995-01-01T00:00:00.000-00", "invalid time zone offset")]
public void ParseRejectsWithExpectedMessage(string input, string expectedMessageFragment)
{
    var ex = Assert.Throws<FormatException>(() => CedarDatetime.Parse(input));
    Assert.Contains(expectedMessageFragment, ex.Message);
}

[Theory]
[InlineData("1995-01-01T00+00:00Z", "'+'")]
[InlineData("1995-01-01T00:00+00Z", "'+'")]
public void ParseRejectsUnexpectedCharacterWithQuotes(string input, string expectedChar)
{
    var ex = Assert.Throws<FormatException>(() => CedarDatetime.Parse(input));
    Assert.Contains(expectedChar, ex.Message);
}
```

### `test/Cedar.Tests/Eval/ComparisonEvaluatorTests.cs` (new file or existing)
Check if `test/Cedar.Tests/Eval/` exists — if not, add to nearest evaluator test file:
```csharp
[Fact]
public void CompareBoolWithLongThrowsEvalException()
{
    // policy: true < 1 should throw
    // Use Compiler/EvalEnv to verify EvalException thrown with IncompatibleComparison message
}
```
(Locate existing eval tests with `find test -name "*Eval*" -o -name "*Comparison*" | head -5`)

---

## File:Line Summary

| File | Action | Lines |
|------|--------|-------|
| `src/Cedar.Core/Internal/Eval/EvalErrors.cs` | Add `IncompatibleComparison` const | After line 12 |
| `src/Cedar.Core/Internal/Eval/Evaluators/ComparisonEvaluators.cs` | Use `EvalErrors.IncompatibleComparison` in both throw sites | Lines 73, 76 |
| `src/Cedar.Types/CedarDuration.cs` | Add `FromTimeSpan(TimeSpan)` static factory | After line 109 |
| `src/Cedar.Types/CedarDatetime.cs` | Fix "unexpected additional characters" message | Line 86 |
| `src/Cedar.Types/CedarDatetime.cs` | Add single quotes around char in `ExpectCharacter` error | Line 289 |
| `src/Cedar.Types/CedarDatetime.cs` | Refactor `ReadOffset` to throw "invalid time zone offset" | Lines 329-337 |
| `test/Cedar.Tests/Types/CedarDurationTests.cs` | Add `FromTimeSpan` tests | End of class |
| `test/Cedar.Tests/Types/CedarDatetimeTests.cs` | Add error-message assertion tests | End of class |

## Already Ported (no action needed)
- `CedarDatetime.FromDateTimeOffset()` — EXISTS at `CedarDatetime.cs:92` ✅
- `ComparableValues` is already internal to eval layer ✅
- `CedarDuration.ToTimeSpan()` — EXISTS (we only need the inverse `FromTimeSpan`) ✅
- "invalid time zone designator" message — EXISTS at `CedarDatetime.cs:81` ✅

## Go Pattern → C# Idiom Map
| Go | C# |
|----|----|
| `var ErrNotComparable = fmt.Errorf(...)` | `public const string IncompatibleComparison = "..."` in `EvalErrors` |
| `return false, ErrNotComparable` from type method | `throw new EvalException(EvalErrors.IncompatibleComparison)` in eval layer |
| `FromStdTime(time.Time)` | `FromDateTimeOffset(DateTimeOffset)` (already exists) |
| `FromStdDuration(time.Duration)` | `FromTimeSpan(TimeSpan)` (needs adding) |
| `ComparableValue` interface in `internal/eval` | `ComparableValues` static class already in `Cedar.Core.Internal.Eval.Evaluators` |

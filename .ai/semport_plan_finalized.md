# Finalized Port Plan: 5a500db — Fix Pattern MarshalCedar to use Cedar-compatible escaping

## Verdict: PARTIAL — C# logic is already correct; only test coverage is missing.

---

## Key Finding

The C# implementation **already has the correct fix** in place:

- `src/Cedar.Types/CedarPattern.cs:140` — `MarshalCedar()` uses `CedarString.EscapeCharAll(component.Literal).Replace("*", "\\*", ...)`
- `src/Cedar.Types/CedarString.cs:36–44` — `EscapeCharAll` iterates by `Rune` with `escapeGraphemeExtend: true`
- `src/Cedar.Types/CedarString.cs:50–64` — `EscapeRune` switch: named escapes for `\0 \t \r \n \\ \" \'`; then `UnicodeEscape` for non-printable (`IsPrintable` returns `false` for all codepoints < 32, which covers bell=U+0007, backspace=U+0008, vtab=U+000B, form-feed=U+000C)
- `src/Cedar.Core/Internal/Rust/RustPrintable.cs:271–285` — `IsPrintable` returns `false` for `x < 32`, so all ASCII control chars below space emit `\u{XX}`

**The Go bug (using `strconv.Quote` → host-language escapes) does not exist in C#.** The C# code was already using the Rust-parity escaper.

**What IS missing:** The test at `test/Cedar.Tests/Types/CedarStringTests.cs:100–108` (`PatternMarshalCedarEscapesEachCharacterUsingRustParity`) does not cover the specific cases from the Go fix (bell, backspace, form-feed, vtab). These should be added to match Go's new test suite.

---

## Files to Change

### 1. `test/Cedar.Tests/Types/CedarStringTests.cs` — Add missing `[InlineData]` cases

**Location:** Lines 100–108 (the `PatternMarshalCedarEscapesEachCharacterUsingRustParity` theory)

**Current state (lines 100–108):**
```csharp
[Theory]
[InlineData("a\u0300", "\"a\\u{300}\"")]
[InlineData("a\uFF9E", "\"a\\u{ff9e}\"")]
[InlineData("a\u0903", "\"a\u0903\"")]
[InlineData("hello", "\"hello\"")]
public void PatternMarshalCedarEscapesEachCharacterUsingRustParity(string literal, string expected)
{
    CedarAssert.CedarText(new CedarPattern(literal), expected);
}
```

**Add these `[InlineData]` lines** (mirror of Go's new test cases):
```csharp
[InlineData("\u0007", "\"\\u{7}\"")]        // bell — Go: bell_char
[InlineData("\u0008", "\"\\u{8}\"")]        // backspace — Go: backspace
[InlineData("\u000C", "\"\\u{c}\"")]        // form-feed — Go: formfeed
[InlineData("\u000B", "\"\\u{b}\"")]        // vertical tab — Go: vtab
[InlineData("*foo", "\"\\*foo\"")]          // literal star escaping — Go: escaped_wildcard
```

Also add a test for the wildcard-plus-literal case from Go (`NewPattern(String("*foo"), Wildcard{})`):
```csharp
// Go: escaped_wildcard — pattern with literal "*foo" followed by wildcard
// CedarPattern constructor takes params object[]; Wildcard.Instance for wildcards
[InlineData("*foo", "\"\\*foo\"")]          // single literal component with leading star
```

And a dedicated `[Fact]` or additional `[Theory]` case for wildcard + literal star:
```csharp
// Equivalent to Go: NewPattern(String("*foo"), Wildcard{}).MarshalCedar() == `"\*foo*"`
// In C#: new CedarPattern("*foo", Wildcard.Instance).MarshalCedar() == "\"\\*foo*\""
```

**No production code changes are needed.**

---

## Acceptance Criteria

1. `dotnet test cedar-dotnet.sln` passes with 0 failures.
2. The following new `[InlineData]` cases exist and pass in `PatternMarshalCedarEscapesEachCharacterUsingRustParity`:
   - `"\u0007"` → `"\"\\u{7}\""` (bell → `\u{7}`)
   - `"\u0008"` → `"\"\\u{8}\""` (backspace → `\u{8}`)
   - `"\u000C"` → `"\"\\u{c}\""` (form-feed → `\u{c}`)
   - `"\u000B"` → `"\"\\u{b}\""` (vtab → `\u{b}`)
   - `"*foo"` → `"\"\\*foo\""` (literal star escaped in pattern literal)
3. A test asserting `new CedarPattern("*foo", Wildcard.Instance).MarshalCedar() == "\"\\*foo*\""` exists and passes.

---

## C# Idiom Notes

| Go | C# equivalent |
|----|--------------|
| `NewPattern(String("*foo"), Wildcard{})` | `new CedarPattern("*foo", Wildcard.Instance)` |
| `t.Run("name", ...)` table test | `[Theory] [InlineData(...)]` on xUnit `[Fact]` |
| `testutil.Equals(t, actual, expected)` | `CedarAssert.CedarText(value, expected)` (defined in `test/Cedar.Tests/TestSupport/CedarAssert.cs:26`) |
| `pattern.MarshalCedar()` → `[]byte` | `pattern.MarshalCedar()` → `string` |

---

## Implementation Steps (in order)

1. Open `test/Cedar.Tests/Types/CedarStringTests.cs`
2. Find the `[Theory]` block for `PatternMarshalCedarEscapesEachCharacterUsingRustParity` (currently at ~line 100)
3. Add the five new `[InlineData]` entries for bell, backspace, form-feed, vtab, and literal-star
4. Add a new `[Fact]` named `PatternMarshalCedarEscapesLiteralStarFollowedByWildcard` that asserts `new CedarPattern("*foo", Wildcard.Instance).MarshalCedar() == "\"\\*foo*\""`
5. Run `dotnet test cedar-dotnet.sln -filter "PatternMarshalCedar"` to confirm all pass
6. Run `dotnet test cedar-dotnet.sln` for full suite green

---

## Ledger Update (after implementation)

```
python3 semport/ledger.py update 5a500db implemented
python3 semport/ledger.py sort
git add semport/ledger.tsv test/Cedar.Tests/Types/CedarStringTests.cs
git commit -m "semport: implement 5a500db - add missing pattern escape tests (bell, backspace, vtab, formfeed, literal-star)"
rm -f .ai/semport_new_commits.md .ai/semport_plan.md .ai/semport_plan_finalized.md
```

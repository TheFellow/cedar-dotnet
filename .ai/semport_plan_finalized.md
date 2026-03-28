# Semport Plan Finalized — commit 595915b
## "types: tweak shape of pattern"

---

## Findings vs. Plan

### Task A — Encapsulate `WildcardPatternComponent` → **PARTIALLY DONE, needs one fix**

**Current C# state:**
- `src/Cedar.Types/Wildcard.cs:3` — `public sealed class Wildcard` with a private constructor and `public static Wildcard Instance { get; }` — already opaque/singleton (callers can't `new` it).
- `src/Cedar.Types/CedarPattern.cs:380` — internal `private readonly record struct PatternComponent(bool Wildcard, string Literal)` — already private.
- The `Wildcard` class IS public but IS already encapsulated (singleton, private ctor). This matches the Go intent.

**Gap found:** `CedarPattern.Match` has TWO overloads:
- `src/Cedar.Types/CedarPattern.cs:91` — `public bool Match(CedarString value)` ✓ already takes Cedar type
- `src/Cedar.Types/CedarPattern.cs:96` — `public bool Match(string value)` — raw `string` overload still public

The Go commit removes the raw-string overload (Go `string` → Go `String` which is Cedar's typed string). The C# equivalent is: **remove the `public bool Match(string value)` overload** or make it `internal`/`private`.

**Callers of `Match(string)`:**
- `src/Cedar.Core/Internal/Eval/Evaluators/PatternEvaluators.cs:9` — calls `pattern.Match(new CedarString(...))` ✓ already uses `CedarString` overload
- Test files: grep shows tests call `Match("hello")`, `Match("prefix")`, `Match("axbyc")` with raw strings.

---

## Concrete Port Tasks

### Task 1 — Remove/internalize `public bool Match(string value)` overload
**File:** `src/Cedar.Types/CedarPattern.cs`
**Lines:** 96–100 (the `public bool Match(string value)` overload)

**Action:** Change the `string` overload from `public` to `private` (keep it as a private helper called by the `CedarString` overload). This matches Go's change: callers must pass a Cedar-typed string, not a raw string.

**Before (line 91–100, approximate):**
```csharp
public bool Match(CedarString value)
{
    return Match(value.Value);
}

public bool Match(string value)
{
    // ... implementation ...
}
```

**After:**
```csharp
public bool Match(CedarString value)
{
    return MatchCore(value.Value);
}

private bool MatchCore(string value)
{
    // ... same implementation ...
}
```
*(Or simply make `Match(string)` private — rename to avoid confusion.)*

**Acceptance criteria:**
- `CedarPattern.Match(string)` is no longer public API.
- `CedarPattern.Match(CedarString)` remains the only public `Match` entry point.
- Build succeeds with no warnings (warnings-as-errors).

---

### Task 2 — Update tests that call `Match(string)` directly
**Files:**
- `test/Cedar.Tests/Types/CedarPatternTests.cs:37` — `Match("hello")`
- `test/Cedar.Tests/Types/CedarPatternTests.cs:43` — `Match("prefix")`
- `test/Cedar.Tests/Types/CedarPatternTests.cs:49` — `Match("axbyc")`

**Action:** Wrap raw string literals in `new CedarString(...)` at each call site.

**Example change:**
```csharp
// Before:
Assert.True(new CedarPattern("he", Wildcard.Instance, "o").Match("hello"));
// After:
Assert.True(new CedarPattern("he", Wildcard.Instance, "o").Match(new CedarString("hello")));
```

**Acceptance criteria:**
- All three `Match` test call sites use `new CedarString(...)`.
- `dotnet test` passes with no failures.

---

### Task 3 — Verify `Wildcard` encapsulation (NO CHANGE NEEDED)
**File:** `src/Cedar.Types/Wildcard.cs`

The C# `Wildcard` class is already correctly encapsulated:
- Private constructor (callers can't `new Wildcard()`)
- `public static Wildcard Instance` singleton (the factory equivalent of Go's `Wildcard()` function)

This already matches the intent of the Go change. No code change required here.

**Acceptance criteria:** Confirm `Wildcard` has `private Wildcard()` ctor — already verified at line 7.

---

## File Reference Summary

| File | Line(s) | Action |
|---|---|---|
| `src/Cedar.Types/CedarPattern.cs` | 96–~105 | Change `public bool Match(string value)` → `private bool MatchCore(string value)`, update `Match(CedarString)` to call `MatchCore` |
| `test/Cedar.Tests/Types/CedarPatternTests.cs` | 37, 43, 49 | Wrap string literals in `new CedarString(...)` at `Match(...)` call sites |

## Go → C# Pattern Mapping

| Go | C# |
|---|---|
| `func (p Pattern) Match(arg String)` | `public bool Match(CedarString value)` |
| Go unexported `wildcardComponent` | C# `private sealed class Wildcard` with private ctor (already done) |
| Go `Wildcard()` factory func | C# `Wildcard.Instance` singleton (already done) |
| Remove `string` overload | Make `Match(string)` private/internal |

## Validation Steps
1. `dotnet build cedar-dotnet.sln` — must succeed (0 warnings, 0 errors)
2. `dotnet test cedar-dotnet.sln` — all tests must pass
3. Confirm `grep -n "public.*Match.*string" src/Cedar.Types/CedarPattern.cs` returns no results

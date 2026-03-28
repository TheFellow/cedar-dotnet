PORT

## Commit: 595915b — "types: tweak shape of pattern"
**Date:** 2024-08-23T13:41:26-06:00

## Semantic Analysis

This commit makes three related semantic changes to the Cedar pattern type:

### 1. `WildcardPatternComponent` → unexported `wildcardComponent`
- **Go before:** `WildcardPatternComponent` was an exported struct; `Wildcard` was a public var of that type.
- **Go after:** The type is renamed to unexported `wildcardComponent`; `Wildcard` becomes a function `Wildcard() PatternComponent` returning the private type.
- **Semantic impact:** The wildcard sentinel is now opaque — callers can't construct or type-assert `WildcardPatternComponent` directly; they must use the `Wildcard()` factory. This is an encapsulation improvement that prevents misuse.

### 2. `errJSONInvalidPatternComponent` moved to `pattern.go`
- Pure code-organization change: the error sentinel was moved from `json.go` to `pattern.go` where it is used.
- **Semantic impact:** None — same error value, same behavior.

### 3. `Pattern.Match(arg string)` → `Pattern.Match(arg String)`
- The `Match` method now takes a Cedar `String` value rather than a raw Go `string`.
- **Semantic impact:** Callers must pass a typed `String`, not a raw string. This ties the match signature to Cedar's value system.

## C# Port Tasks

### Task A — Encapsulate `WildcardPatternComponent`
**Go source:** `inspiration/cedar-go/types/pattern.go` lines 32–35 (before) → 32–34 (after)

Find how `WildcardPatternComponent` is currently represented in the C# codebase.

- Search `src/Cedar.Types` for `WildcardPatternComponent`, `PatternComponent`, and `Pattern`.
- If `WildcardPatternComponent` is a public `sealed record` or similar, make the concrete type internal and expose only a static factory method (e.g., `PatternComponent.Wildcard()` or `WildcardPatternComponent.Instance` behind an interface).
- Ensure the `PatternComponent` interface/discriminated-union is what callers reference, not the concrete wildcard type.

**Target files:** likely `src/Cedar.Types/Pattern.cs` (or similar).

### Task B — `Pattern.Match` signature
**Go source:** `inspiration/cedar-go/types/pattern.go` line 90 (after)

If `Pattern.Match` currently accepts `string`, change it to accept `CedarString` (or whatever the C# Cedar string value type is) to match the Go change. If it already uses the Cedar string type, this is a no-op.

**Target files:** likely `src/Cedar.Types/Pattern.cs`.

### Task C — Update tests
**Go source:** `inspiration/cedar-go/types/patttern_test.go`

Update any C# tests that construct patterns using the old `WildcardPatternComponent` directly to use the new factory method, and update `Match` call sites to pass a `CedarString`.

**Target files:** likely `test/Cedar.Tests/PatternTests.cs` or similar.

## Notes
- The error sentinel move (task for `errJSONInvalidPatternComponent`) has no C# equivalent to port.
- Verify whether C# already uses an opaque/internal wildcard type; if so Tasks A may reduce to acknowledged.

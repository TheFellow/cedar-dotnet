PORT

## Commit Summary
**SHA:** 60b4b94  
**Message:** types: Address PR feedback  
**Date:** 2026-01-06T09:25:52-08:00

## Semantic Analysis

This commit fixes a **parsing bug** in `EntityUID.UnmarshalCedar` (Go: `types/entity_uid.go`).

### The Bug
The original code used `strings.LastIndex(s, "::\"")`  to find the boundary between the entity type and the quoted ID. This is wrong: if the entity **ID** itself contains `::` (e.g. `X::Y::"asdf::"`) then `LastIndex` would find the `::` inside the ID, not the separator between type and ID. The fix switches to `strings.Index` (first occurrence), so the type portion is correctly extracted as everything before the first `::\"`.

Additionally, the fix adds proper unquoting of the ID string using `rust.Unquote` (Cedar uses Rust-style string escape sequences), replacing the naive raw substring slice that ignored escape characters.

### Impact
Any C# code that parses `EntityUID` from Cedar text (e.g. `Type::"id"` syntax) and uses a "last index" or equivalent strategy to split type vs. ID is affected. Entity IDs containing `::` would be mis-parsed.

## Port Tasks

### 1. Find the C# EntityUID Cedar-text parser
- Look for the C# equivalent of `UnmarshalCedar` for `EntityUID`.
- Likely location: `src/Cedar.Types/` or `src/Cedar.Ast/` — search for parsing of `::"`  or `EntityUID` from string.
- Key files to check:
  - `src/Cedar.Types/EntityUid.cs` (or similar)
  - Any Cedar-text / policy parser that constructs `EntityUID` from text

### 2. Fix the split strategy
- Change any `LastIndexOf("::\"")`  or `LastIndexOf("::")` call used to split type vs. ID to `IndexOf` (first occurrence).
- After the first `::\"`, extract the quoted substring, validate it starts and ends with `"`, then unquote the inner content.

### 3. Ensure proper string unquoting
- Cedar string literals use Rust-style escapes (`\n`, `\t`, `\\`, `\"`, `\u{XXXX}`, etc.).
- The C# parser should call whatever unquoting utility already exists (search for `Unquote`, `UnescapeString`, or similar in `src/Cedar.Ast/Internal/`).
- If no utility exists, implement one or reuse the existing Cedar string literal parser.

### 4. Add / extend tests
- Target: `test/Cedar.Tests/` — find existing `EntityUid` or `EntityUID` parse tests.
- Add cases mirroring the new Go tests:
  - `X::Y::"asdf::"` → type=`X::Y`, id=`asdf::`
  - `Search::Algorithm::"A*"` → type=`Search::Algorithm`, id=`A*`
  - `Super::"*"` → type=`Super`, id=`*`
  - `namespace::type::""` → type=`namespace::type`, id=`` (empty)
- Ensure existing invalid-input cases still fail (`::"id"`, missing closing quote, etc.)

### Go source reference
- **Bug fix:** `inspiration/cedar-go/types/entity_uid.go`, `UnmarshalCedar` function, lines ~55-80
- **New tests:** `inspiration/cedar-go/types/entity_uid_test.go`, `UnmarshalCedar with ::\" in ID` and expanded `MarshalBinary round trip`

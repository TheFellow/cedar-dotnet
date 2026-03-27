PORT

## Commit: 5a500db — Fix Pattern MarshalCedar to use Cedar-compatible escaping

### Summary
`Pattern.MarshalCedar()` in cedar-go was using Go's `strconv.Quote` to escape literal segments of a wildcard pattern. `strconv.Quote` produces Go-style escape sequences (e.g. `\x00`, `\v`, `\a`) which are **not valid Cedar syntax**. The fix switches to `rust.EscapeCharAll` which emits Cedar/Rust-compatible escapes (`\0`, `\u{7}`, `\u{b}`, `\u{c}`, etc.) — including always escaping Unicode combining/grapheme-extend characters rather than passing them through raw.

The additional constraint (vs a simple string escaper) is that each **character** of the pattern literal is escaped independently (via Rust's `char::escape_debug`), so combining marks at continuation positions are always escaped rather than embedded as raw UTF-8.

### Semantic Impact
This is a **correctness bug fix** in Cedar policy serialization. Any code path that converts a `Pattern` (wildcard expression) to its Cedar text representation must produce escape sequences valid in Cedar, not in the host language. Concretely:
- `\a` (bell, U+0007) → must be `\u{7}`, not `\x07` or `\a`
- `\b` (backspace, U+0008) → must be `\u{8}`
- `\f` (form-feed, U+000C) → must be `\u{c}`
- `\v` (vertical tab, U+000B) → must be `\u{b}`
- Unicode combining marks (e.g. U+0300) → must be `\u{300}` not raw UTF-8
- Literal `*` inside a literal segment → must be `\*`

### Port Tasks

#### 1. Find the C# Pattern type and its Cedar serialization
**Go source:** `types/pattern.go`, `MarshalCedar()` method (line ~59–70 after patch)  
**C# target:** Locate the `Pattern` type — likely in `src/Cedar.Types/` or `src/Cedar.Ast/`. Look for any method that renders a pattern to a Cedar policy string (e.g. `ToCedarString`, `MarshalCedar`, `ToString`, a visitor, or a policy printer).

Search targets:
- `src/Cedar.Types/` — for a `Pattern`, `Wildcard`, or `PatternComponent` type
- `src/Cedar.Ast/` — for policy/expression printing/serialization
- Any file containing `like` expression handling (Cedar `like` operator uses patterns)

#### 2. Audit the C# escape logic for pattern literals
The C# equivalent of `strconv.Quote` would be anything using:
- `System.Text.RegularExpressions` escaping
- `JsonEncodedText` or JSON string escaping
- `string.Escape` / verbatim strings
- Any custom char-by-char loop

The correct C# Cedar escape function must:
1. For each `char` (or Unicode scalar/codepoint) in the literal segment:
   - If it's a printable ASCII char (not `*`, not `\`, not `"`): emit as-is
   - If it's `\`: emit `\\`
   - If it's `"`: emit `\"`  
   - If it's `*`: emit `\*` (pattern wildcard escape)
   - If it's a Cedar named escape (`\n`, `\r`, `\t`, `\0`): emit the named form
   - Otherwise (non-printable, non-ASCII, or combining/grapheme-extend): emit `\u{XXXX}` (lowercase hex, no leading zeros beyond necessity, wrapped in braces)
2. Emit `*` for wildcard components

#### 3. Implement Cedar-compatible escape for pattern literals
Create or update a helper (e.g. `CedarEscape.EscapePatternLiteral(string s)`) that applies the per-character Cedar escape rules above. This helper should be distinct from any general Cedar string escaper if combining marks must always be escaped in pattern context (per the Go commit note about `EscapeCharAll` vs `EscapeString`).

**C# escape mapping (per Cedar spec):**
| Char | Cedar escape |
|------|-------------|
| U+0000 | `\0` |
| U+0007 (bell) | `\u{7}` |
| U+0008 (backspace) | `\u{8}` |
| U+0009 (tab) | `\t` |
| U+000A (newline) | `\n` |
| U+000C (form-feed) | `\u{c}` |
| U+000D (carriage return) | `\r` |
| U+000B (vtab) | `\u{b}` |
| U+0022 (`"`) | `\"` |
| U+002A (`*`) | `\*` |
| U+005C (`\`) | `\\` |
| Other non-printable / non-ASCII / combining | `\u{XXXX}` lowercase hex |

#### 4. Update tests
**Go tests added in `types/patttern_test.go`:**
- `"\*foo*"` — escaped wildcard star in literal
- `"\u{7}"` — bell (U+0007)
- `"\u{8}"` — backspace (U+0008)  
- `"\u{c}"` — form-feed (U+000C)
- `"\u{b}"` — vertical tab (U+000B)
- `"a\u{300}"` — combining grave accent (U+0300) after 'a'

Add equivalent xUnit `[Theory]` tests in `test/Cedar.Tests/` (or the relevant test project for `Pattern`) covering the same cases, asserting that `pattern.ToCedarString()` (or equivalent) produces the correct Cedar escape output.

#### 5. Verify round-trip
After implementing, verify that a Cedar policy containing a `like` expression with special characters can be:
1. Serialized to Cedar text with correct escapes
2. Parsed back (round-trip) without loss

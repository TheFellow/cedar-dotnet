# Finalized Port Plan: 378f896 — Trailing Comma After Resource

## Verdict: ALREADY IMPLEMENTED — acknowledge as 'implemented'

The semantic change from upstream commit 378f896 is **fully present** in the C# codebase already.
No code changes are needed. Mark the ledger entry as `implemented`.

---

## Evidence

### Go change (4952185)
`inspiration/cedar-go/internal/parser/cedar_unmarshal.go`
```go
// After parsing resource, before consuming ')'
parser.skipAtMostOnce(",")   // optionally consume trailing comma
```

### C# equivalent — already present
**File:** `src/Cedar.Core/Internal/Parser/CedarParser.cs`
**Line 78:**
```csharp
IScope resource = ScopeParser.ParseScopeConstraint(state, "resource");
state.Match(TokenType.Comma);   // ← exact equivalent of skipAtMostOnce(",")
state.Expect(TokenType.RParen, "Expected ')' after scope tuple.");
```

`ParserState.Match(TokenType)` (defined in `src/Cedar.Core/Internal/Parser/ParserState.cs:45`) peeks at the current token and advances only if it matches — identical semantics to Go's `skipAtMostOnce`.

### Tests — already present
| File | Line | Test |
|------|------|------|
| `test/Cedar.Tests/Parser/ParserTests.cs` | 512–521 | `ParseTrailingCommaInScopeTuple` — parses `permit(principal, action, resource,);` and asserts all scopes are `ScopeAll` |
| `test/Cedar.Tests/Parser/RoundTripTests.cs` | 45 | Round-trip test with `permit(principal, action, resource,) when { ((1 + 2) * 3) };` — confirms trailing comma is accepted but normalized away |

---

## Action Required

```bash
python3 semport/ledger.py update 378f896 implemented
python3 semport/ledger.py sort
git add semport/ledger.tsv
git commit -m "semport: implement 378f896 - trailing comma after resource (already present)"
rm -f .ai/semport_new_commits.md
```

---

## Go → C# Pattern Mapping (for future reference)

| Go pattern | C# equivalent |
|---|---|
| `parser.skipAtMostOnce(tok)` | `state.Match(TokenType.X)` — advances iff current token matches, no error if not |
| `parser.exact(tok)` | `state.Expect(TokenType.X, msg)` — must match or throws `ParseException` |
| `parser.peek()` | `state.Current` — current token without advancing |
| `parser.advance()` | `state.Advance()` — unconditionally consume current token |
| Go `error` return | `throw new ParseException(position, message)` — collected into `AggregateException` |

PORT

## Commit Summary
**SHA:** 4eb9960
**Message:** add feature: extended has

Adds support for chained `has` expressions in Cedar policy syntax. Instead of only `principal has attr`, users can now write `principal has a.b.c`, which the parser expands into:
`principal has a && principal.a has b && principal.a.b has c`

## Semantic Analysis

This is a parser-level feature. When parsing a `has` expression:
- **Before:** `lhs has ident` → `lhs.Has(ident)`
- **After:** `lhs has ident.ident.ident...` → chained `.And()` combining `.Has()` on each successive `.Access()` level

The desugaring is:
```
x has a.b.c
=>
x.Has("a") && x.Access("a").Has("b") && x.Access("a").Access("b").Has("c")
```

This is purely syntactic sugar in the parser — no AST node changes, no evaluator changes, no type system changes. The AST already supports `Has` and `Access` nodes; only the parser needs updating.

An error path is also added: if a dot is followed by a non-identifier token (e.g. `principal has a.b.`), the parser must return a descriptive error: `"expected ident after dot"`.

## Go Source Reference
**File:** `inspiration/cedar-go/internal/parser/cedar_unmarshal.go`
**Function:** `func (p *parser) has(lhs ast.Node) (ast.Node, error)` — lines ~590–614

Key logic:
1. Parse first attribute into `firstAttr`, build `result = lhs.Has(firstAttr)`, `currentLhs = lhs.Access(firstAttr)`
2. Loop: while next token is `.`, consume dot, consume ident, build `hasExpr = currentLhs.Has(attr)`, `result = result.And(hasExpr)`, `currentLhs = currentLhs.Access(attr)`
3. Return `result`

## Concrete Port Tasks

### 1. Find the C# parser's `has` parsing logic
**Target:** `src/Cedar.Ast/` — locate the Cedar text parser, specifically the method that handles `has` relational expressions.
Look for a method named something like `ParseHas`, `RelationHas`, or a `relation`/`relop` parsing method that calls `Has(...)`.

### 2. Update the `has` parsing method
After parsing the first identifier (the initial attribute), add a loop:
- While the next token is `.` (dot), consume it
- Consume the next token; if not an identifier, throw a parse error: `"expected ident after dot"`
- Accumulate: `result = result.And(currentLhs.Has(attr))` and `currentLhs = currentLhs.Access(attr)`
- Return the accumulated `result`

### 3. Add error test
Add a test case verifying that `principal has a.b.` (trailing dot) produces a parse error containing `"expected ident after dot"`.

### 4. Add feature test
Add a test case verifying that `principal has a.b.c` in a policy parses to the equivalent of:
`principal has a && principal.a has b && principal.a.b has c`
(i.e., the AND-chain of Has/Access nodes as shown in the Go test).

### C# Target Files (to discover via read):
- `src/Cedar.Ast/` — parser source file(s), likely `CedarParser.cs` or similar
- `test/Cedar.Tests/` — parser/policy tests, likely `PolicyParsingTests.cs` or similar

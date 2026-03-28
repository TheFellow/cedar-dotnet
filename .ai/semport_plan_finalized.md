# Semport Plan Finalized: 6f1b20e — Fix parsing of reserved keywords

## Status: ALREADY IMPLEMENTED

**The semantic change from upstream commit `6f1b20e` is fully implemented and tested in the C# codebase.**

No code changes are required. This commit should be marked `acknowledged`.

---

## Evidence

### Go change (upstream)
- **File:** `inspiration/cedar-go/internal/parser/cedar_unmarshal.go`, function `annotation()`
- **Change:** Accept `t.isIdent() || t.isReservedKeyword()` instead of just `t.isIdent()` as valid annotation key tokens
- **Helper added:** `isReservedKeyword()` on `cedar_tokenize.go`

### C# equivalent (already present)

**`src/Cedar.Core/Internal/Parser/ParserState.cs`, line 76–80**
```csharp
public Token ExpectAnnotationKey()
{
    // line 79:
    if (token.Type == TokenType.Ident || IsAnnotationKeywordToken(token.Type) || IsReservedKeywordToken(token.Type))
    // ...
```
`IsReservedKeywordToken()` at line 208 covers the same set of reserved keywords that Go's `TokenReservedKeyword` type covers.

**`test/Cedar.Tests/Parser/ParserTests.cs`, lines 134–152**
```csharp
[Fact]
public void ParseCollapsedAnnotationWithReservedKeywordKey()
{
    PolicyAst policy = ParseSingle("@is(\"bar\") permit(principal, action, resource);");
    Annotation annotation = Assert.Single(policy.Annotations);
    Assert.Equal("is", annotation.Key.Value);
    Assert.Equal("bar", annotation.Value.Value);
}

[Fact]
public void ParseInlineAnnotationWithReservedKeywordKey()
{
    PolicyAst policy = ParseSingle("@ if ( \"bar\" ) permit(principal, action, resource);");
    Annotation annotation = Assert.Single(policy.Annotations);
    Assert.Equal("if", annotation.Key.Value);
    Assert.Equal("bar", annotation.Value.Value);
}
```

Both tests **pass** as of the current build.

---

## Acceptance Criteria (all satisfied)

- [x] `@is("bar") permit(principal, action, resource);` parses successfully
- [x] Annotation key is `"is"` (a reserved keyword), annotation value is `"bar"`
- [x] `@ if ("bar") permit(principal, action, resource);` parses successfully with key `"if"`
- [x] Tests exist in `test/Cedar.Tests/Parser/ParserTests.cs`
- [x] `dotnet test --filter ParseCollapsedAnnotationWithReservedKeywordKey|ParseInlineAnnotationWithReservedKeywordKey` → Passed: 2, Failed: 0

---

## Action Required

Mark commit `6f1b20e` as **`acknowledged`** — the semantic change is already present in the C# implementation.

```bash
python3 semport/ledger.py update 6f1b20e acknowledged && python3 semport/ledger.py sort
git add semport/ledger.tsv && git commit -m "semport: acknowledge 6f1b20e - reserved keyword annotation keys already implemented"
rm -f .ai/semport_new_commits.md
```

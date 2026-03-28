PORT

## Commit Summary
**SHA:** 6f1b20e  
**Date:** 2024-09-13T14:01:23-07:00  
**Message:** Fix parsing of reserved keywords (PR #33)

The actual change is in the merge tip `aa13fe7`: annotation keys in Cedar policies must now accept reserved keywords (e.g. `@is("bar")`) in addition to plain identifiers. This mirrors upstream cedar-lang commit 5f62c6df.

---

## Semantic Analysis

Cedar policy annotations have the form `@key("value")`. Previously, `key` was required to be a plain identifier. This fix allows Cedar reserved keywords (`is`, `in`, `has`, `like`, `if`, `then`, `else`, `true`, `false`, `permit`, `forbid`, `when`, `unless`, `principal`, `action`, `resource`, `context`) to be used as annotation keys.

This is a **semantic parser change** — it affects what Cedar policy text is accepted as valid. Any policy with a reserved-keyword annotation that previously failed to parse must now succeed.

---

## Port Tasks

### 1. Locate annotation key parsing in the C# parser

**Go source:** `inspiration/cedar-go/internal/parser/cedar_unmarshal.go`, function `annotation()`, line where `t.isIdent()` is checked.  
**C# target:** `src/Cedar.Ast` — find where annotation parsing occurs (likely in the policy parser/tokenizer). Search for `annotation` or `Annotation` in the parser files.

The fix: wherever the annotation key token is validated, change the check from "must be identifier" to "must be identifier OR reserved keyword".

### 2. Locate token classification in C# tokenizer

**Go source:** `inspiration/cedar-go/internal/parser/cedar_tokenize.go` — adds `isReservedKeyword()` helper that checks `t.Type == TokenReservedKeyword`.  
**C# target:** The equivalent token type check in `src/Cedar.Ast` tokenizer. Find `TokenType` enum and the annotation-key validation path.

The fix: ensure that `TokenKind.ReservedKeyword` (or equivalent) is treated as valid for annotation keys in addition to `TokenKind.Ident`.

### 3. Add regression test

**Go source:** `inspiration/cedar-go/internal/parser/cedar_unmarshal_test.go` — adds test case `"reserved keyword annotation key"` with policy `@is("bar")\npermit ( principal, action, resource );` expected to parse successfully and produce `Annotation("is", "bar")`.  
**C# target:** `test/Cedar.Tests` — add a test (likely alongside existing annotation parsing tests) that round-trips or parses `@is("bar") permit ( principal, action, resource );` and asserts it succeeds with the correct annotation key `"is"`.

---

## Files to Read First
- `src/Cedar.Ast` — find the annotation parsing method (grep for `annotation` or `Annotation`)
- The Cedar tokenizer in `src/Cedar.Ast` — find reserved keyword token classification
- Existing annotation tests in `test/Cedar.Tests`

PORT

## Commit Summary
**SHA:** 378f896 (merge of 4952185 + b26eaae)
**Date:** 2025-11-03
**Title:** Support trailing comma after a resource statement

The Cedar parser now tolerates a trailing comma after the `resource` clause in a policy scope, i.e.:

```cedar
permit (
    principal,
    action == Action::"editPhoto",
    resource,   // <-- trailing comma now accepted
)
when { resource.owner == principal };
```

The trailing comma is silently consumed on parse but NOT emitted on marshal/pretty-print. This partially fixes cedar-go issue #103.

## Semantic Analysis

**Go change (cedar-unmarshal.go):**
- After parsing the `resource` scope clause, a new `skipAtMostOnce(",")` call optionally consumes a single trailing comma before requiring the closing `)`.
- A helper `skipAtMostOnce(tok string)` was added: peek at next token; if it matches, advance without error.

**Semantic impact:** This is a **parser tolerance/leniency** change. Previously, `permit (principal, action, resource,)` was a parse error. Now it is valid and parses identically to `permit (principal, action, resource)`. No AST change, no evaluation change — purely parser input acceptance.

**C# relevance:** Our C# parser in `Cedar.Ast` must similarly accept (and silently discard) a trailing comma after the resource scope clause. Without this fix, valid Cedar policies that contain a trailing comma will fail to parse in our implementation, creating a conformance gap.

## Port Tasks

### 1. Locate the C# policy scope parser
**Target file:** `src/Cedar.Ast/` — find the file that parses the policy scope `(principal, action, resource)` clause. Likely a `Parser.cs`, `CedarParser.cs`, or `PolicyParser.cs`. Look for where `resource` is parsed and the closing `)` is expected.

**What to find:** The sequence that parses:
1. `principal` clause
2. `,`
3. `action` clause
4. `,`
5. `resource` clause
6. `)`  ← insert optional trailing comma consumption here

### 2. Add trailing-comma tolerance after the resource clause
After the `resource` scope clause is parsed and before consuming the mandatory `)`, add logic equivalent to:

```go
parser.skipAtMostOnce(",")
```

In C#, this means: peek at the current token; if it is a `,`, advance the position (consume it) without emitting an error. If it is not `,`, do nothing (do not error).

### 3. Add a conformance test
**Target file:** `test/Cedar.Tests/` — in the policy parsing tests, add a test that:
- Parses `permit (principal, action, resource,) when { true };`
- Asserts it succeeds (no parse error)
- Asserts the resulting policy is equivalent to the same policy without the trailing comma
- Optionally assert that marshal/pretty-print does NOT emit the trailing comma

### 4. Check all three scope positions (optional / future)
The Go issue #103 notes this only *partially* fixes trailing commas — it only handles the `resource` position. The Go implementation does not yet handle trailing commas after `principal` or `action`. Our C# port should match the Go behavior exactly (only after `resource`), not over-implement.

## Files to Examine in C#
1. `src/Cedar.Ast/` — parser source (grep for "resource" or the closing paren of scope)
2. `test/Cedar.Tests/` — existing parser/policy tests to find where to add the new test

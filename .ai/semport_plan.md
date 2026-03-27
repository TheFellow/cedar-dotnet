PORT

## Commit: d267346
**Fix canMarshalAsIdent for empty strings and reserved keywords**

## Semantic Analysis

This is a correctness bug fix in Cedar policy serialization. The function `canMarshalAsIdent(s string)` determines whether an attribute name can be written using dot-notation (`context.foo`) vs. bracket-notation (`context["foo"]`). Two bugs existed:

1. **Empty string**: `canMarshalAsIdent("")` returned `true` because the loop body never executes — producing invalid Cedar like `context.` instead of `context[""]`.
2. **Reserved keywords**: `canMarshalAsIdent("true")` returned `true` — producing invalid Cedar like `context.true` instead of `context["true"]`. The Cedar language reserves keywords (`true`, `false`, `if`, `then`, `else`, `in`, `is`, `like`, `has`, `and`, `or`, `not`, `principal`, `action`, `resource`, `context`, `permit`, `forbid`, `when`, `unless`, `advice`, `__cedar`), which cannot be used as bare identifiers in attribute access expressions.

The fix: add an early-return guard `if len(s) == 0 || IsReservedKeyword(s) { return false }` before the character-by-character loop.

## Go Source Location
- **File**: `inspiration/cedar-go/internal/parser/cedar_marshal.go`
- **Function**: `canMarshalAsIdent` (~line 168)
- **Test file**: `inspiration/cedar-go/internal/parser/cedar_marshal_test.go` (3 new test cases added)

## Port Tasks

### 1. Find the C# equivalent of `canMarshalAsIdent`
Search `src/Cedar.Ast` for the logic that decides dot-notation vs. bracket-notation when serializing/printing a policy `Access` or `Has` node. Likely in a Cedar pretty-printer or marshaling class. Candidates:
- `src/Cedar.Ast/` — look for `Access`, `Has`, `Marshal`, `Print`, `Format`, or `ToString` methods on expression/AST node types.
- Look for a helper method named something like `IsValidIdentifier`, `CanUseDotAccess`, `IsIdent`, or similar.

### 2. Apply the same guard
Wherever the C# code checks if an attribute string can be used as a bare identifier in dot-notation, add:
- Guard for **empty string** (return false / use bracket-notation)
- Guard for **reserved keywords** (return false / use bracket-notation)

The C# reserved keyword list should match Cedar's, not C#'s. Find the existing `IsReservedKeyword` equivalent in the C# codebase (likely in `src/Cedar.Ast` or `src/Cedar.Core`), or create one matching Cedar's keyword set: `true`, `false`, `if`, `then`, `else`, `in`, `is`, `like`, `has`, `and`, `or`, `not`, `principal`, `action`, `resource`, `context`, `permit`, `forbid`, `when`, `unless`, `advice`, `__cedar`.

### 3. Add tests
In `test/Cedar.Tests` (or the appropriate AST test file), add xUnit test cases mirroring the Go additions:
- `context["true"]` — access with reserved keyword `"true"`
- `context[""]` — access with empty string
- `context has "if"` — has-check with reserved keyword `"if"`

These should test the **serialized Cedar string output** of the corresponding AST nodes.

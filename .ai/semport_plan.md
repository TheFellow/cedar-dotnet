PORT

## Commit Summary
- **SHA:** ace189d
- **Date:** 2024-08-23
- **Message:** types: made record output the same as the rust cedar formatter (IDX-142)

## Semantic Analysis
The upstream Go implementation changed the Cedar text serialization of `Record` values to remove the space after the colon separator between a key and its value:

- Before: `{"foo": true, "bar": 42}`
- After:  `{"foo":true, "bar":42}`

Note: there is still a space after the comma between entries (`"bar":42` is preceded by `, `), but the `: ` between key and value becomes `:`.

This is a **semantic change to the Cedar output format** — it affects how Cedar policies and values are rendered as text, which impacts round-trip parsing, conformance tests, and any output compared against the Rust cedar formatter.

## Port Tasks

### 1. Find the C# Record `Cedar()` / `ToString()` serialization method
Look in `src/Cedar.Types/` for the `Record` type (likely `Record.cs` or similar). Find the method that renders a record as a Cedar string. It will have a colon+space separator like `": "` between key and value — change it to `":"` (no space).

**Go source reference:** `inspiration/cedar-go/types/record.go` line ~94:
```go
// Before:
sb.WriteString(": ")
// After:
sb.WriteString(":")
```

**C# target:** `src/Cedar.Types/Record.cs` (or equivalent) — find the Cedar string builder logic and replace `": "` with `":"` in the key-value separator.

### 2. Update any affected tests
Look in `test/Cedar.Tests/` for tests that assert Record Cedar string output. Update expected strings from `{"key": value}` to `{"key":value}`.

**Go test reference:** `inspiration/cedar-go/types/record_test.go` and `inspiration/cedar-go/internal/eval/evalers_test.go` — both updated expected strings to remove the space after colon.

### 3. Verify conformance tests still pass
Run `dotnet test cedar-dotnet.sln` to ensure no conformance or other tests regress. If any conformance test fixtures contain `{"key": value}` format expectations, they may need updating too.

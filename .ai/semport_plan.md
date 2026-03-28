PORT

## Commit Summary
**SHA:** 0408738  
**Date:** 2025-10-22  
**PR:** #115 — `internal/schema/parser: support Path type in context decl`

The Cedar schema now allows an action's `context` in an `appliesTo` block to reference a named type via a `Path` (e.g. `context: commonContext`) in addition to an inline record literal (`context: { ... }`). This is a semantic extension to the schema grammar and AST.

## Semantic Analysis

### Go-side changes (inspiration/cedar-go)
1. **`internal/schema/ast/ast.go`** — `AppliesTo` struct: `Context *RecordType` split into:
   - `ContextPath   *Path`       — set when context is a named type reference
   - `ContextRecord *RecordType` — set when context is an inline record

2. **`internal/schema/parser/parser.go`** — Parser peeks at next token:
   - `{` → parse as `RecordType` (existing behaviour)
   - otherwise → parse as `Path` (new behaviour)

3. **`internal/schema/ast/convert_human.go`** — Human→JSON conversion: emits `EntityOrCommon` type when context is a `Path`.

4. **`internal/schema/ast/convert_json.go`** — JSON→AST conversion: type-switches on result of `convertJSONType`, routing to `ContextPath` or `ContextRecord`.

5. **`internal/schema/ast/format.go`** — Formatter: prints `ContextRecord` or `ContextPath` as appropriate.

6. **`internal/schema/ast/walk_test.go`** — Walker updated for split fields.

### Semantic impact on Cedar.Schema (C#)
The C# schema model needs the same split: an action's context can be either an inline record **or** a named-type reference (like `EntityOrCommon`). Any place that models `AppliesTo.Context` as only a record type is now incomplete.

## Concrete Port Tasks

### 1. Locate C# AppliesTo / ActionType schema model
- Target project: `src/Cedar.Schema`
- Look for a type like `ActionType`, `AppliesTo`, or `ActionDeclaration` that holds a `Context` property typed as a record.
- Expected file(s): `src/Cedar.Schema/Model/*.cs` or similar.

### 2. Split the context field
Change any `Context` property typed as an inline-record-only type to a discriminated union or two nullable properties:
```csharp
// Before (likely):
public RecordType? Context { get; init; }

// After:
public RecordType? ContextRecord { get; init; }
public string?     ContextPath   { get; init; }  // or a Path/TypeRef type
```
Or model as a `SchemaType?` union that can be either `RecordType` or `EntityOrCommonType`.

### 3. Update the schema parser / deserializer
- If parsing human-readable `.cedarschema`: update the parser to peek at the next token after `context:` and branch to `Path` vs `RecordType`.
- If deserializing JSON schema: the JSON `"type": "EntityOrCommon"` case must be routed to `ContextPath` (already supported generically if `SchemaType` is a union).

### 4. Update any schema-to-JSON conversion
Where C# converts the in-memory schema model back to JSON (or to Cedar policy JSON), emit `"type": "EntityOrCommon", "name": "<path>"` when `ContextPath` is set, and `"type": "Record", "attributes": {...}` when `ContextRecord` is set.

### 5. Update formatter / pretty-printer (if any)
If `Cedar.Schema` has a human-readable formatter, print `context: SomeType,` when path-based and `context: { ... },` when record-based.

### 6. Add tests (Cedar.Schema.Tests)
- Test parsing `context: commonContext` in a `.cedarschema` string → `ContextPath = "commonContext"`, `ContextRecord = null`.
- Test parsing `context: { ... }` → `ContextRecord` set, `ContextPath = null`.
- Test round-trip: parse → serialize back to JSON schema → deserialize → same model.
- Test with namespaced path: `context: __cedar::datetime` (if applicable).

### Reference files in Go source
| Go file | Line(s) | Change |
|---|---|---|
| `internal/schema/ast/ast.go` | ~339-348 | Split `Context` field |
| `internal/schema/parser/parser.go` | ~346-359 | Peek & branch on `{` vs path |
| `internal/schema/ast/convert_human.go` | ~66-75 | Emit `EntityOrCommon` for path |
| `internal/schema/ast/convert_json.go` | ~187-197 | Type-switch to route path vs record |
| `internal/schema/ast/format.go` | ~302-315 | Print path or record |

PORT

## Commit: 5fdb1c7
**Date:** 2025-10-21T14:50:09-07:00
**Subject:** internal/schema/parser: support Path type in context decl

---

## Semantic Analysis

The upstream change allows an action's `appliesTo` context to be specified as either:
1. An **inline record type**: `context: { field: Type, ... }` (already supported)
2. A **named type path reference**: `context: commonContext` or `context: Foo::Bar` (NEW)

### Go changes summary:
- **`internal/schema/ast/ast.go`**: `AppliesTo.Context *RecordType` split into `AppliesTo.ContextPath *Path` and `AppliesTo.ContextRecord *RecordType`
- **`internal/schema/parser/parser.go`**: Parser now peeks at the next token — if `{` parse as RecordType, else parse as Path
- **`internal/schema/ast/convert_human.go`**: When converting to JSON schema, checks `ContextRecord` first then falls back to `ContextPath` (converted via `convertType`)
- **`internal/schema/ast/convert_json.go`**: When loading from JSON, dispatches on type: `*RecordType` → `ContextRecord`, `*Path` → `ContextPath`
- **`internal/schema/ast/format.go`**: Formatter prints either `ContextRecord` or `ContextPath` depending on which is set
- **`internal/schema/ast/walk_test.go`**: Walk visits both fields independently

---

## Concrete Port Tasks

### 1. Locate the C# AppliesTo model in `src/Cedar.Schema`
- Find the schema AST/model type representing `AppliesTo` (likely in `src/Cedar.Schema/`)
- Currently the `Context` property is likely typed as a record/inline type only
- **Change**: Split into two nullable properties: `ContextRecord` (inline record shape) and `ContextPath` (named type reference string or Path type)
  - If C# uses a discriminated union / sealed hierarchy for schema types, add a new `NamedTypeRef` or reuse existing `EntityOrCommonType` variant
  - If C# uses a flat model, add a nullable `string? ContextTypeName` alongside existing context record

### 2. Update the Cedar schema text parser (`.cedarschema` human-readable format)
- **File**: Wherever the human-readable schema is parsed (search for `appliesTo`, `context:` token handling in `src/Cedar.Schema/`)
- **Change**: After consuming `context :`, peek at the next character/token:
  - If `{` → parse as inline record (existing behavior)
  - Otherwise → parse as a path identifier (e.g. `commonContext` or `Foo::Bar`) and store as `ContextPath`/`ContextTypeName`

### 3. Update JSON schema serialization/deserialization
- **File**: JSON converter for the schema (search for `"context"` handling in `src/Cedar.Schema/`)
- **Change**: When deserializing `context`, handle both `{"type":"Record","attributes":{...}}` and `{"type":"EntityOrCommon","name":"..."}` shapes
- When serializing a path-based context, emit `{"type":"EntityOrCommon","name":"<path>"}` (matching cedar-go's JSON output)

### 4. Update the formatter / pretty-printer (if one exists in Cedar.Schema)
- **File**: Any `Format`/`Print` method for schema actions
- **Change**: Print `context: <TypeName>` when path-based, `context: { ... }` when record-based

### 5. Add tests in `test/Cedar.Schema.Tests`
- Test parsing `context: commonContext` (named type reference) in a `.cedarschema` string
- Test round-trip: parse → serialize to JSON → deserialize
- Test that the JSON representation uses `{"type":"EntityOrCommon","name":"commonContext"}`
- Mirror the upstream test data in `internal/schema/parser/testdata/cases/example.cedarschema`

---

## Key Files to Investigate First
- `src/Cedar.Schema/` — find the AppliesTo / action schema model
- `src/Cedar.Schema/` — find the human-readable parser (search for `context` keyword handling)
- `src/Cedar.Schema/` — find JSON schema serialization
- `test/Cedar.Schema.Tests/` — existing schema parser tests to understand test patterns

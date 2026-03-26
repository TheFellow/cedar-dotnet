# Sprint 008: Schema Package

## Overview
Port Cedar schema parsing from Go's `x/exp/schema/` and `internal/schema/` packages. Creates a separate `Cedar.Schema` assembly for human-readable and JSON schema parsing. Schema validation is out of scope (matching Go).

## Use Cases
1. **Parse human-readable schemas**: Parse Cedar schema text into AST
2. **Parse JSON schemas**: Parse Cedar schema JSON format
3. **Format schemas**: Serialize schema AST back to human-readable text
4. **Round-trip schemas**: Schema text -> parse -> format -> re-parse
5. **Bidirectional conversion**: Convert between human-readable and JSON schema formats

## Implementation

### Phase 1: Schema project setup (~10% effort)

**Files:**
- `src/Cedar.Schema/Cedar.Schema.csproj` — References Cedar.Types
- `test/Cedar.Schema.Tests/Cedar.Schema.Tests.csproj`

### Phase 2: Schema AST and parser with tests (~45% effort)

**Files:**
- `src/Cedar.Schema/SchemaDocument.cs` — Top-level: namespaces, entity types, actions, common types
- `src/Cedar.Schema/Internal/SchemaAst.cs` — Node types: NamespaceDecl, EntityDecl, ActionDecl, TypeRef, AttributeDecl
- `src/Cedar.Schema/Internal/SchemaTokenizer.cs` — Schema-specific tokenizer
- `src/Cedar.Schema/Internal/SchemaParser.cs` — Recursive descent parser (bounded error accumulation)
- `test/Cedar.Schema.Tests/SchemaParserTests.cs` — Parse entity types, actions, common types, nested attributes, error accumulation (~12 tests)

**Acceptance:** Parser tests pass before moving to Phase 3. K8s authorization schema from Go testdata parses successfully.

### Phase 3: Schema formatting, JSON, and round-trip tests (~45% effort)

**Files:**
- `src/Cedar.Schema/Internal/SchemaWriter.cs` — Schema -> human-readable text
- `src/Cedar.Schema/Internal/SchemaJsonConverter.cs` — Schema JSON <-> SchemaDocument
- `src/Cedar.Schema/Internal/HumanToJsonConverter.cs` — Bidirectional format conversion
- `test/Cedar.Schema.Tests/SchemaWriterTests.cs` — Format output matches expected text, whitespace/indentation correct (~8 tests)
- `test/Cedar.Schema.Tests/SchemaJsonTests.cs` — JSON parse/serialize, bidirectional conversion (~8 tests)
- `test/Cedar.Schema.Tests/SchemaRoundTripTests.cs` — Text -> parse -> format -> re-parse, JSON -> parse -> JSON -> re-parse (~6 tests)

**Acceptance:** All round-trip tests pass. Schema text -> parse -> format -> re-parse produces equivalent AST.

## Files Summary

| File | Action | Purpose |
|------|--------|---------|
| `src/Cedar.Schema/Cedar.Schema.csproj` | Create | Schema library project |
| `test/Cedar.Schema.Tests/Cedar.Schema.Tests.csproj` | Create | Schema test project |
| `src/Cedar.Schema/SchemaDocument.cs` | Create | Top-level schema document |
| `src/Cedar.Schema/Internal/SchemaAst.cs` | Create | Schema AST nodes |
| `src/Cedar.Schema/Internal/SchemaTokenizer.cs` | Create | Schema tokenizer |
| `src/Cedar.Schema/Internal/SchemaParser.cs` | Create | Schema parser |
| `src/Cedar.Schema/Internal/SchemaWriter.cs` | Create | Schema text formatter |
| `src/Cedar.Schema/Internal/SchemaJsonConverter.cs` | Create | Schema JSON converter |
| `src/Cedar.Schema/Internal/HumanToJsonConverter.cs` | Create | Format converter |
| `test/Cedar.Schema.Tests/SchemaParserTests.cs` | Create | Parser behavior tests |
| `test/Cedar.Schema.Tests/SchemaWriterTests.cs` | Create | Formatter behavior tests |
| `test/Cedar.Schema.Tests/SchemaJsonTests.cs` | Create | JSON conversion tests |
| `test/Cedar.Schema.Tests/SchemaRoundTripTests.cs` | Create | Round-trip equivalence tests |

## Definition of Done

### Build gate
- [ ] `dotnet test` succeeds for Cedar.Tests, Cedar.Conformance, and Cedar.Schema.Tests, zero warnings
- [ ] **626+ unit tests** + corpus suite

### Schema parsing behavior
- [ ] Parses entity type declarations with attributes, parents, and tags
- [ ] Parses action declarations with appliesTo and memberOf
- [ ] Parses common type definitions and type references
- [ ] Parses namespaced schemas with multiple entity types
- [ ] K8s authorization schema (from Go testdata) parses successfully
- [ ] Parser accumulates errors (doesn't stop on first error)
- [ ] No validation claims beyond parsing (matches Go scope)

### Schema formatting behavior
- [ ] Schema text -> parse -> format -> re-parse produces equivalent AST
- [ ] Schema JSON -> parse -> JSON produces equivalent document
- [ ] Bidirectional conversion: human-readable <-> JSON preserves semantics

### Error handling
- [ ] Malformed schema text produces descriptive parse errors
- [ ] Empty/null input handled gracefully

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Schema syntax divergence from policy syntax | Medium | Medium | Keep schema tokenizer independent; don't share policy parser internals |
| JSON schema format underdocumented | Low | Medium | Use Go implementation as source of truth |

## Security Considerations
- Schema parsing enforces same depth/size bounds as policy parser
- No validation execution — parsing only

## Dependencies
- Sprint 007 completed (core engine stable)

## Open Questions
None identified.

# Sprint 004: Tokenizer, Recursive Descent Parser, and Cedar Text Serialization

## Overview
Port the full Cedar tokenizer and recursive descent parser from Go's `internal/parser/`. Add Cedar text serialization so policies can round-trip: Cedar text -> AST -> Cedar text. After this sprint, the library can parse arbitrary valid Cedar policy files.

## Use Cases
1. **Tokenize**: Tokenize Cedar policy text into positioned tokens
2. **Parse policies**: Parse single policies and policy lists from Cedar text
3. **Full syntax support**: Handle all Cedar syntax: scopes, conditions, nested expressions, comments, string escaping, trailing commas, extended has
4. **Serialize AST**: Serialize AST back to Cedar text (pretty-printed)
5. **Round-trip**: Parse -> serialize -> re-parse yields equivalent AST

## Implementation

### Phase 1: Tokenizer (~25% effort)

**Files:**
- `src/Cedar.Core/Internal/Parser/TokenType.cs` — Token type enum
- `src/Cedar.Core/Internal/Parser/Token.cs` — `readonly record struct Token(TokenType, string Text, Position)`
- `src/Cedar.Core/Internal/Parser/CedarTokenizer.cs` — Stream-based tokenizer: comments, string escapes, integers, operators, keywords
- `src/Cedar.Core/Internal/Rust/RustStringHelper.cs` — Port of Rust-style string unquoting (175 LOC Go)

### Phase 2: Recursive descent parser (~35% effort)

**Files:**
- `src/Cedar.Core/Internal/Parser/CedarParser.cs` — Main parser: `ParsePolicies(byte[])` -> `PolicyAst[]`
- `src/Cedar.Core/Internal/Parser/ExpressionParser.cs` — Precedence climbing for binary ops; unary, primary, member access
- `src/Cedar.Core/Internal/Parser/ScopeParser.cs` — principal/action/resource scope constraints
- `src/Cedar.Core/Internal/Parser/PatternParser.cs` — Pattern parsing for `like` operator

### Phase 3: Cedar text serializer (~20% effort)

**Files:**
- `src/Cedar.Core/Internal/Parser/CedarWriter.cs` — AST -> Cedar text with precedence-aware parenthesization

### Phase 4: Tests (~20% effort)

**Files:**
- 6 test files: TokenizerTests (~20), RustStringTests (~8), ParserTests (~25), ParserErrorTests (~12), CedarWriterTests (~10), RoundTripTests (~15)
- ~90 tests total

## Files Summary

| File | Action | Purpose |
|------|--------|---------|
| `src/Cedar.Core/Internal/Parser/TokenType.cs` | Create | Token type enum |
| `src/Cedar.Core/Internal/Parser/Token.cs` | Create | Token struct |
| `src/Cedar.Core/Internal/Parser/CedarTokenizer.cs` | Create | Stream tokenizer |
| `src/Cedar.Core/Internal/Rust/RustStringHelper.cs` | Create | Rust string unquoting |
| `src/Cedar.Core/Internal/Parser/CedarParser.cs` | Create | Main parser |
| `src/Cedar.Core/Internal/Parser/ExpressionParser.cs` | Create | Expression parser |
| `src/Cedar.Core/Internal/Parser/ScopeParser.cs` | Create | Scope parser |
| `src/Cedar.Core/Internal/Parser/PatternParser.cs` | Create | Pattern parser |
| `src/Cedar.Core/Internal/Parser/CedarWriter.cs` | Create | Cedar text serializer |

## Definition of Done
- [ ] `dotnet test` passes with **317+ tests** across 27 test files
- [ ] Tokenizer handles: identifiers, integers, strings (all escape sequences), keywords, operators, comments, positions
- [ ] Parser handles: permit/forbid, all 6 scope types, when/unless, all expression nodes, annotations, trailing commas, extended has
- [ ] Round-trip tests pass for 20+ distinct policy patterns
- [ ] Error messages include line/column positions
- [ ] Parser depth bounded to prevent stack overflow

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Operator precedence bugs | High | High | Match Go's precedence climbing exactly; round-trip tests catch drift |
| Rust string unquoting edge cases | Medium | Medium | Port Go's test cases directly |
| Stack overflow on deep nesting | Medium | High | Enforce max parse depth (~256); return error instead of crash |

## Security Considerations
- Reject invalid UTF-8 during tokenization
- Enforce maximum token count / nesting depth to prevent DoS
- String parsing validates all escape sequences
- Parser error accumulation bounded (max 10 errors, matching Go)

## Dependencies
- Sprint 003 completed (AST nodes are parser output)

## Open Questions
None identified.

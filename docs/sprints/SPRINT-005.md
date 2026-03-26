# Sprint 005: Cedar JSON Serialization and Policy Container APIs

## Overview
Port the Cedar JSON format (marshal + unmarshal) for policies, values, and entities. Build `Policy`, `PolicySet`, `PolicyList`, and stream APIs. After this sprint, the library can load policies from both Cedar text and Cedar JSON, and manage named/unnamed policy collections.

## Use Cases
1. **Policy JSON round-trip**: Cedar JSON -> AST -> Cedar JSON
2. **Value JSON round-trip**: All 12 value types serialize and deserialize correctly
3. **Policy containers**: Manage named/unnamed policy collections with PolicySet
4. **Cross-format conversion**: Cedar text -> AST -> Cedar JSON -> AST -> Cedar text
5. **Stream APIs**: Stream-based encoding/decoding of Cedar text

## Implementation

### Phase 1: Policy JSON DTOs and converters (~35% effort)

**Files:**
- `src/Cedar.Core/Internal/Json/PolicyJsonModel.cs` — DTOs matching Go's `internal/json/json.go`
- `src/Cedar.Core/Internal/Json/ScopeJsonModel.cs` — Scope serialization
- `src/Cedar.Core/Internal/Json/NodeJsonModel.cs` — Discriminated node JSON (30+ node types)
- `src/Cedar.Core/Internal/Json/PolicyJsonMarshal.cs` — AST -> JSON DTO -> JSON string
- `src/Cedar.Core/Internal/Json/PolicyJsonUnmarshal.cs` — JSON string -> DTO -> AST
- `src/Cedar.Core/Internal/Json/PolicySetJsonModel.cs` — `{ "staticPolicies": { "id": PolicyJson } }`

### Phase 2: Public policy APIs (~35% effort)

**Files:**
- `src/Cedar.Core/Policy.cs` — UnmarshalCedar(), MarshalCedar(), UnmarshalJson(), MarshalJson(), Effect, Annotations, Position, Ast
- `src/Cedar.Core/PolicySet.cs` — Add(), Get(), Remove(), All(), MarshalCedar(), MarshalJson()
- `src/Cedar.Core/PolicyList.cs` — ParseCedar() -> Policy[]
- `src/Cedar.Core/Annotations.cs` — IReadOnlyDictionary wrapper
- `src/Cedar.Core/IPolicyIterator.cs` — Policy enumeration interface for authorizer
- `src/Cedar.Core/Encoder.cs` — Stream-based Cedar text encoder
- `src/Cedar.Core/Decoder.cs` — Stream-based Cedar text decoder

### Phase 3: Tests (~30% effort)

**Files:**
- 7 test files: ValueJsonTests (~18), EntityJsonTests (~10), PolicyJsonTests (~20), PolicySetJsonTests (~8), PolicyTests (~12), PolicySetTests (~10), PolicyListTests (~6)
- ~84 tests total

## Files Summary

| File | Action | Purpose |
|------|--------|---------|
| `src/Cedar.Core/Internal/Json/PolicyJsonModel.cs` | Create | Policy JSON DTOs |
| `src/Cedar.Core/Internal/Json/ScopeJsonModel.cs` | Create | Scope JSON model |
| `src/Cedar.Core/Internal/Json/NodeJsonModel.cs` | Create | Node JSON model |
| `src/Cedar.Core/Internal/Json/PolicyJsonMarshal.cs` | Create | AST -> JSON |
| `src/Cedar.Core/Internal/Json/PolicyJsonUnmarshal.cs` | Create | JSON -> AST |
| `src/Cedar.Core/Internal/Json/PolicySetJsonModel.cs` | Create | PolicySet JSON model |
| `src/Cedar.Core/Policy.cs` | Create | Policy public API |
| `src/Cedar.Core/PolicySet.cs` | Create | PolicySet public API |
| `src/Cedar.Core/PolicyList.cs` | Create | Policy list parser |
| `src/Cedar.Core/Annotations.cs` | Create | Annotations wrapper |
| `src/Cedar.Core/IPolicyIterator.cs` | Create | Policy iterator interface |
| `src/Cedar.Core/Encoder.cs` | Create | Stream encoder |
| `src/Cedar.Core/Decoder.cs` | Create | Stream decoder |

## Definition of Done
- [ ] `dotnet test` passes with **401+ tests** across 34 test files
- [ ] Value JSON round-trips for all 12 value types
- [ ] Policy JSON round-trips: Cedar JSON -> AST -> Cedar JSON
- [ ] Entity JSON supports both implicit and explicit EntityUid formats
- [ ] PolicySet supports add/get/remove/iterate with deterministic serialization
- [ ] Cross-format: Cedar text -> AST -> Cedar JSON -> AST -> Cedar text works
- [ ] Records with literal `__entity` or `__extn` keys handled correctly (not confused with sentinels)

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| STJ polymorphic serialization for CedarValue | High | Medium | Use manual converter with discriminator, not [JsonDerivedType] |
| `__entity`/`__extn` sentinel key ambiguity | Medium | High | Match Go's disambiguation logic exactly; add collision tests |
| JSON node union complexity (30+ types) | Medium | Medium | Match Go's exact JSON structure; corpus will validate |

## Security Considerations
- JSON deserialization enforces maximum depth to prevent stack overflow
- Extension `__extn` values validated against known function names
- Reject malformed JSON with bounded error reporting

## Dependencies
- Sprint 004 completed (parser needed for Cedar text on Policy)

## Open Questions
None identified.

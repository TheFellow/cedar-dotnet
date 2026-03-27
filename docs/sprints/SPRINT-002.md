# Sprint 002: Extended Types, Collection Types, Entity System, and Serialization

## Overview
Complete the Cedar type system: extended scalars (Decimal, Datetime, Duration, IpAddress), Pattern for `like`, collection types (Set, Record), the full entity model (EntityUid, Entity, EntityMap, EntityUidSet), and JSON serialization for all value/entity types. After this sprint, the library can represent every Cedar value and round-trip entity JSON.

## Use Cases
1. **Fixed-point decimals**: Parse and construct `CedarDecimal` with 4-decimal-place fixed-point precision
2. **Datetime handling**: Construct `CedarDatetime` from millisecond epoch and parse Cedar datetime strings
3. **Duration handling**: Construct `CedarDuration` from milliseconds with day/hour/minute/second/ms units
4. **IP address validation**: Parse and validate `CedarIpAddress` for IPv4/IPv6 and CIDR ranges
5. **Pattern matching**: Build `CedarPattern` from literal and wildcard components
6. **Immutable collections**: Construct immutable `CedarSet` and `CedarRecord` with structural equality
7. **Entity graphs**: Build entity graphs with `Entity`, `EntityUid`, `EntityMap`, `EntityUidSet`
8. **JSON round-trip**: Round-trip entity and value JSON in Cedar format

## Implementation

### Phase 1: MapSet infrastructure (~10% effort)

**Files:**
- `src/Cedar.Core/Internal/MapSet/ImmutableMapSet.cs` — Generic immutable set: Contains, Equal, GetEnumerator
- `src/Cedar.Core/Internal/MapSet/MapSetBuilder.cs` — Mutable builder for efficient construction
- `src/Cedar.Core/Internal/Consts/CedarConsts.cs` — PARC variable names + time unit constants

### Phase 2: Extended scalar types (~25% effort)

**Files:**
- `src/Cedar.Types/CedarDecimal.cs` — Fixed-point (long x 10000); range +/-922337203685477.5807
- `src/Cedar.Types/CedarDatetime.cs` — Milliseconds since epoch; parse Cedar datetime format
- `src/Cedar.Types/CedarDuration.cs` — Total milliseconds; parse "5d12h30m10s500ms" format
- `src/Cedar.Types/CedarIpAddress.cs` — IPv4/IPv6 + CIDR; `Contains()` for range checks
- `src/Cedar.Types/CedarPattern.cs` — Pattern components (literal + wildcard); `Match()` method
- `src/Cedar.Types/Wildcard.cs` — Singleton marker for pattern construction

### Phase 3: Collection types (~15% effort)

**Files:**
- `src/Cedar.Types/CedarSet.cs` — Immutable set with structural equality and hash-based lookup
- `src/Cedar.Types/CedarRecord.cs` — Immutable map CedarString->CedarValue with structural equality
- `src/Cedar.Types/RecordMap.cs` — Builder/alias for record construction

### Phase 4: Entity types (~20% effort)

**Files:**
- `src/Cedar.Types/EntityType.cs` — `readonly record struct EntityType(string Value)`
- `src/Cedar.Types/EntityUid.cs` — `sealed record EntityUid(EntityType Type, CedarString Id)`
- `src/Cedar.Types/EntityUidSet.cs` — ImmutableMapSet<EntityUid> wrapper
- `src/Cedar.Types/Entity.cs` — `sealed record Entity(EntityUid Uid, EntityUidSet Parents, CedarRecord Attributes, CedarRecord Tags)`
- `src/Cedar.Types/EntityMap.cs` — ImmutableDictionary wrapper implementing IEntityGetter
- `src/Cedar.Types/IEntityGetter.cs` — `interface IEntityGetter { bool TryGet(EntityUid uid, out Entity entity); }`
- `src/Cedar.Core/Request.cs` — `record Request(EntityUid Principal, EntityUid Action, EntityUid Resource, CedarRecord Context)`
- `src/Cedar.Types/Ident.cs` — Unquoted identifier type

### Phase 5: Value and entity JSON serialization (~15% effort)

**Files:**
- `src/Cedar.Core/Internal/Json/CedarValueJsonConverter.cs` — All value types: primitives as JSON natives, EntityUid as `__entity`, extensions as `__extn`
- `src/Cedar.Core/Internal/Json/EntityUidJsonConverter.cs` — Implicit `{type,id}` and explicit `{__entity:{type,id}}` formats
- `src/Cedar.Core/Internal/Json/EntityJsonConverter.cs` — Entity: `{uid, parents, attrs, tags}`
- `src/Cedar.Core/Internal/Json/EntityMapJsonConverter.cs` — Entity array <-> EntityMap

### Phase 6: Type tests (~15% effort)

**Files:**
- 11 test files: CedarDecimalTests, CedarDatetimeTests, CedarDurationTests, CedarIpAddressTests, CedarPatternTests, CedarSetTests, CedarRecordTests, EntityUidTests, EntityTests, EntityMapTests, MapSetTests
- ~128 tests covering: parse, overflow, equality, comparison, Cedar text, JSON round-trip, structural equality for collections, entity graph traversal

## Files Summary

| File | Action | Purpose |
|------|--------|---------|
| `src/Cedar.Core/Internal/MapSet/ImmutableMapSet.cs` | Create | Generic immutable set |
| `src/Cedar.Core/Internal/MapSet/MapSetBuilder.cs` | Create | Mutable builder |
| `src/Cedar.Core/Internal/Consts/CedarConsts.cs` | Create | Constants |
| `src/Cedar.Types/CedarDecimal.cs` | Create | Fixed-point decimal |
| `src/Cedar.Types/CedarDatetime.cs` | Create | Datetime type |
| `src/Cedar.Types/CedarDuration.cs` | Create | Duration type |
| `src/Cedar.Types/CedarIpAddress.cs` | Create | IP address type |
| `src/Cedar.Types/CedarPattern.cs` | Create | Pattern matching type |
| `src/Cedar.Types/Wildcard.cs` | Create | Wildcard marker |
| `src/Cedar.Types/CedarSet.cs` | Create | Immutable set |
| `src/Cedar.Types/CedarRecord.cs` | Create | Immutable record |
| `src/Cedar.Types/RecordMap.cs` | Create | Record builder |
| `src/Cedar.Types/EntityType.cs` | Create | Entity type struct |
| `src/Cedar.Types/EntityUid.cs` | Create | Entity UID record |
| `src/Cedar.Types/EntityUidSet.cs` | Create | Entity UID set |
| `src/Cedar.Types/Entity.cs` | Create | Entity record |
| `src/Cedar.Types/EntityMap.cs` | Create | Entity map |
| `src/Cedar.Types/IEntityGetter.cs` | Create | Entity getter interface |
| `src/Cedar.Core/Request.cs` | Create | Request record |
| `src/Cedar.Types/Ident.cs` | Create | Identifier type |
| `src/Cedar.Core/Internal/Json/CedarValueJsonConverter.cs` | Create | Value JSON converter |
| `src/Cedar.Core/Internal/Json/EntityUidJsonConverter.cs` | Create | EntityUid JSON converter |
| `src/Cedar.Core/Internal/Json/EntityJsonConverter.cs` | Create | Entity JSON converter |
| `src/Cedar.Core/Internal/Json/EntityMapJsonConverter.cs` | Create | EntityMap JSON converter |

## Definition of Done
- [ ] `dotnet test` passes with **162+ tests** across 16 test files
- [ ] All 12 Cedar value types constructable with correct equality, hashing, and Cedar text
- [ ] `CedarDecimal` precision matches Go exactly: `NewDecimal(12345, -2)` -> `123.45`
- [ ] `CedarIpAddress` handles all IPv4/IPv6 and CIDR formats from Go test cases
- [ ] Entity JSON round-trips without semantic loss (both implicit and explicit EntityUid formats)
- [ ] Pattern matching handles wildcard + literal segments
- [ ] Set/Record structural equality is order-independent

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| CedarDecimal overflow mismatch with Go | High | High | Use identical long x 10000 representation; port Go edge-case tests directly |
| CedarIpAddress CIDR validation differs from Go's netip.Prefix | Medium | High | Port all Go IP test cases; validate non-canonical prefixes |
| Set hash order-independence | Medium | High | XOR-based hashing (matching Go); verify with scrambled insertion order tests |
| STJ converter design locks in before Policy JSON | Medium | Medium | Keep value converters simple; Policy JSON gets its own converters in Sprint 005 |

## Security Considerations
- Reject malformed decimal, datetime, duration, and IP strings at parse time
- CIDR prefix validation rejects invalid prefix lengths
- All collection types are immutable — no mutation after construction
- Set and record hash computation bounded by collection size

## Dependencies
- Sprint 001 completed
- `System.Collections.Immutable` (BCL)

## Open Questions
1. Should `CedarRecord` keys be `CedarString` or plain `string`? Go uses `types.String`.
2. Should `CedarPattern` eagerly compile or use character-by-character matching like Go?

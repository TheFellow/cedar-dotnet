# Cedar-DotNet Sprint Plan: Semantic Port of cedar-go to C#

**Status**: Approved
**Sprints**: 9
**Reference**: `github.com/strongdm/cedar-go` (79 source files, 75 test files, 255 tests, 4 fuzz tests, 1.5MB conformance corpus)
**Target**: .NET 9.0+, multi-project solution, idiomatic C#
**Consensus**: Claude Opus 4.6 + GPT-5.4 + Gemini 3.1 Pro (with cross-critique)

---

## Solution Layout

```text
cedar-dotnet.sln
Directory.Build.props                   # net9.0, nullable, warnings-as-errors
Directory.Packages.props                # Central NuGet version management
global.json                             # Pin .NET SDK 9.0.x
src/
  Cedar.Types/                          # Value types, entities, collections
    Cedar.Types.csproj
  Cedar.Ast/                            # AST nodes + fluent builder
    Cedar.Ast.csproj
  Cedar.Core/                           # Public API: Authorize, Policy, PolicySet
    Cedar.Core.csproj
    Internal/
      Parser/                           # Tokenizer + recursive descent parser
      Eval/                             # Compiled evaluators, constant folding
      Extensions/                       # Extension function registry
      Json/                             # Cedar JSON marshal/unmarshal
      MapSet/                           # Immutable hash-set (port of Go mapset)
      Consts/                           # PARC variable names, time units
      Rust/                             # Rust string unquoting compat
  Cedar.Schema/                         # Schema parsing (human-readable + JSON)
    Cedar.Schema.csproj
  Cedar.Batch/                          # Batch authorization (partial eval)
    Cedar.Batch.csproj
  Cedar.Experimental/                   # DOT export, node eval
    Cedar.Experimental.csproj
test/
  Cedar.Tests/                          # Unit tests for Types, Ast, Core
    Cedar.Tests.csproj
  Cedar.Conformance/                    # Corpus conformance runner
    Cedar.Conformance.csproj
  Cedar.Schema.Tests/
  Cedar.Batch.Tests/
  Cedar.Experimental.Tests/
benchmarks/
  Cedar.Benchmarks/
    Cedar.Benchmarks.csproj
testdata/
  corpus-tests.tar.gz                   # Embedded conformance corpus
```

## Design Decisions

### 1. Multi-Project Solution
Separate projects for Types, Ast, and Core provide clean dependency boundaries. Parser/Eval/Json are internal namespaces within Cedar.Core (matching Go's `internal/` packages). Sidecar assemblies (Schema, Batch, Experimental) for genuinely distinct public surfaces.

### 2. Value Type Hierarchy
Abstract sealed record class `CedarValue` with derived sealed records. Enables exhaustive `switch` expression matching. All values immutable. Concrete types: `CedarBool`, `CedarLong`, `CedarString`, `CedarDecimal`, `CedarDatetime`, `CedarDuration`, `CedarIpAddress`, `CedarSet`, `CedarRecord`, `CedarPattern`, `EntityUid`, `Entity`.

### 3. Naming Conventions
.NET idiomatic: `EntityUid` (not `EntityUID`), `PolicyId`, `CedarIpAddress`, `PolicySet`, `CedarValue`. Method names: `MarshalCedar()`, `Equals()`.

### 4. Error Handling
- Parse boundaries: throw `CedarParseException` with position info
- Evaluation: collect errors in `Diagnostic` — never throw during policy evaluation (matches Go)
- Programmer errors: `ArgumentException` / `InvalidOperationException`

### 5. Immutable Collections
- `CedarRecord`: wraps `ImmutableDictionary<CedarString, CedarValue>` with structural equality
- `CedarSet`: custom `ImmutableMapSet<CedarValue>` (port of Go's mapset) for O(1) membership with structural equality
- `EntityUidSet`: `ImmutableMapSet<EntityUid>`
- `EntityMap`: `ImmutableDictionary<EntityUid, Entity>` wrapper implementing `IEntityGetter`
- Consider `FrozenSet<T>` / `FrozenDictionary<K,V>` for read-hot paths after benchmarking

### 6. Serialization
System.Text.Json with custom `JsonConverter<T>`. Cedar JSON uses sentinel keys (`__entity`, `__extn`). Polymorphic dispatch via `[JsonDerivedType]` or manual converter with discriminator.

### 7. Thread Safety
Compiled evaluator trees and parsed policies are immutable and safe for concurrent reads. `PolicySet` is immutable after construction (builder pattern for mutation). No locking required for authorization.

### 8. CancellationToken
`BatchAuthorization.Authorize()` accepts `CancellationToken` (matching Go's `context.Context`). Core `Authorization.Authorize()` does not — it's synchronous and bounded.

---

## Sprint 001: Bootstrap, Build Infrastructure, and Primitive Values

### Overview
Create the .NET solution structure, build configuration, test infrastructure, and the three primitive Cedar value types. Establishes the foundational patterns (immutability, equality, hashing, Cedar text rendering) that every subsequent sprint builds on.

### Use Cases
- Construct `CedarBool`, `CedarLong`, and `CedarString` values with equality, hashing, and Cedar text formatting
- Build and test the solution from a clean clone
- Assert Cedar value behavior using shared test helpers

### Architecture
- `Cedar.Types.csproj` targeting `net9.0` with nullable enabled, warnings-as-errors
- `CedarValue` abstract base with `Equals(CedarValue)`, `MarshalCedar()`, `GetHashCode()`, `ToString()`
- `Cedar.Tests.csproj` with xUnit and shared `TestSupport/` helpers
- `Directory.Build.props` enforces consistent settings across all projects
- `Directory.Packages.props` centralizes NuGet versions

### Implementation

#### Phase 1: Solution scaffolding (~20% effort)
| File | Action | Purpose |
|------|--------|---------|
| `cedar-dotnet.sln` | Create | Solution file |
| `global.json` | Create | Pin SDK to 9.0.x |
| `Directory.Build.props` | Create | net9.0, nullable enable, ImplicitUsings disable, TreatWarningsAsErrors |
| `Directory.Packages.props` | Create | Central versions: xUnit 2.9+, coverlet |
| `src/Cedar.Types/Cedar.Types.csproj` | Create | Core types library |
| `src/Cedar.Ast/Cedar.Ast.csproj` | Create | AST library (references Types) |
| `src/Cedar.Core/Cedar.Core.csproj` | Create | Public API library (references Types + Ast), InternalsVisibleTo Cedar.Tests |
| `test/Cedar.Tests/Cedar.Tests.csproj` | Create | Test project |

#### Phase 2: Value base and supporting types (~25% effort)
| File | Action | Purpose |
|------|--------|---------|
| `src/Cedar.Types/CedarValue.cs` | Create | Abstract sealed record: `Equals()`, `MarshalCedar()`, abstract `ComputeHash()` |
| `src/Cedar.Core/Decision.cs` | Create | `enum Decision { Allow, Deny }` |
| `src/Cedar.Core/Effect.cs` | Create | `enum Effect { Permit, Forbid }` |
| `src/Cedar.Core/Position.cs` | Create | `readonly record struct Position(string Filename, int Offset, int Line, int Column)` |
| `src/Cedar.Core/PolicyId.cs` | Create | `readonly record struct PolicyId(string Value)` |
| `src/Cedar.Core/Diagnostic.cs` | Create | `record Diagnostic(ImmutableArray<DiagnosticReason>, ImmutableArray<DiagnosticError>)` |
| `src/Cedar.Core/DiagnosticReason.cs` | Create | `record DiagnosticReason(PolicyId, Position)` |
| `src/Cedar.Core/DiagnosticError.cs` | Create | `record DiagnosticError(PolicyId, Position, string Message)` |

#### Phase 3: Primitive value types (~30% effort)
| File | Action | Purpose |
|------|--------|---------|
| `src/Cedar.Types/CedarBool.cs` | Create | Wraps `bool`; `True`/`False` constants; Cedar text: `true`/`false` |
| `src/Cedar.Types/CedarLong.cs` | Create | Wraps `long`; Cedar text: integer literal; deterministic hash |
| `src/Cedar.Types/CedarString.cs` | Create | Wraps `string`; Cedar text: quoted with escaping; stable hash |

#### Phase 4: Test infrastructure and tests (~25% effort)
| File | Action | Purpose |
|------|--------|---------|
| `test/Cedar.Tests/TestSupport/CedarAssert.cs` | Create | Typed assertion helpers (equality, hash consistency, Cedar text) |
| `test/Cedar.Tests/Types/CedarBoolTests.cs` | Create | ~8 tests: construction, equality, hashing, Cedar text, ToString |
| `test/Cedar.Tests/Types/CedarLongTests.cs` | Create | ~10 tests: construction, equality, hashing, overflow, Cedar text |
| `test/Cedar.Tests/Types/CedarStringTests.cs` | Create | ~10 tests: construction, equality, hashing, escaping, Cedar text |
| `test/Cedar.Tests/Core/DiagnosticTests.cs` | Create | ~6 tests: construction, empty diagnostics, reason/error collections |

### Definition of Done
- `dotnet build cedar-dotnet.sln` succeeds with zero warnings
- `dotnet test` passes with **34+ tests** across 5 test files
- Primitive values demonstrate equality, hash stability, and Cedar text rendering
- Hash codes are deterministic (FNV-1a or equivalent, not process-randomized)
- CI-ready: `dotnet restore && dotnet build && dotnet test` works from clean clone

### Risks & Mitigations
| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| CedarValue hierarchy constrains all later types | Medium | High | Keep base minimal; sealed records prevent unintended extension |
| Hash algorithm choice affects perf downstream | Low | Medium | Use FNV-1a (matching Go); benchmark in Sprint 009 |
| Multi-project reference graph complexity | Low | Low | InternalsVisibleTo only where needed; Directory.Build.props enforces consistency |

### Security
- All types are immutable by construction (sealed records)
- Hash codes are deterministic and not process-randomized
- String rendering escapes Cedar special characters

### Dependencies
- .NET 9.0 SDK
- xUnit 2.9+, Microsoft.NET.Test.Sdk, coverlet.collector

### Open Questions
1. Should `CedarValue.Equals()` use virtual dispatch or pattern matching in the base?
2. Should `PolicyId` support implicit conversion from `string`?

---

## Sprint 002: Extended Types, Collection Types, Entity System, and Serialization

### Overview
Complete the Cedar type system: extended scalars (Decimal, Datetime, Duration, IpAddress), Pattern for `like`, collection types (Set, Record), the full entity model (EntityUid, Entity, EntityMap, EntityUidSet), and JSON serialization for all value/entity types. After this sprint, the library can represent every Cedar value and round-trip entity JSON.

### Use Cases
- Parse and construct `CedarDecimal` with 4-decimal-place fixed-point precision
- Construct `CedarDatetime` from millisecond epoch and parse Cedar datetime strings
- Construct `CedarDuration` from milliseconds with day/hour/minute/second/ms units
- Parse and validate `CedarIpAddress` for IPv4/IPv6 and CIDR ranges
- Build `CedarPattern` from literal and wildcard components
- Construct immutable `CedarSet` and `CedarRecord` with structural equality
- Build entity graphs with `Entity`, `EntityUid`, `EntityMap`, `EntityUidSet`
- Round-trip entity and value JSON in Cedar format

### Implementation

#### Phase 1: MapSet infrastructure (~10% effort)
- `src/Cedar.Core/Internal/MapSet/ImmutableMapSet.cs` — Generic immutable set: Contains, Equal, GetEnumerator
- `src/Cedar.Core/Internal/MapSet/MapSetBuilder.cs` — Mutable builder for efficient construction
- `src/Cedar.Core/Internal/Consts/CedarConsts.cs` — PARC variable names + time unit constants

#### Phase 2: Extended scalar types (~25% effort)
- `src/Cedar.Types/CedarDecimal.cs` — Fixed-point (long x 10000); range +/-922337203685477.5807
- `src/Cedar.Types/CedarDatetime.cs` — Milliseconds since epoch; parse Cedar datetime format
- `src/Cedar.Types/CedarDuration.cs` — Total milliseconds; parse "5d12h30m10s500ms" format
- `src/Cedar.Types/CedarIpAddress.cs` — IPv4/IPv6 + CIDR; `Contains()` for range checks
- `src/Cedar.Types/CedarPattern.cs` — Pattern components (literal + wildcard); `Match()` method
- `src/Cedar.Types/Wildcard.cs` — Singleton marker for pattern construction

#### Phase 3: Collection types (~15% effort)
- `src/Cedar.Types/CedarSet.cs` — Immutable set with structural equality and hash-based lookup
- `src/Cedar.Types/CedarRecord.cs` — Immutable map CedarString->CedarValue with structural equality
- `src/Cedar.Types/RecordMap.cs` — Builder/alias for record construction

#### Phase 4: Entity types (~20% effort)
- `src/Cedar.Types/EntityType.cs` — `readonly record struct EntityType(string Value)`
- `src/Cedar.Types/EntityUid.cs` — `sealed record EntityUid(EntityType Type, CedarString Id)`
- `src/Cedar.Types/EntityUidSet.cs` — ImmutableMapSet<EntityUid> wrapper
- `src/Cedar.Types/Entity.cs` — `sealed record Entity(EntityUid Uid, EntityUidSet Parents, CedarRecord Attributes, CedarRecord Tags)`
- `src/Cedar.Types/EntityMap.cs` — ImmutableDictionary wrapper implementing IEntityGetter
- `src/Cedar.Types/IEntityGetter.cs` — `interface IEntityGetter { bool TryGet(EntityUid uid, out Entity entity); }`
- `src/Cedar.Core/Request.cs` — `record Request(EntityUid Principal, EntityUid Action, EntityUid Resource, CedarRecord Context)`
- `src/Cedar.Types/Ident.cs` — Unquoted identifier type

#### Phase 5: Value and entity JSON serialization (~15% effort)
- `src/Cedar.Core/Internal/Json/CedarValueJsonConverter.cs` — All value types: primitives as JSON natives, EntityUid as `__entity`, extensions as `__extn`
- `src/Cedar.Core/Internal/Json/EntityUidJsonConverter.cs` — Implicit `{type,id}` and explicit `{__entity:{type,id}}` formats
- `src/Cedar.Core/Internal/Json/EntityJsonConverter.cs` — Entity: `{uid, parents, attrs, tags}`
- `src/Cedar.Core/Internal/Json/EntityMapJsonConverter.cs` — Entity array <-> EntityMap

#### Phase 6: Type tests (~15% effort)
- 11 test files: CedarDecimalTests, CedarDatetimeTests, CedarDurationTests, CedarIpAddressTests, CedarPatternTests, CedarSetTests, CedarRecordTests, EntityUidTests, EntityTests, EntityMapTests, MapSetTests
- ~128 tests covering: parse, overflow, equality, comparison, Cedar text, JSON round-trip, structural equality for collections, entity graph traversal

### Definition of Done
- `dotnet test` passes with **162+ tests** across 16 test files
- All 12 Cedar value types constructable with correct equality, hashing, and Cedar text
- `CedarDecimal` precision matches Go exactly: `NewDecimal(12345, -2)` -> `123.45`
- `CedarIpAddress` handles all IPv4/IPv6 and CIDR formats from Go test cases
- Entity JSON round-trips without semantic loss (both implicit and explicit EntityUid formats)
- Pattern matching handles wildcard + literal segments
- Set/Record structural equality is order-independent

### Risks & Mitigations
| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| CedarDecimal overflow mismatch with Go | High | High | Use identical long x 10000 representation; port Go edge-case tests directly |
| CedarIpAddress CIDR validation differs from Go's netip.Prefix | Medium | High | Port all Go IP test cases; validate non-canonical prefixes |
| Set hash order-independence | Medium | High | XOR-based hashing (matching Go); verify with scrambled insertion order tests |
| STJ converter design locks in before Policy JSON | Medium | Medium | Keep value converters simple; Policy JSON gets its own converters in Sprint 005 |

### Security
- Reject malformed decimal, datetime, duration, and IP strings at parse time
- CIDR prefix validation rejects invalid prefix lengths
- All collection types are immutable — no mutation after construction
- Set and record hash computation bounded by collection size

### Dependencies
- Sprint 001 completed
- `System.Collections.Immutable` (BCL)

### Open Questions
1. Should `CedarRecord` keys be `CedarString` or plain `string`? Go uses `types.String`.
2. Should `CedarPattern` eagerly compile or use character-by-character matching like Go?

---

## Sprint 003: Internal AST Node Hierarchy and Public Fluent Builder

### Overview
Build the internal AST representation (30+ node types, 6 scope types, policy structure) and the public fluent builder API. This sprint does not parse Cedar text yet — it provides programmatic policy construction and establishes the AST shape consumed by parser and evaluator.

### Use Cases
- Programmatically construct Cedar policies: `CedarAst.Permit().PrincipalIs("User").When(Resource().Access("owner").Equal(Principal()))`
- Build all expression types: comparisons, arithmetic, logic, collection ops, extension calls, if-then-else, like, is, in, has, tags, isEmpty
- Construct all 6 scope constraints: All, Eq, In, InSet, Is, IsIn
- Attach annotations to policies

### Implementation

#### Phase 1: Internal AST nodes (~35% effort)
- `src/Cedar.Ast/Internal/INode.cs` — Interface marker
- `src/Cedar.Ast/Internal/NodeTypes.cs` — All 30+ sealed node records: NodeEquals, NodeNotEquals, NodeLessThan, NodeLessThanOrEqual, NodeGreaterThan, NodeGreaterThanOrEqual, NodeAnd, NodeOr, NodeNot, NodeNegate, NodeAdd, NodeSub, NodeMult, NodeIn, NodeIs, NodeIsIn, NodeHas, NodeHasTag, NodeLike, NodeIfThenElse, NodeAccess, NodeGetTag, NodeContains, NodeContainsAll, NodeContainsAny, NodeIsEmpty, NodeExtensionCall, NodeValue, NodeVariable, NodeRecord, NodeSet
- `src/Cedar.Ast/Internal/ScopeTypes.cs` — Abstract IScope + 6 sealed records: ScopeAll, ScopeEq, ScopeIn, ScopeInSet, ScopeIs, ScopeIsIn
- `src/Cedar.Ast/Internal/PolicyAst.cs` — Full policy AST structure with effect, scopes, conditions, annotations, position

#### Phase 2: Public fluent builder (~40% effort)
- `src/Cedar.Ast/CedarAst.cs` — Static entry: `Permit()`, `Forbid()`, `Annotation()`
- `src/Cedar.Ast/PolicyBuilder.cs` — Scope methods, When(), Unless()
- `src/Cedar.Ast/Node.cs` — Public wrapper with fluent operator methods
- `src/Cedar.Ast/Operators.cs` — All operators: Equal, NotEqual, LessThan, And, Or, In, Has, Access, Contains, Like, Is, IsIn, GetTag, HasTag, Add, Sub, Mult, IsEmpty, IfThenElse
- `src/Cedar.Ast/Variables.cs` — Principal(), Action(), Resource(), Context()
- `src/Cedar.Ast/Values.cs` — Boolean(), String(), Long(), Set(), Record(), EntityUid(), IpAddr(), Decimal(), Datetime(), Duration()
- `src/Cedar.Ast/ExtensionOperators.cs` — Decimal comparisons, IP methods, Datetime methods, Duration methods

#### Phase 3: AST tests (~25% effort)
- 5 test files: NodeTypeTests, ScopeTests, PolicyBuilderTests, OperatorTests, VariableAndValueTests
- ~65 tests covering all node types, scopes, fluent builders, extension operators

### Definition of Done
- `dotnet test` passes with **227+ tests** across 21 test files
- All 30+ AST node types constructable and inspectable
- All 6 scope types constructable
- Fluent builder can reproduce Go's README example as a C# expression
- Builder covers 25+ operator categories

### Risks & Mitigations
| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Internal AST too Go-shaped for idiomatic C# | Medium | Medium | Use C# record inheritance, not Go-style embedding |
| Public builder API changes after parser/eval consume it | Low | High | Keep builder thin — delegates to internal nodes |

### Security
- AST nodes are immutable records — no mutation after construction

### Dependencies
- Sprint 002 completed (value types needed for NodeValue, EntityUid in scopes)

---

## Sprint 004: Tokenizer, Recursive Descent Parser, and Cedar Text Serialization

### Overview
Port the full Cedar tokenizer and recursive descent parser from Go's `internal/parser/`. Add Cedar text serialization so policies can round-trip: Cedar text -> AST -> Cedar text. After this sprint, the library can parse arbitrary valid Cedar policy files.

### Use Cases
- Tokenize Cedar policy text into positioned tokens
- Parse single policies and policy lists from Cedar text
- Handle all Cedar syntax: scopes, conditions, nested expressions, comments, string escaping, trailing commas, extended has
- Serialize AST back to Cedar text (pretty-printed)
- Round-trip: parse -> serialize -> re-parse yields equivalent AST

### Implementation

#### Phase 1: Tokenizer (~25% effort)
- `src/Cedar.Core/Internal/Parser/TokenType.cs` — Token type enum
- `src/Cedar.Core/Internal/Parser/Token.cs` — `readonly record struct Token(TokenType, string Text, Position)`
- `src/Cedar.Core/Internal/Parser/CedarTokenizer.cs` — Stream-based tokenizer: comments, string escapes, integers, operators, keywords
- `src/Cedar.Core/Internal/Rust/RustStringHelper.cs` — Port of Rust-style string unquoting (175 LOC Go)

#### Phase 2: Recursive descent parser (~35% effort)
- `src/Cedar.Core/Internal/Parser/CedarParser.cs` — Main parser: `ParsePolicies(byte[])` -> `PolicyAst[]`
- `src/Cedar.Core/Internal/Parser/ExpressionParser.cs` — Precedence climbing for binary ops; unary, primary, member access
- `src/Cedar.Core/Internal/Parser/ScopeParser.cs` — principal/action/resource scope constraints
- `src/Cedar.Core/Internal/Parser/PatternParser.cs` — Pattern parsing for `like` operator

#### Phase 3: Cedar text serializer (~20% effort)
- `src/Cedar.Core/Internal/Parser/CedarWriter.cs` — AST -> Cedar text with precedence-aware parenthesization

#### Phase 4: Tests (~20% effort)
- 6 test files: TokenizerTests (~20), RustStringTests (~8), ParserTests (~25), ParserErrorTests (~12), CedarWriterTests (~10), RoundTripTests (~15)
- ~90 tests total

### Definition of Done
- `dotnet test` passes with **317+ tests** across 27 test files
- Tokenizer handles: identifiers, integers, strings (all escape sequences), keywords, operators, comments, positions
- Parser handles: permit/forbid, all 6 scope types, when/unless, all expression nodes, annotations, trailing commas, extended has
- Round-trip tests pass for 20+ distinct policy patterns
- Error messages include line/column positions
- Parser depth bounded to prevent stack overflow

### Risks & Mitigations
| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Operator precedence bugs | High | High | Match Go's precedence climbing exactly; round-trip tests catch drift |
| Rust string unquoting edge cases | Medium | Medium | Port Go's test cases directly |
| Stack overflow on deep nesting | Medium | High | Enforce max parse depth (~256); return error instead of crash |

### Security
- Reject invalid UTF-8 during tokenization
- Enforce maximum token count / nesting depth to prevent DoS
- String parsing validates all escape sequences
- Parser error accumulation bounded (max 10 errors, matching Go)

### Dependencies
- Sprint 003 completed (AST nodes are parser output)

---

## Sprint 005: Cedar JSON Serialization and Policy Container APIs

### Overview
Port the Cedar JSON format (marshal + unmarshal) for policies, values, and entities. Build `Policy`, `PolicySet`, `PolicyList`, and stream APIs. After this sprint, the library can load policies from both Cedar text and Cedar JSON, and manage named/unnamed policy collections.

### Implementation

#### Phase 1: Policy JSON DTOs and converters (~35% effort)
- `src/Cedar.Core/Internal/Json/PolicyJsonModel.cs` — DTOs matching Go's `internal/json/json.go`
- `src/Cedar.Core/Internal/Json/ScopeJsonModel.cs` — Scope serialization
- `src/Cedar.Core/Internal/Json/NodeJsonModel.cs` — Discriminated node JSON (30+ node types)
- `src/Cedar.Core/Internal/Json/PolicyJsonMarshal.cs` — AST -> JSON DTO -> JSON string
- `src/Cedar.Core/Internal/Json/PolicyJsonUnmarshal.cs` — JSON string -> DTO -> AST
- `src/Cedar.Core/Internal/Json/PolicySetJsonModel.cs` — `{ "staticPolicies": { "id": PolicyJson } }`

#### Phase 2: Public policy APIs (~35% effort)
- `src/Cedar.Core/Policy.cs` — UnmarshalCedar(), MarshalCedar(), UnmarshalJson(), MarshalJson(), Effect, Annotations, Position, Ast
- `src/Cedar.Core/PolicySet.cs` — Add(), Get(), Remove(), All(), MarshalCedar(), MarshalJson()
- `src/Cedar.Core/PolicyList.cs` — ParseCedar() -> Policy[]
- `src/Cedar.Core/Annotations.cs` — IReadOnlyDictionary wrapper
- `src/Cedar.Core/IPolicyIterator.cs` — Policy enumeration interface for authorizer
- `src/Cedar.Core/Encoder.cs` — Stream-based Cedar text encoder
- `src/Cedar.Core/Decoder.cs` — Stream-based Cedar text decoder

#### Phase 3: Tests (~30% effort)
- 7 test files: ValueJsonTests (~18), EntityJsonTests (~10), PolicyJsonTests (~20), PolicySetJsonTests (~8), PolicyTests (~12), PolicySetTests (~10), PolicyListTests (~6)
- ~84 tests total

### Definition of Done
- `dotnet test` passes with **401+ tests** across 34 test files
- Value JSON round-trips for all 12 value types
- Policy JSON round-trips: Cedar JSON -> AST -> Cedar JSON
- Entity JSON supports both implicit and explicit EntityUid formats
- PolicySet supports add/get/remove/iterate with deterministic serialization
- Cross-format: Cedar text -> AST -> Cedar JSON -> AST -> Cedar text works
- Records with literal `__entity` or `__extn` keys handled correctly (not confused with sentinels)

### Risks & Mitigations
| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| STJ polymorphic serialization for CedarValue | High | Medium | Use manual converter with discriminator, not [JsonDerivedType] |
| `__entity`/`__extn` sentinel key ambiguity | Medium | High | Match Go's disambiguation logic exactly; add collision tests |
| JSON node union complexity (30+ types) | Medium | Medium | Match Go's exact JSON structure; corpus will validate |

### Security
- JSON deserialization enforces maximum depth to prevent stack overflow
- Extension `__extn` values validated against known function names
- Reject malformed JSON with bounded error reporting

### Dependencies
- Sprint 004 completed (parser needed for Cedar text on Policy)

---

## Sprint 006: Evaluation Engine, Extension Functions, and Authorization

### Overview
Port the evaluation engine: compile AST -> evaluator tree, evaluate against PARC environment, produce values or errors. Build `Authorize()` with fail-safe decision logic. Covers all core operators and 23+ extension functions. After this sprint, the library can authorize requests.

### Implementation

#### Phase 1: Evaluation environment and type conversion (~15% effort)
- `src/Cedar.Core/Internal/Eval/EvalEnv.cs` — record EvalEnv(IEntityGetter, CedarValue Principal/Action/Resource/Context)
- `src/Cedar.Core/Internal/Eval/IEvaluator.cs` — interface IEvaluator { CedarValue Eval(EvalEnv); }
- `src/Cedar.Core/Internal/Eval/BoolEvaluator.cs` — Wraps IEvaluator, ensures boolean result
- `src/Cedar.Core/Internal/Eval/TypeConversion.cs` — ValueToBool, ValueToLong, ValueToString, ValueToSet, ValueToRecord, ValueToEntity, ValueToDecimal, ValueToDatetime, ValueToDuration, ValueToIp
- `src/Cedar.Core/Internal/Eval/EvalErrors.cs` — Sentinel errors

#### Phase 2: Core evaluators (~30% effort)
- `src/Cedar.Core/Internal/Eval/Evaluators/LiteralEvaluator.cs`
- `src/Cedar.Core/Internal/Eval/Evaluators/VariableEvaluator.cs`
- `src/Cedar.Core/Internal/Eval/Evaluators/LogicalEvaluators.cs` — And (short-circuit), Or (short-circuit), Not
- `src/Cedar.Core/Internal/Eval/Evaluators/ComparisonEvaluators.cs` — Equal, NotEqual, LT, LTE, GT, GTE
- `src/Cedar.Core/Internal/Eval/Evaluators/ArithmeticEvaluators.cs` — Add, Sub, Mult, Negate (overflow detection)
- `src/Cedar.Core/Internal/Eval/Evaluators/CollectionEvaluators.cs` — Contains, ContainsAll, ContainsAny, IsEmpty, SetLiteral, RecordLiteral
- `src/Cedar.Core/Internal/Eval/Evaluators/MembershipEvaluators.cs` — In (hierarchy traversal), Is, IsIn
- `src/Cedar.Core/Internal/Eval/Evaluators/AccessEvaluators.cs` — AttributeAccess, Has (records + entities)
- `src/Cedar.Core/Internal/Eval/Evaluators/TagEvaluators.cs` — GetTag, HasTag
- `src/Cedar.Core/Internal/Eval/Evaluators/PatternEvaluators.cs` — Like
- `src/Cedar.Core/Internal/Eval/Evaluators/ConditionalEvaluator.cs` — IfThenElse
- `src/Cedar.Core/Internal/Eval/Evaluators/ExtensionEvaluator.cs` — Dispatches to registry

#### Phase 3: Extension function registry (~15% effort)
- `src/Cedar.Core/Internal/Extensions/ExtensionRegistry.cs` — 23 entries: name -> arity + isMethod + implementation
- `src/Cedar.Core/Internal/Extensions/DecimalExtensions.cs` — lessThan, lessThanOrEqual, greaterThan, greaterThanOrEqual
- `src/Cedar.Core/Internal/Extensions/IpAddressExtensions.cs` — isIpv4, isIpv6, isLoopback, isMulticast, isInRange
- `src/Cedar.Core/Internal/Extensions/DatetimeExtensions.cs` — toDate, toTime, offset, durationSince
- `src/Cedar.Core/Internal/Extensions/DurationExtensions.cs` — toDays, toHours, toMinutes, toSeconds, toMilliseconds
- `src/Cedar.Core/Internal/Extensions/ConstructorExtensions.cs` — ip(), decimal(), datetime(), duration()

#### Phase 4: Compiler (~15% effort)
- `src/Cedar.Core/Internal/Eval/Compiler.cs` — Compile(PolicyAst) -> BoolEvaluator; ToEval(INode) -> IEvaluator (switch dispatch over 30+ node types)
- `src/Cedar.Core/Internal/Eval/ScopeCompiler.cs` — Compile scopes into node expressions

#### Phase 5: Public authorization API (~10% effort)
- `src/Cedar.Core/Authorization.cs` — `static (Decision, Diagnostic) Authorize(IPolicyIterator, IEntityGetter, Request)`

#### Phase 6: Tests (~15% effort)
- 6 test files: TypeConversionTests (~20), EvaluatorTests (~50), ExtensionTests (~30), CompilerTests (~15), AuthorizeTests (~25), DiagnosticTests (~10)
- ~150 tests total

### Definition of Done
- `dotnet test` passes with **551+ tests** across 40 test files
- All 30+ evaluator types functional and tested
- All 23 extension functions produce results identical to Go
- Authorization: any forbid -> Deny, permits + no forbids -> Allow, no matches -> Deny
- `&&`/`||` short-circuit correctly (errors on short-circuited branch suppressed)
- Missing entities -> evaluation error, not crash or implicit allow
- Arithmetic overflow detected and reported

### Risks & Mitigations
| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Extension function type mismatch semantics | High | High | Port Go's error behavior exactly; test each extension with wrong types |
| Entity hierarchy cycles | Low | Medium | Assume DAG (matching Go); document limitation |
| Short-circuit error suppression | Medium | High | Add explicit tests: error on left + false on right, etc. |

### Security
- Authorization is fail-safe: any forbid wins; default deny
- Type errors -> diagnostic errors, not process crashes
- Missing entities -> evaluation errors, never implicit allows
- Arithmetic overflow detected
- Extension functions enforce strict arity and type checks
- Evaluation depth bounded

### Dependencies
- Sprint 005 completed

---

## Sprint 007: Constant Folding, Conformance Corpus, and Parser Hardening

### Overview
Add constant folding optimization, integrate the 1.5MB conformance corpus, and harden with property-based tests. Validates the entire stack end-to-end against the Cedar reference. After this sprint, the core engine achieves full parity with cedar-go.

### Implementation

#### Phase 1: Constant folding (~20% effort)
- `src/Cedar.Core/Internal/Eval/ConstantFolder.cs` — FoldPolicy(PolicyAst) -> optimized PolicyAst; folds constant sub-expressions into NodeValue; never folds PARC-dependent or entity-dependent expressions
- Update `Compiler.cs` — Insert FoldPolicy() before ToEval()

#### Phase 2: Conformance corpus (~40% effort)
- `testdata/corpus-tests.tar.gz` — Copy from cedar-go
- `test/Cedar.Conformance/Cedar.Conformance.csproj` — Conformance test project
- `test/Cedar.Conformance/CorpusTestData.cs` — Tar.gz extraction; scenario enumeration
- `test/Cedar.Conformance/CorpusTests.cs` — [Theory] with member data: authorize and compare decision, reasons, error policy IDs

#### Phase 3: Property-based and fuzz-seed tests (~25% effort)
- `test/Cedar.Tests/Parser/PropertyTests.cs` — FsCheck: valid Cedar -> parse -> serialize -> re-parse -> assert equivalence
- `test/Cedar.Tests/Parser/FuzzSeedTests.cs` — Port Go's fuzz corpus seeds as [Theory] cases (~20 tests)
- `test/Cedar.Tests/Eval/ConstantFolderTests.cs` — Fold arithmetic, extensions, sets; verify entity-dependent NOT folded (~18 tests)

#### Phase 4: Integration smoke tests (~15% effort)
- `test/Cedar.Tests/Integration/EndToEndTests.cs` — Full pipeline: parse Cedar text -> authorize -> verify (~10 tests)

### Definition of Done
- `dotnet test` passes with **592+ tests** + **full corpus suite**
- **100% conformance corpus pass rate** — decisions, reasons, and error policy IDs match cedar-go
- Constant folding: `1 + 1` -> `NodeValue(2)`, `decimal("3.14")` -> `NodeValue(CedarDecimal(3.14))`
- Constant folding does NOT fold PARC-dependent or entity-dependent expressions
- Property tests exercise 1000+ random valid Cedar policies without crashes
- Fuzz seed tests exercise all edge cases from Go's 4 fuzz targets

### Risks & Mitigations
| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Corpus failures expose bugs across multiple layers | High | Medium | Strong unit test coverage in Sprints 001-006 reduces blast radius |
| FsCheck generators incomplete for Cedar syntax | Medium | Low | Use Go fuzz seeds as baseline; expand generators incrementally |
| Corpus extraction adds CI time | Low | Low | Separate test project; can be excluded from quick test runs |

### Security
- Corpus inputs treated as untrusted: tar.gz extraction in-memory, path-safe, bounded
- Constant folding never evaluates PARC/entity-dependent expressions
- Property tests bounded by FsCheck default parameters

### Dependencies
- Sprint 006 completed
- FsCheck.Xunit (test-only dependency)

---

## Sprint 008: Schema Package

### Overview
Port Cedar schema parsing from Go's `x/exp/schema/` and `internal/schema/` packages. Creates a separate `Cedar.Schema` assembly for human-readable and JSON schema parsing. Schema validation is out of scope (matching Go).

### Implementation

#### Phase 1: Schema project setup (~10% effort)
- `src/Cedar.Schema/Cedar.Schema.csproj` — References Cedar.Types
- `test/Cedar.Schema.Tests/Cedar.Schema.Tests.csproj`

#### Phase 2: Schema AST and parser (~40% effort)
- `src/Cedar.Schema/SchemaDocument.cs` — Top-level: namespaces, entity types, actions, common types
- `src/Cedar.Schema/Internal/SchemaAst.cs` — Node types: NamespaceDecl, EntityDecl, ActionDecl, TypeRef, AttributeDecl
- `src/Cedar.Schema/Internal/SchemaTokenizer.cs` — Schema-specific tokenizer
- `src/Cedar.Schema/Internal/SchemaParser.cs` — Recursive descent parser (bounded error accumulation)

#### Phase 3: Schema formatting and JSON (~30% effort)
- `src/Cedar.Schema/Internal/SchemaWriter.cs` — Schema -> human-readable text
- `src/Cedar.Schema/Internal/SchemaJsonConverter.cs` — Schema JSON <-> SchemaDocument
- `src/Cedar.Schema/Internal/HumanToJsonConverter.cs` — Bidirectional format conversion

#### Phase 4: Tests (~20% effort)
- 4 test files: SchemaParserTests (~12), SchemaWriterTests (~8), SchemaJsonTests (~8), SchemaRoundTripTests (~6)
- ~34 tests total

### Definition of Done
- `dotnet test` succeeds for Cedar.Tests, Cedar.Conformance, and Cedar.Schema.Tests
- **626+ unit tests** + corpus suite
- Schema text -> parse -> format -> re-parse round-trip works
- Schema JSON -> parse -> JSON round-trip works
- No validation claims beyond parsing (matches Go scope)
- K8s authorization schema (from Go testdata) parses successfully

### Dependencies
- Sprint 007 completed (core engine stable)

---

## Sprint 009: Batch Authorization, Experimental Surface, Benchmarks, and Packaging

### Overview
Close the remaining Go surface: batch authorization (`x/exp/batch/`), standalone node evaluation (`x/exp/eval/`), DOT export (`x/exp/dot/`), performance benchmarks, NuGet packaging, and CI. After this sprint, the C# port has complete feature parity.

### Implementation

#### Phase 1: Batch authorization (~30% effort)
- `src/Cedar.Batch/Cedar.Batch.csproj` — References Cedar.Core
- `src/Cedar.Batch/BatchAuthorization.cs` — Authorize with variable substitution; accepts CancellationToken
- `src/Cedar.Batch/BatchRequest.cs` — Request template with variable placeholders
- `src/Cedar.Batch/BatchResult.cs` — Per-combination decision + diagnostic
- `src/Cedar.Batch/BatchVariable.cs` — Variable substitution types

#### Phase 2: Experimental (~20% effort)
- `src/Cedar.Experimental/Cedar.Experimental.csproj`
- `src/Cedar.Experimental/NodeEvaluation.cs` — Evaluate standalone AST node in environment
- `src/Cedar.Experimental/PartialEvaluation.cs` — Partially evaluate policy, return residual AST
- `src/Cedar.Experimental/EntityGraphDotWriter.cs` — EntityMap -> Graphviz DOT format

#### Phase 3: Tests (~20% effort)
- `test/Cedar.Batch.Tests/BatchAuthorizationTests.cs` — Variable substitution, multi-combo, cancellation (~15 tests)
- `test/Cedar.Experimental.Tests/NodeEvaluationTests.cs` — Standalone node eval (~10 tests)
- `test/Cedar.Experimental.Tests/PartialEvaluationTests.cs` — Partial eval, residuals (~10 tests)
- `test/Cedar.Experimental.Tests/DotWriterTests.cs` — DOT output, identifier quoting (~8 tests)

#### Phase 4: Benchmarks (~15% effort)
- `benchmarks/Cedar.Benchmarks/Cedar.Benchmarks.csproj` — BenchmarkDotNet
- `benchmarks/Cedar.Benchmarks/AuthorizeBenchmarks.cs` — Simple/complex/many policies
- `benchmarks/Cedar.Benchmarks/ParseBenchmarks.cs` — Cedar text and JSON throughput
- `benchmarks/Cedar.Benchmarks/TypeBenchmarks.cs` — Entity lookup, set contains, record access

#### Phase 5: Packaging and CI (~15% effort)
- Update all `.csproj` — NuGet metadata: PackageId, Version, Authors, License, RepositoryUrl
- `.github/workflows/ci.yml` — Build, test, pack on push/PR
- `CLAUDE.md` — Repository conventions for future sessions

### Definition of Done
- `dotnet test cedar-dotnet.sln` passes across ALL projects
- **687+ unit tests** + full conformance corpus across 55+ test files
- Batch authorization matches Go for all variable combination scenarios
- DOT export produces valid Graphviz DOT with properly quoted identifiers
- Benchmarks run without errors; baseline numbers documented
- NuGet packages build: `dotnet pack` succeeds for Cedar.Core, Cedar.Schema, Cedar.Batch, Cedar.Experimental
- CI pipeline: restore -> build -> test -> pack all green
- `CLAUDE.md` documents project conventions

### Risks & Mitigations
| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Batch performance scales with variable domain product | Medium | Medium | Document limits; CancellationToken for timeout |
| Partial evaluation complexity | Medium | Medium | Match Go implementation closely; bounded by test parity |
| Experimental APIs hard to retract | Low | Medium | Mark as pre-release in NuGet versioning |

### Security
- Batch APIs never treat unresolved variables as allow
- DOT export quotes all identifiers (prevent DOT injection)
- Benchmarks out-of-process, don't affect release dependencies

### Dependencies
- Sprint 007 completed (core engine + constant folding)
- Sprint 008 completed (schema)
- BenchmarkDotNet (benchmark-only)

---

## Sprint Summary Matrix

| Sprint | Focus | New Source | New Tests | Cumulative | Key Deliverable |
|--------|-------|-----------|-----------|------------|-----------------|
| 001 | Bootstrap + primitives | 12 | 5 | 34+ | Solution builds, primitive values |
| 002 | Extended types + entities + JSON | 24 | 11 | 162+ | Full Cedar type system |
| 003 | AST nodes + fluent builder | 15 | 5 | 227+ | Programmatic policy construction |
| 004 | Tokenizer + parser + Cedar text | 10 | 6 | 317+ | Parse Cedar text <-> AST |
| 005 | Cedar JSON + Policy containers | 16 | 7 | 401+ | Cedar JSON, PolicySet |
| 006 | Evaluator + extensions + Authorize | 22 | 6 | 551+ | **Core authorization works** |
| 007 | Constant folding + corpus + hardening | 3 | 5 | 592+ + corpus | **Full core parity validated** |
| 008 | Schema package | 7 | 4 | 626+ | Schema parsing |
| 009 | Batch + experimental + benchmarks | 14 | 6 | 687+ | **Complete feature parity** |

## Critical Path

```
Sprint 001 -> 002 -> 003 -> 004 -> 005 -> 006 -> 007 -> 008 -> 009
                                                    |
                                              008 can start after 007
                                              009 needs both 007 + 008
```

## End-State After Sprint 009

- **Core parity**: Authorize, Policy, PolicySet, PolicyList, all 12 Cedar value types, full parser, Cedar JSON, AST builder, constant folding, 23+ extension functions, 100% conformance corpus
- **Sidecar parity**: Schema parsing, batch authorization, partial evaluation, DOT export
- **Test coverage**: 687+ xUnit tests, FsCheck property suites, full conformance corpus, fuzz-seed tests, BenchmarkDotNet
- **Every sprint ships**: compilable, testable, independently useful increment
- **Architecture**: multi-project solution with internal namespaces; sidecar assemblies for experimental features; BCL-only for core engine; idiomatic C# throughout

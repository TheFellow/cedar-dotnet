# Cedar-DotNet Multi-Sprint Plan (Claude Draft)

## Planning Baseline

- **Reference implementation**: `github.com/strongdm/cedar-go` (fork of `cedar-policy/cedar-go`)
- **Reference surface**: 79 source files, 75 test files, 255 test functions, 4 fuzz tests, 1.5MB embedded conformance corpus
- **Reference LOC**: types/ 5,515 | internal/eval/ 8,261 | internal/parser/ 4,107 | internal/json/ 1,588 | x/exp/ast/ 1,633 | extensions/ 53 | mapset/ 706 | rust/ 390 | root 586
- **Repository starting state**: greenfield; only `.git/` and `docs/` exist
- **Target**: .NET 9.0+, BCL-only for engine, xUnit for tests
- **Test stack**: xUnit, Microsoft.NET.Test.Sdk, coverlet.collector; FluentAssertions for readability; FsCheck.Xunit for property-based tests (Sprint 7); BenchmarkDotNet for benchmarks (Sprint 8)

## Solution Layout

```text
cedar-dotnet.sln
Directory.Build.props                      # TFM, nullable, warnings-as-errors
Directory.Packages.props                   # Central package management
global.json                                # Pin .NET SDK version
src/
  Cedar/
    Cedar.csproj                           # Single core assembly
    Types/                                 # Value types, entities, collections
    Ast/                                   # Public AST builder + internal node hierarchy
    Internal/
      Consts/                              # PARC variable names, time unit constants
      MapSet/                              # Immutable hash-set (port of Go mapset)
      Parser/                              # Tokenizer + recursive descent parser
      Eval/                                # Compiled evaluators, constant folding
      Extensions/                          # Extension function registry + implementations
      Json/                                # Cedar JSON marshal/unmarshal
      Rust/                                # Rust string unquoting compat
    Authorization.cs                       # Public Authorize() entry point
    Policy.cs                              # Policy: parse, marshal, compile
    PolicySet.cs                           # Named policy collection
    PolicyList.cs                          # Unnamed policy sequence
    PolicyId.cs                            # Strongly-typed policy identifier
    Request.cs                             # Authorization request (PARC + context)
    Decision.cs                            # Allow/Deny
    Effect.cs                              # Permit/Forbid
    Diagnostic.cs                          # Reasons + errors
    Position.cs                            # Source location tracking
  Cedar.Schema/                            # Schema parsing (mirrors Go x/exp/schema)
    Cedar.Schema.csproj
  Cedar.Batch/                             # Batch authorization (mirrors Go x/exp/batch)
    Cedar.Batch.csproj
  Cedar.Experimental/                      # DOT export, node eval (mirrors Go x/exp)
    Cedar.Experimental.csproj
tests/
  Cedar.Tests/
    Cedar.Tests.csproj
    TestSupport/                            # Shared assertion helpers
    Types/                                 # Value type tests
    Ast/                                   # AST builder tests
    Parser/                                # Tokenizer + parser tests
    Eval/                                  # Evaluator + fold tests
    Json/                                  # JSON round-trip tests
    Authorization/                         # End-to-end authorize tests
    Corpus/                                # Conformance corpus runner
  Cedar.Schema.Tests/
  Cedar.Batch.Tests/
  Cedar.Experimental.Tests/
benchmarks/
  Cedar.Benchmarks/
    Cedar.Benchmarks.csproj
testdata/
  corpus-tests.tar.gz                      # Embedded conformance corpus
```

## Series-Level Design Decisions

### 1. Single Core Assembly
Keep the core engine in one `Cedar` assembly with `internal` namespaces for parser/eval/json. This matches Go's single-module structure, avoids assembly reference complexity, and allows `InternalsVisibleTo` for test access. Sidecar assemblies (`Cedar.Schema`, `Cedar.Batch`, `Cedar.Experimental`) only for genuinely separate public surface areas.

### 2. Value Type Hierarchy
Abstract sealed record class `CedarValue` with derived sealed records. This enables exhaustive pattern matching via C# `switch` expressions while keeping all values immutable and reference-equal-safe. Concrete types:
- **Value structs** (`readonly record struct`): None — avoid boxing headaches with the `CedarValue` base; everything is a sealed record class.
- **Sealed records**: `CedarBool`, `CedarLong`, `CedarString`, `CedarDecimal`, `CedarDatetime`, `CedarDuration`, `CedarIPAddress`, `CedarSet`, `CedarRecord`, `CedarPattern`, `EntityUID`, `Entity`.

### 3. Naming Conventions
Follow Cedar spec naming to maintain readability against the spec and Go source. Keep `EntityUID` (not `EntityUid`), `PolicySet` (not `PolicyCollection`), `Authorize` (not `IsAuthorized`). Use C# casing for method names: `MarshalCedar()`, `Equal()`.

### 4. Error Handling
- **Parse boundaries**: Return `Result<T, CedarError>` or throw `CedarParseException` (TBD in Sprint 3).
- **Evaluation**: Collect errors in `Diagnostic` — never throw during policy evaluation. Matches Go's `(Value, error)` pattern.
- **Programmer errors**: `ArgumentException` / `InvalidOperationException` for misuse.

### 5. Immutable Collections
- `CedarRecord` wraps `ImmutableDictionary<CedarString, CedarValue>` with custom equality.
- `CedarSet` wraps a custom hash-based set (port of Go's `mapset`) for O(1) membership with structural equality.
- `EntityUIDSet` uses the same mapset implementation.
- `EntityMap` wraps `ImmutableDictionary<EntityUID, Entity>`.

### 6. Serialization
System.Text.Json with custom `JsonConverter<T>` implementations. Cedar JSON format uses sentinel keys (`__entity`, `__extn`) for extension types. Custom converters handle both implicit and explicit entity UID formats.

---

## Sprint 1: Repository Bootstrap, Build Infrastructure, and Primitive Values

### Overview
Create the .NET solution structure, build configuration, test infrastructure, and the three primitive Cedar value types. This sprint establishes the foundational patterns (immutability, equality, hashing, Cedar text rendering) that every subsequent sprint builds on.

### Use Cases
- Construct `CedarBool`, `CedarLong`, and `CedarString` values with equality, hashing, and Cedar text formatting.
- Build and test the solution from a clean clone.
- Assert Cedar value behavior using shared test helpers.

### Architecture
- Single `Cedar.csproj` targeting `net9.0` with nullable enabled, warnings-as-errors.
- `CedarValue` abstract base with `Equal(CedarValue)`, `MarshalCedar()`, `GetHashCode()`, `ToString()`.
- `Cedar.Tests.csproj` with xUnit and shared `TestSupport/` helpers.
- `Directory.Build.props` enforces consistent settings across all future projects.
- `Directory.Packages.props` centralizes NuGet versions.

### Implementation

#### Phase 1: Solution scaffolding
| File | Purpose |
|------|---------|
| `cedar-dotnet.sln` | Solution file |
| `global.json` | Pin SDK to 9.0.x |
| `Directory.Build.props` | TFM net9.0, nullable enable, ImplicitUsings disable, TreatWarningsAsErrors |
| `Directory.Packages.props` | Central package versions: xUnit 2.9+, FluentAssertions 7+, coverlet |
| `src/Cedar/Cedar.csproj` | Core library, net9.0, InternalsVisibleTo Cedar.Tests |
| `tests/Cedar.Tests/Cedar.Tests.csproj` | Test project referencing Cedar |

#### Phase 2: Value base and supporting types
| File | Purpose |
|------|---------|
| `src/Cedar/Types/CedarValue.cs` | Abstract sealed record: `Equal()`, `MarshalCedar()`, abstract `ComputeHash()` |
| `src/Cedar/Decision.cs` | `enum Decision { Allow, Deny }` or bool-wrapper matching Go |
| `src/Cedar/Effect.cs` | `enum Effect { Permit, Forbid }` |
| `src/Cedar/Position.cs` | `readonly record struct Position(string Filename, int Offset, int Line, int Column)` |
| `src/Cedar/PolicyId.cs` | `readonly record struct PolicyId(string Value)` |
| `src/Cedar/Diagnostic.cs` | `record Diagnostic(ImmutableArray<DiagnosticReason> Reasons, ImmutableArray<DiagnosticError> Errors)` |
| `src/Cedar/DiagnosticReason.cs` | `record DiagnosticReason(PolicyId PolicyId, Position Position)` |
| `src/Cedar/DiagnosticError.cs` | `record DiagnosticError(PolicyId PolicyId, Position Position, string Message)` |

#### Phase 3: Primitive value types
| File | Purpose |
|------|---------|
| `src/Cedar/Types/CedarBool.cs` | Wraps `bool`; `True`/`False` constants; Cedar text: `true`/`false` |
| `src/Cedar/Types/CedarLong.cs` | Wraps `long`; Cedar text: integer literal; overflow checks |
| `src/Cedar/Types/CedarString.cs` | Wraps `string`; Cedar text: quoted with escaping; stable hash |

#### Phase 4: Test infrastructure and primitive tests
| File | Purpose |
|------|---------|
| `tests/Cedar.Tests/TestSupport/CedarAssert.cs` | Typed assertion helpers (value equality, hash consistency, cedar text) |
| `tests/Cedar.Tests/Types/CedarBoolTests.cs` | Construction, equality, hashing, Cedar text, ToString (~8 tests) |
| `tests/Cedar.Tests/Types/CedarLongTests.cs` | Construction, equality, hashing, overflow, Cedar text (~10 tests) |
| `tests/Cedar.Tests/Types/CedarStringTests.cs` | Construction, equality, hashing, escaping, Cedar text (~10 tests) |
| `tests/Cedar.Tests/DiagnosticTests.cs` | Construction, empty diagnostics, reason/error collections (~6 tests) |

### Files Summary
- **New source files**: 12
- **New test files**: 5
- **Key files**: `CedarValue.cs`, `CedarBool.cs`, `CedarLong.cs`, `CedarString.cs`, `Decision.cs`, `Diagnostic.cs`

### Definition of Done
- `dotnet build cedar-dotnet.sln` succeeds with zero warnings on a clean machine
- `dotnet test` passes with **34+ tests** across 5 test files
- Primitive values demonstrate equality, hash stability, and Cedar text rendering
- CI-ready: `dotnet restore && dotnet build && dotnet test` works from clean clone

### Risks
- Early `CedarValue` hierarchy design constrains all later type additions. Mitigated by keeping the base minimal and sealed.
- Hash algorithm choice affects performance downstream. Use FNV-1a or similar deterministic algorithm.

### Security
- All types are immutable by construction (sealed records).
- Hash codes are deterministic and not process-randomized.
- String rendering escapes Cedar special characters.

### Dependencies
- .NET 9.0 SDK
- xUnit 2.9+, Microsoft.NET.Test.Sdk, coverlet.collector, FluentAssertions

### Open Questions
1. Should `CedarValue.Equal()` be virtual dispatch or use pattern matching in the base? Virtual dispatch is cleaner per-type; pattern matching centralizes comparison logic.
2. Should `PolicyId` support implicit conversion from `string`?

---

## Sprint 2: Extended Scalar Types, Collection Types, and Entity System

### Overview
Complete the Cedar type system: extended scalars (Decimal, Datetime, Duration, IPAddress), the Pattern type for `like`, collection types (Set, Record), and the full entity model (EntityUID, Entity, EntityMap, EntityUIDSet). After this sprint, the library can represent every Cedar value and model complete entity graphs.

### Use Cases
- Parse and construct `CedarDecimal` with 4-decimal-place fixed-point precision and overflow validation.
- Construct `CedarDatetime` from millisecond epoch and parse from Cedar datetime strings.
- Construct `CedarDuration` from milliseconds with day/hour/minute/second/millisecond unit support.
- Parse and validate `CedarIPAddress` for IPv4/IPv6 single addresses and CIDR ranges.
- Build `CedarPattern` from literal and wildcard components for the `like` operator.
- Construct immutable `CedarSet` and `CedarRecord` with structural equality and stable hashing.
- Build entity graphs with `Entity`, `EntityUID`, `EntityMap`, and `EntityUIDSet`.
- Access entities via `IEntityGetter` interface.
- Construct `Request` objects with principal, action, resource, and context.

### Architecture
- Extended scalars in `src/Cedar/Types/` following the same sealed-record pattern.
- `CedarDecimal` stores value as `long` with implicit 4-decimal precision (multiplied by 10,000), matching Go's implementation exactly.
- `CedarDatetime` stores milliseconds since UTC epoch as `long`.
- `CedarDuration` stores total milliseconds as `long`.
- `CedarIPAddress` wraps `System.Net.IPNetwork` (or manual prefix/mask for .NET 9 compat).
- `ImmutableMapSet<T>` in `Internal/MapSet/` ports Go's `mapset` package for hash-based set operations.
- `EntityUIDSet` = `ImmutableMapSet<EntityUID>`.
- `CedarSet` uses hash-based lookup internally for O(1) `Contains()`.

### Implementation

#### Phase 1: MapSet infrastructure (port of Go internal/mapset)
| File | Purpose |
|------|---------|
| `src/Cedar/Internal/MapSet/ImmutableMapSet.cs` | Generic immutable set: `Contains()`, `Intersects()`, `Equal()`, `GetEnumerator()` |
| `src/Cedar/Internal/MapSet/MapSetBuilder.cs` | Mutable builder for constructing sets efficiently |
| `src/Cedar/Internal/Consts/CedarConsts.cs` | PARC variable names + time unit constants (port of Go `internal/consts`) |

#### Phase 2: Extended scalar types
| File | Purpose |
|------|---------|
| `src/Cedar/Types/CedarDecimal.cs` | Fixed-point decimal (long × 10,000); range ±922337203685477.5807; parse from string |
| `src/Cedar/Types/CedarDatetime.cs` | Milliseconds since epoch; parse Cedar datetime format `"2024-01-15T10:30:00Z"` |
| `src/Cedar/Types/CedarDuration.cs` | Total milliseconds; parse `"5d12h30m10s500ms"` format; unit accessors |
| `src/Cedar/Types/CedarIPAddress.cs` | IPv4/IPv6 + CIDR prefix; parse `"192.168.1.0/24"`, `"::1"`; `Contains()` for range checks |
| `src/Cedar/Types/CedarPattern.cs` | Pattern components (literal + wildcard); `Match(CedarString)` method |
| `src/Cedar/Types/Wildcard.cs` | Singleton marker type for pattern construction |
| `src/Cedar/Types/Ident.cs` | `readonly record struct Ident(string Value)` — unquoted identifier |

#### Phase 3: Collection types
| File | Purpose |
|------|---------|
| `src/Cedar/Types/CedarSet.cs` | Immutable set of `CedarValue`; structural equality; hash-based lookup |
| `src/Cedar/Types/CedarRecord.cs` | Immutable map `CedarString → CedarValue`; structural equality; ordered iteration |
| `src/Cedar/Types/RecordMap.cs` | Type alias / helper for `ImmutableDictionary<CedarString, CedarValue>` |

#### Phase 4: Entity types
| File | Purpose |
|------|---------|
| `src/Cedar/Types/EntityType.cs` | `readonly record struct EntityType(string Value)` — colon-separated path |
| `src/Cedar/Types/EntityUID.cs` | `sealed record EntityUID(EntityType Type, CedarString Id)` with Cedar text + hash |
| `src/Cedar/Types/EntityUIDSet.cs` | `ImmutableMapSet<EntityUID>` wrapper |
| `src/Cedar/Types/Entity.cs` | `sealed record Entity(EntityUID UID, EntityUIDSet Parents, CedarRecord Attributes, CedarRecord Tags)` |
| `src/Cedar/Types/EntityMap.cs` | `ImmutableDictionary<EntityUID, Entity>` wrapper implementing `IEntityGetter` |
| `src/Cedar/Types/IEntityGetter.cs` | `interface IEntityGetter { bool TryGet(EntityUID uid, out Entity entity); }` |
| `src/Cedar/Request.cs` | `record Request(EntityUID Principal, EntityUID Action, EntityUID Resource, CedarRecord Context)` |

#### Phase 5: Type tests
| File | Purpose |
|------|---------|
| `tests/Cedar.Tests/Internal/MapSetTests.cs` | Contains, Intersects, Equal, Empty, iteration (~12 tests) |
| `tests/Cedar.Tests/Types/CedarDecimalTests.cs` | Parse, overflow, equality, comparison, Cedar text (~16 tests) |
| `tests/Cedar.Tests/Types/CedarDatetimeTests.cs` | Parse, epoch, equality, Cedar text (~12 tests) |
| `tests/Cedar.Tests/Types/CedarDurationTests.cs` | Parse, units, equality, Cedar text (~12 tests) |
| `tests/Cedar.Tests/Types/CedarIPAddressTests.cs` | Parse IPv4/6, CIDR, contains, equality (~14 tests) |
| `tests/Cedar.Tests/Types/CedarPatternTests.cs` | Literal, wildcard, match/no-match (~10 tests) |
| `tests/Cedar.Tests/Types/CedarSetTests.cs` | Construction, equality, contains, containsAll, containsAny, empty, hash (~14 tests) |
| `tests/Cedar.Tests/Types/CedarRecordTests.cs` | Construction, equality, access, has, hash, iteration (~14 tests) |
| `tests/Cedar.Tests/Types/EntityUIDTests.cs` | Construction, equality, hash, Cedar text, type path (~10 tests) |
| `tests/Cedar.Tests/Types/EntityTests.cs` | Construction, parents, attributes, tags (~8 tests) |
| `tests/Cedar.Tests/Types/EntityMapTests.cs` | Get, missing entity, iteration (~6 tests) |

### Files Summary
- **New source files**: 20
- **New test files**: 11
- **Key files**: `CedarDecimal.cs`, `CedarIPAddress.cs`, `CedarSet.cs`, `CedarRecord.cs`, `EntityUID.cs`, `Entity.cs`, `EntityMap.cs`, `ImmutableMapSet.cs`

### Definition of Done
- `dotnet test` passes with **162+ tests** (34 prior + 128 new) across 16 test files
- All 12 Cedar value types constructable with correct equality, hashing, and Cedar text rendering
- `CedarDecimal` precision matches Go exactly: `NewDecimal(12345, -2)` → `123.45`
- `CedarIPAddress` handles all IPv4/IPv6 and CIDR formats from Go test cases
- Entity graph traversal via parents works correctly
- Pattern matching handles wildcard + literal segments

### Risks
- `CedarDecimal` overflow semantics must exactly match Go's `int64 × 10000` representation. Off-by-one in parsing will cascade into evaluation bugs.
- `CedarIPAddress` CIDR prefix validation must match Go's `netip.Prefix` behavior, particularly for non-canonical prefixes.
- Custom set hashing must be order-independent (XOR-based) to match Go's approach.

### Security
- Reject malformed decimal strings, datetime formats, duration strings, and IP addresses at parse time.
- CIDR prefix validation rejects invalid prefix lengths.
- All collection types are immutable — no mutation after construction.
- Set and record hash computation is bounded by collection size.

### Dependencies
- Sprint 1 completed
- `System.Collections.Immutable` (part of BCL)

### Open Questions
1. Should `CedarRecord` keys be `CedarString` or plain `string` internally? Go uses `types.String` as keys. Using `CedarString` maintains type safety but adds allocation.
2. Should `CedarPattern` eagerly compile regex, or use a simple character-by-character matcher like Go?

---

## Sprint 3: Internal AST Node Hierarchy and Public Fluent Builder

### Overview
Build the internal AST representation (all 30+ node types, scope types, policy structure) and the public fluent builder API. This sprint does not parse Cedar text yet — it provides programmatic policy construction and establishes the AST shape that parser and evaluator will consume.

### Use Cases
- Programmatically construct Cedar policies: `Permit().PrincipalIs("User").When(Resource().Access("owner").Equal(Principal()))`.
- Build all expression types: comparisons, arithmetic, logic, collection ops, extension calls, if-then-else, like, is, in, has, tags.
- Construct scope constraints: All, Eq, In, InSet, Is, IsIn for principal/action/resource.
- Attach annotations to policies.
- Inspect AST nodes for downstream compilation.

### Architecture

**Two-level AST** (matching Go's `ast/` + `x/exp/ast/` split):
- **Internal AST** (`src/Cedar/Ast/Internal/`): Full node hierarchy with all structural details. Used by parser, evaluator, JSON, and constant folder.
- **Public AST** (`src/Cedar/Ast/`): Thin fluent wrappers that construct internal nodes. This is the API consumers use.

**Node hierarchy** (all `sealed record` types implementing `INode`):
- `BinaryNode(INode Left, INode Right)` — base for binary ops
- `UnaryNode(INode Arg)` — base for unary ops
- `StrOpNode(INode Arg, CedarString Value)` — access/has with string key
- 30+ concrete node types matching Go's `x/exp/ast/node.go`

**Scope types** (discriminated union via sealed hierarchy):
- `ScopeAll`, `ScopeEq`, `ScopeIn`, `ScopeInSet`, `ScopeIs`, `ScopeIsIn`

### Implementation

#### Phase 1: Internal AST node types
| File | Purpose |
|------|---------|
| `src/Cedar/Ast/Internal/INode.cs` | `interface INode { }` marker |
| `src/Cedar/Ast/Internal/BinaryNode.cs` | `record BinaryNode(INode Left, INode Right)` |
| `src/Cedar/Ast/Internal/UnaryNode.cs` | `record UnaryNode(INode Arg)` |
| `src/Cedar/Ast/Internal/StrOpNode.cs` | `record StrOpNode(INode Arg, CedarString Value)` |
| `src/Cedar/Ast/Internal/NodeTypes.cs` | All 30+ sealed node records: `NodeEquals`, `NodeNotEquals`, `NodeLessThan`, `NodeLessThanOrEqual`, `NodeGreaterThan`, `NodeGreaterThanOrEqual`, `NodeAnd`, `NodeOr`, `NodeNot`, `NodeNegate`, `NodeAdd`, `NodeSub`, `NodeMult`, `NodeIn`, `NodeIs`, `NodeIsIn`, `NodeHas`, `NodeHasTag`, `NodeLike`, `NodeIfThenElse`, `NodeAccess`, `NodeGetTag`, `NodeContains`, `NodeContainsAll`, `NodeContainsAny`, `NodeIsEmpty`, `NodeExtensionCall`, `NodeValue`, `NodeVariable`, `NodeRecord`, `NodeSet` |
| `src/Cedar/Ast/Internal/RecordElementNode.cs` | `record RecordElementNode(CedarString Key, INode Value)` |
| `src/Cedar/Ast/Internal/ConditionType.cs` | `record ConditionType(ConditionKind Kind, INode Body)` — when/unless |
| `src/Cedar/Ast/Internal/ConditionKind.cs` | `enum ConditionKind { When, Unless }` |

#### Phase 2: Scope and policy AST
| File | Purpose |
|------|---------|
| `src/Cedar/Ast/Internal/Scope.cs` | Abstract `IScope` + 6 sealed records: `ScopeAll`, `ScopeEq(EntityUID)`, `ScopeIn(EntityUID)`, `ScopeInSet(ImmutableArray<EntityUID>)`, `ScopeIs(EntityType)`, `ScopeIsIn(EntityType, EntityUID)` |
| `src/Cedar/Ast/Internal/PolicyAst.cs` | `record PolicyAst(Effect Effect, ImmutableArray<AnnotationType> Annotations, IScope Principal, IScope Action, IScope Resource, ImmutableArray<ConditionType> Conditions, Position Position)` |
| `src/Cedar/Ast/Internal/AnnotationType.cs` | `record AnnotationType(Ident Key, CedarString Value)` |

#### Phase 3: Public fluent builder API
| File | Purpose |
|------|---------|
| `src/Cedar/Ast/Node.cs` | Public `Node` wrapper around `INode`; fluent operator methods |
| `src/Cedar/Ast/Policy.cs` | Public `Policy` builder: `Permit()`, `Forbid()`, scope methods, `When()`, `Unless()` |
| `src/Cedar/Ast/Annotation.cs` | Annotations builder: `Annotation(key, value).Permit()` / `.Forbid()` |
| `src/Cedar/Ast/Operators.cs` | Extension methods on `Node`: `.Equal()`, `.NotEqual()`, `.LessThan()`, `.And()`, `.Or()`, `.In()`, `.Has()`, `.Access()`, `.Contains()`, `.Like()`, `.Is()`, `.IsIn()`, `.GetTag()`, `.HasTag()`, `.Add()`, `.Sub()`, `.Mult()`, `.IsEmpty()`, `.IfThenElse()` |
| `src/Cedar/Ast/Variables.cs` | Static methods: `Principal()`, `Action()`, `Resource()`, `Context()` |
| `src/Cedar/Ast/Values.cs` | Static constructors: `Boolean()`, `String()`, `Long()`, `Set()`, `Record()`, `EntityUID()`, `IPAddr()`, `Decimal()`, `Datetime()`, `Duration()`, `ExtensionCall()`, `Value()` |
| `src/Cedar/Ast/DecimalOperators.cs` | Decimal comparison methods: `.DecimalLessThan()`, etc. |
| `src/Cedar/Ast/IPOperators.cs` | IP methods: `.IsIpv4()`, `.IsIpv6()`, `.IsLoopback()`, `.IsMulticast()`, `.IsInRange()` |
| `src/Cedar/Ast/DatetimeOperators.cs` | Datetime methods: `.ToDate()`, `.ToTime()`, `.Offset()`, `.DurationSince()` |
| `src/Cedar/Ast/DurationOperators.cs` | Duration methods: `.ToDays()`, `.ToHours()`, `.ToMinutes()`, `.ToSeconds()`, `.ToMilliseconds()` |

#### Phase 4: AST tests
| File | Purpose |
|------|---------|
| `tests/Cedar.Tests/Ast/NodeTypeTests.cs` | Verify all 30+ node types construct correctly (~15 tests) |
| `tests/Cedar.Tests/Ast/ScopeTests.cs` | All 6 scope types construct correctly (~8 tests) |
| `tests/Cedar.Tests/Ast/PolicyBuilderTests.cs` | Build permit/forbid policies with scopes, conditions, annotations (~12 tests) |
| `tests/Cedar.Tests/Ast/OperatorTests.cs` | All fluent operators produce correct node types (~20 tests) |
| `tests/Cedar.Tests/Ast/VariableAndValueTests.cs` | Variable and value constructors (~10 tests) |

### Files Summary
- **New source files**: 20
- **New test files**: 5
- **Key files**: `NodeTypes.cs`, `Scope.cs`, `PolicyAst.cs`, `Node.cs` (public), `Policy.cs` (public), `Operators.cs`

### Definition of Done
- `dotnet test` passes with **227+ tests** (162 prior + 65 new) across 21 test files
- All 30+ AST node types constructable and inspectable
- All 6 scope types constructable
- Fluent builder can reproduce Go's README authorization example as a C# expression
- Builder covers: comparison (6), logical (3), arithmetic (3), collection (4), membership (3), access (2), tag (2), pattern (1), conditional (1), extension calls — total 25+ operator categories

### Risks
- If internal AST nodes are too Go-shaped (embedding structs), the C# code will feel unnatural. Mitigated by using C# record inheritance rather than Go-style embedding.
- Public builder API must be stable — changes after parser and evaluator consume it will cascade.

### Security
- AST nodes are immutable records — no mutation after construction.
- No user input processing in this sprint (no parsing).

### Dependencies
- Sprint 2 completed (value types needed for `NodeValue`, `EntityUID` in scopes)

### Open Questions
1. Should public `Node` be a `readonly record struct` wrapping `INode`, or a class? Struct avoids heap allocation but may box when stored in collections.
2. Should extension call operators be methods on `Node` (e.g., `node.IsIpv4()`) or static methods? Go uses both patterns.

---

## Sprint 4: Tokenizer, Recursive Descent Parser, and Cedar Text Serialization

### Overview
Port the full Cedar tokenizer and recursive descent parser from Go's `internal/parser/`. This sprint also adds Cedar text serialization (marshal) so policies can round-trip: Cedar text → AST → Cedar text. After this sprint, the library can parse arbitrary valid Cedar policy files.

### Use Cases
- Tokenize Cedar policy text into a stream of positioned tokens.
- Parse single policies and policy lists from Cedar text.
- Handle all Cedar syntax: scope constraints, when/unless conditions, nested expressions, comments, string escaping, trailing commas.
- Serialize AST back to Cedar text (pretty-printed).
- Round-trip: parse → serialize → re-parse yields equivalent AST.

### Architecture
- **Tokenizer** (`CedarTokenizer`): Stream-based scanner producing `Token` structs with type, text, position, and value extraction methods. Matches Go's `internal/parser/cedar_tokenize.go` (520 LOC).
- **Parser** (`CedarParser`): Recursive descent consuming token stream, producing `PolicyAst` nodes. Matches Go's `internal/parser/cedar_unmarshal.go` (1,034 LOC).
- **Serializer** (`CedarWriter`): AST → Cedar text with proper indentation and escaping. Matches Go's `internal/parser/cedar_marshal.go` (434 LOC).
- **Rust string compat** (`RustStringHelper`): Port of Go's `internal/rust/rust.go` for Rust-style string unquoting.

### Implementation

#### Phase 1: Tokenizer
| File | Purpose |
|------|---------|
| `src/Cedar/Internal/Parser/TokenType.cs` | `enum TokenType { EOF, Ident, Int, ReservedKeyword, String, Operator, Unknown }` |
| `src/Cedar/Internal/Parser/Token.cs` | `readonly record struct Token(TokenType Type, string Text, Position Pos)` with `StringValue()`, `IntValue()` helpers |
| `src/Cedar/Internal/Parser/CedarTokenizer.cs` | Stream-based tokenizer: `Tokenize(ReadOnlySpan<byte>)` → `Token[]`; handles comments, string escapes, integer literals, operators, keywords |
| `src/Cedar/Internal/Rust/RustStringHelper.cs` | Port of Rust-style string unquoting (175 LOC Go source) |

#### Phase 2: Recursive descent parser
| File | Purpose |
|------|---------|
| `src/Cedar/Internal/Parser/CedarParser.cs` | Main parser class: `ParsePolicies(byte[])` → `PolicyAst[]`; token cursor management; error accumulation |
| `src/Cedar/Internal/Parser/ExpressionParser.cs` | Expression parsing: precedence climbing for binary ops; unary, primary, member access, method calls |
| `src/Cedar/Internal/Parser/ScopeParser.cs` | Scope constraint parsing: principal/action/resource `==`, `in`, `is`, `is in`, `in [set]` |
| `src/Cedar/Internal/Parser/PatternParser.cs` | Pattern parsing for `like` operator (port of `internal/parser/pattern.go`, 26 LOC) |

#### Phase 3: Cedar text serializer
| File | Purpose |
|------|---------|
| `src/Cedar/Internal/Parser/CedarWriter.cs` | AST → Cedar text: policy formatting, expression precedence-aware parenthesization, string escaping |
| `src/Cedar/Internal/Parser/PolicySlice.cs` | Thin wrapper matching Go's `parser.PolicySlice` |

#### Phase 4: Tokenizer and parser tests
| File | Purpose |
|------|---------|
| `tests/Cedar.Tests/Parser/TokenizerTests.cs` | Token types, positions, keywords, string escapes, integers, comments, error cases (~20 tests) |
| `tests/Cedar.Tests/Parser/RustStringTests.cs` | Rust-style unquoting edge cases (~8 tests) |
| `tests/Cedar.Tests/Parser/ParserTests.cs` | Parse valid policies: permit/forbid, scopes, conditions, all expression types, annotations (~25 tests) |
| `tests/Cedar.Tests/Parser/ParserErrorTests.cs` | Invalid Cedar text: syntax errors, unterminated strings, bad escapes, position tracking (~12 tests) |
| `tests/Cedar.Tests/Parser/CedarWriterTests.cs` | Serialize AST to Cedar text; verify formatting (~10 tests) |
| `tests/Cedar.Tests/Parser/RoundTripTests.cs` | Parse → serialize → re-parse equivalence for diverse policy set (~15 tests) |

### Files Summary
- **New source files**: 10
- **New test files**: 6
- **Key files**: `CedarTokenizer.cs`, `CedarParser.cs`, `ExpressionParser.cs`, `CedarWriter.cs`

### Definition of Done
- `dotnet test` passes with **317+ tests** (227 prior + 90 new) across 27 test files
- Tokenizer handles: identifiers, integers, strings (with all escape sequences), reserved keywords, operators, comments, EOF, source positions
- Parser handles: permit/forbid effects, principal/action/resource scopes (all 6 types), when/unless conditions, all expression node types, annotations, trailing commas
- Cedar text serializer produces valid, re-parseable output
- Round-trip tests pass for at least 20 distinct policy patterns
- Error messages include line/column positions

### Risks
- Operator precedence bugs are the #1 parser risk. Mitigated by: (a) matching Go's precedence climbing exactly, (b) round-trip tests that catch serialization drift.
- Rust string unquoting edge cases (Unicode escapes, null bytes). Mitigated by porting Go's test cases directly.

### Security
- Reject invalid UTF-8 during tokenization.
- Enforce maximum token count / nesting depth to prevent DoS on hostile input.
- String parsing validates all escape sequences; rejects invalid ones with positioned errors.
- No unbounded recursion — parser depth is bounded.

### Dependencies
- Sprint 3 completed (AST nodes are the parser's output type)

### Open Questions
1. Should the parser use `ReadOnlySpan<byte>` or `string` input? Span is faster but less ergonomic. Go uses `[]byte`.
2. Should parse errors be collected (like Go) or fail-fast? Collected errors match Go behavior and are more user-friendly.

---

## Sprint 5: Cedar JSON Serialization and Policy Container APIs

### Overview
Port the Cedar JSON format (marshal + unmarshal) for policies, values, and entities. Build the public `Policy`, `PolicySet`, and `PolicyList` container APIs. After this sprint, the library can load policies from both Cedar text and Cedar JSON, and manage named/unnamed policy collections.

### Use Cases
- Parse policies from Cedar JSON format.
- Serialize policies to Cedar JSON format.
- Round-trip: Cedar JSON → AST → Cedar JSON.
- Marshal/unmarshal entities to/from JSON (explicit and implicit EntityUID formats).
- Marshal/unmarshal extension values (decimal, IP, datetime, duration) using `__extn` sentinel format.
- Manage named policies via `PolicySet.Add()`, `.Get()`, `.Remove()`, `.All()`.
- Parse unnamed policy lists via `PolicyList`.
- Convert between Cedar text and JSON formats.

### Architecture
- **JSON DTOs** (`src/Cedar/Internal/Json/`): Dedicated serialization models matching Cedar JSON spec exactly. Separate from AST nodes to isolate format changes.
- **JSON converters**: `System.Text.Json` custom converters for policies, nodes, values, entities.
- **Policy containers**: `PolicySet` (named), `PolicyList` (unnamed) — both implement policy iteration for the authorizer.

### Implementation

#### Phase 1: Value and entity JSON
| File | Purpose |
|------|---------|
| `src/Cedar/Internal/Json/ValueJsonConverter.cs` | Marshal/unmarshal Cedar values: primitives as JSON natives, EntityUID as `{"__entity": {...}}`, extensions as `{"__extn": {"fn": "...", "arg": "..."}}` |
| `src/Cedar/Internal/Json/EntityUIDJsonConverter.cs` | Both implicit `{"type","id"}` and explicit `{"__entity": {"type","id"}}` formats |
| `src/Cedar/Internal/Json/EntityJsonConverter.cs` | Entity: `{"uid", "parents", "attrs", "tags"}` |
| `src/Cedar/Internal/Json/EntityMapJsonConverter.cs` | Entity array ↔ `EntityMap` |

#### Phase 2: Policy JSON (AST nodes)
| File | Purpose |
|------|---------|
| `src/Cedar/Internal/Json/PolicyJsonModel.cs` | DTO: `PolicyJson { Effect, Annotations, Principal, Action, Resource, Conditions }` matching Go's `internal/json/json.go` |
| `src/Cedar/Internal/Json/ScopeJsonModel.cs` | DTO: `ScopeJson { Op, Entity, Entities, EntityType, In }` |
| `src/Cedar/Internal/Json/NodeJsonModel.cs` | DTO: discriminated node JSON with all operators, values, vars, extension calls |
| `src/Cedar/Internal/Json/PolicyJsonMarshal.cs` | AST → JSON DTO → JSON string (port of Go `internal/json/json_marshal.go`, 330 LOC) |
| `src/Cedar/Internal/Json/PolicyJsonUnmarshal.cs` | JSON string → JSON DTO → AST (port of Go `internal/json/json_unmarshal.go`, 338 LOC) |
| `src/Cedar/Internal/Json/PolicySetJsonModel.cs` | DTO: `{ "staticPolicies": { "id": PolicyJson } }` |

#### Phase 3: Public policy APIs
| File | Purpose |
|------|---------|
| `src/Cedar/Policy.cs` | `sealed class Policy`: `UnmarshalCedar(byte[])`, `MarshalCedar()`, `UnmarshalJson(byte[])`, `MarshalJson()`, `Effect`, `Annotations`, `Position`, `AST` |
| `src/Cedar/PolicySet.cs` | Named collection: `Add(PolicyId, Policy)`, `Get(PolicyId)`, `Remove(PolicyId)`, `All()` iterator, `MarshalCedar()`, `MarshalJson()`, `UnmarshalJson()` |
| `src/Cedar/PolicyList.cs` | `static ParseCedar(byte[])` → `Policy[]`; unnamed parsing |
| `src/Cedar/Annotations.cs` | `IReadOnlyDictionary<Ident, CedarString>` wrapper |
| `src/Cedar/IPolicyIterator.cs` | Interface for policy enumeration used by authorizer |

#### Phase 4: JSON and policy tests
| File | Purpose |
|------|---------|
| `tests/Cedar.Tests/Json/ValueJsonTests.cs` | Primitives, EntityUID (implicit/explicit), extensions (__extn format) (~18 tests) |
| `tests/Cedar.Tests/Json/EntityJsonTests.cs` | Entity marshal/unmarshal, EntityMap round-trip (~10 tests) |
| `tests/Cedar.Tests/Json/PolicyJsonTests.cs` | Policy JSON round-trip: all node types, scopes, conditions (~20 tests) |
| `tests/Cedar.Tests/Json/PolicySetJsonTests.cs` | PolicySet JSON round-trip, multi-policy (~8 tests) |
| `tests/Cedar.Tests/PolicyTests.cs` | Policy construct/parse/marshal via both formats (~12 tests) |
| `tests/Cedar.Tests/PolicySetTests.cs` | Add, get, remove, iterate, deterministic serialization order (~10 tests) |
| `tests/Cedar.Tests/PolicyListTests.cs` | Parse multi-policy Cedar text (~6 tests) |

### Files Summary
- **New source files**: 16
- **New test files**: 7
- **Key files**: `PolicyJsonMarshal.cs`, `PolicyJsonUnmarshal.cs`, `ValueJsonConverter.cs`, `Policy.cs`, `PolicySet.cs`

### Definition of Done
- `dotnet test` passes with **401+ tests** (317 prior + 84 new) across 34 test files
- Value JSON round-trips for all 12 value types
- Policy JSON round-trips: Cedar JSON → AST → Cedar JSON matches for all node types
- Entity JSON supports both implicit and explicit EntityUID formats
- `PolicySet` supports add/get/remove/iterate with deterministic serialization
- Cross-format: Cedar text → AST → Cedar JSON → AST → Cedar text round-trip works

### Risks
- Cedar JSON sentinel keys (`__entity`, `__extn`) require careful disambiguation during deserialization. A record with a key literally named `__entity` must not trigger entity parsing.
- JSON node discriminated union is complex (30+ node types in a single JSON object). Mitigated by matching Go's exact JSON structure.

### Security
- Reject malformed JSON with bounded error reporting.
- JSON deserialization enforces maximum depth to prevent stack overflow.
- Extension `__extn` values validated against known function names.
- EntityUID type and ID strings validated for correct formatting.

### Dependencies
- Sprint 4 completed (parser needed for Cedar text on `Policy`)
- `System.Text.Json` (part of BCL)

### Open Questions
1. Should `PolicySet` preserve insertion order? Go sorts by key on serialization.
2. Should `Policy` cache its compiled evaluator, or defer until Sprint 6?

---

## Sprint 6: Core Evaluation Engine and Authorization

### Overview
Port the evaluation engine: compile AST → evaluator tree, evaluate against PARC environment, produce values or errors. Build the public `Authorize()` entry point with fail-safe decision logic. This sprint covers all core operators and the extension function registry. After this sprint, the library can authorize requests — the primary use case.

### Use Cases
- Compile a parsed policy into a reusable evaluator tree.
- Evaluate boolean, comparison, arithmetic, logical, collection, membership, access, has, like, is, is-in, if-then-else, tag, and extension call expressions.
- Authorize a request against a policy set: permit/forbid/default-deny with full diagnostic output.
- Evaluate all 23 extension functions (4 constructors + 19 methods).
- Type-convert evaluation results with clear error messages.

### Architecture
- **Evaluator interface** (`IEvaluator`): `CedarValue Eval(EvalEnv env)` + error propagation.
- **Compiled evaluator tree**: Each AST node compiles to a specialized evaluator (matching Go's `internal/eval/convert.go`). 30+ evaluator types.
- **Extension registry**: Static dictionary mapping function names to `(arity, isMethod, implementation)` (port of Go's `internal/extensions/extensions.go`).
- **Authorization loop**: Iterate all policies, collect permits/forbids/errors, apply fail-safe decision logic (port of Go's `authorize.go`).
- **Type conversion utilities**: `ValueToBool()`, `ValueToLong()`, `ValueToString()`, `ValueToSet()`, `ValueToRecord()`, `ValueToEntity()`, `ValueToDecimal()`, `ValueToDatetime()`, `ValueToDuration()`, `ValueToIP()`.

### Implementation

#### Phase 1: Evaluation environment and type conversion
| File | Purpose |
|------|---------|
| `src/Cedar/Internal/Eval/EvalEnv.cs` | `record EvalEnv(IEntityGetter Entities, CedarValue Principal, CedarValue Action, CedarValue Resource, CedarValue Context)` |
| `src/Cedar/Internal/Eval/IEvaluator.cs` | `interface IEvaluator { CedarValue Eval(EvalEnv env); }` |
| `src/Cedar/Internal/Eval/BoolEvaluator.cs` | Wraps `IEvaluator`, ensures boolean result |
| `src/Cedar/Internal/Eval/TypeConversion.cs` | `ValueToBool()`, `ValueToLong()`, `ValueToString()`, `ValueToSet()`, `ValueToRecord()`, `ValueToEntity()`, `ValueToDecimal()`, `ValueToDatetime()`, `ValueToDuration()`, `ValueToIP()`, `TypeName()` |
| `src/Cedar/Internal/Eval/EvalErrors.cs` | Sentinel errors: overflow, unknown extension, arity, attribute access, tag access, entity not exist, unspecified entity |

#### Phase 2: Core evaluators (port of Go eval/evalers.go, 1,541 LOC)
| File | Purpose |
|------|---------|
| `src/Cedar/Internal/Eval/Evaluators/LiteralEvaluator.cs` | Returns constant value |
| `src/Cedar/Internal/Eval/Evaluators/VariableEvaluator.cs` | Returns principal/action/resource/context from env |
| `src/Cedar/Internal/Eval/Evaluators/LogicalEvaluators.cs` | `AndEvaluator` (short-circuit), `OrEvaluator` (short-circuit), `NotEvaluator` |
| `src/Cedar/Internal/Eval/Evaluators/ComparisonEvaluators.cs` | `EqualEvaluator`, `NotEqualEvaluator`, `LessThanEvaluator`, `LessThanOrEqualEvaluator`, `GreaterThanEvaluator`, `GreaterThanOrEqualEvaluator` |
| `src/Cedar/Internal/Eval/Evaluators/ArithmeticEvaluators.cs` | `AddEvaluator`, `SubtractEvaluator`, `MultiplyEvaluator`, `NegateEvaluator` (with overflow detection) |
| `src/Cedar/Internal/Eval/Evaluators/CollectionEvaluators.cs` | `ContainsEvaluator`, `ContainsAllEvaluator`, `ContainsAnyEvaluator`, `IsEmptyEvaluator`, `SetLiteralEvaluator`, `RecordLiteralEvaluator` |
| `src/Cedar/Internal/Eval/Evaluators/MembershipEvaluators.cs` | `InEvaluator` (entity hierarchy traversal), `IsEvaluator` (type check), `IsInEvaluator` (type + parent) |
| `src/Cedar/Internal/Eval/Evaluators/AccessEvaluators.cs` | `AttributeAccessEvaluator`, `HasEvaluator` (attribute existence on records and entities) |
| `src/Cedar/Internal/Eval/Evaluators/TagEvaluators.cs` | `GetTagEvaluator`, `HasTagEvaluator` |
| `src/Cedar/Internal/Eval/Evaluators/PatternEvaluators.cs` | `LikeEvaluator` (pattern matching) |
| `src/Cedar/Internal/Eval/Evaluators/ConditionalEvaluator.cs` | `IfThenElseEvaluator` |
| `src/Cedar/Internal/Eval/Evaluators/ExtensionEvaluator.cs` | Dispatches to extension registry by name |

#### Phase 3: Extension function registry (port of Go internal/extensions + eval extension dispatch)
| File | Purpose |
|------|---------|
| `src/Cedar/Internal/Extensions/ExtensionRegistry.cs` | Static dictionary: 23 entries mapping name → arity + isMethod + implementation |
| `src/Cedar/Internal/Extensions/DecimalExtensions.cs` | `lessThan`, `lessThanOrEqual`, `greaterThan`, `greaterThanOrEqual` |
| `src/Cedar/Internal/Extensions/IPAddressExtensions.cs` | `isIpv4`, `isIpv6`, `isLoopback`, `isMulticast`, `isInRange` |
| `src/Cedar/Internal/Extensions/DatetimeExtensions.cs` | `toDate`, `toTime`, `offset`, `durationSince` |
| `src/Cedar/Internal/Extensions/DurationExtensions.cs` | `toDays`, `toHours`, `toMinutes`, `toSeconds`, `toMilliseconds` |
| `src/Cedar/Internal/Extensions/ConstructorExtensions.cs` | `ip()`, `decimal()`, `datetime()`, `duration()` — type constructors |

#### Phase 4: Compiler (AST → evaluator tree)
| File | Purpose |
|------|---------|
| `src/Cedar/Internal/Eval/Compiler.cs` | `Compile(PolicyAst)` → `BoolEvaluator`; `ToEval(INode)` → `IEvaluator` — switch dispatch over 30+ node types (port of Go `eval/compile.go` + `eval/convert.go`) |
| `src/Cedar/Internal/Eval/ScopeCompiler.cs` | Compile scope constraints into node expressions, then into evaluators |

#### Phase 5: Public authorization API
| File | Purpose |
|------|---------|
| `src/Cedar/Authorization.cs` | `static (Decision, Diagnostic) Authorize(IPolicyIterator policies, IEntityGetter entities, Request request)` — iterate all policies, collect permits/forbids/errors, fail-safe decision |

#### Phase 6: Evaluation and authorization tests
| File | Purpose |
|------|---------|
| `tests/Cedar.Tests/Eval/TypeConversionTests.cs` | All 10 type conversion functions, error cases (~20 tests) |
| `tests/Cedar.Tests/Eval/EvaluatorTests.cs` | Each evaluator type in isolation: literals, variables, logic, comparison, arithmetic, collection, membership, access, tags, pattern, conditional (~50 tests) |
| `tests/Cedar.Tests/Eval/ExtensionTests.cs` | All 23 extension functions: constructors, decimal comparison, IP checks, datetime/duration methods (~30 tests) |
| `tests/Cedar.Tests/Eval/CompilerTests.cs` | AST → evaluator compilation for all node types (~15 tests) |
| `tests/Cedar.Tests/Authorization/AuthorizeTests.cs` | End-to-end: permit, forbid, default-deny, mixed, errors in diagnostic, multi-policy (~25 tests) |
| `tests/Cedar.Tests/Authorization/DiagnosticTests.cs` | Diagnostic reasons, error collection, policy ID tracking (~10 tests) |

### Files Summary
- **New source files**: 22
- **New test files**: 6
- **Key files**: `Compiler.cs`, `Authorization.cs`, `ExtensionRegistry.cs`, `EvalEnv.cs`, all evaluator files

### Definition of Done
- `dotnet test` passes with **551+ tests** (401 prior + 150 new) across 40 test files
- All 30+ evaluator types functional and tested
- All 23 extension functions produce results identical to Go
- Authorization produces correct Allow/Deny decisions with diagnostic reasons/errors
- Fail-safe semantics: any forbid → Deny, permits with no forbids → Allow, no matches → Deny
- `&&` and `||` short-circuit correctly (left side evaluated first; right side skipped if result determined)
- Missing entities handled as evaluation errors, not crashes
- Arithmetic overflow detected and reported as evaluation errors

### Risks
- Extension function type mismatch semantics must exactly match Go. For example, calling `isIpv4` on a non-IP value must produce the same error type.
- Entity hierarchy traversal in `InEvaluator` must handle cycles (or assume DAG like Go does).
- Short-circuit behavior in `&&`/`||` must match Go: errors on short-circuited branch are suppressed.

### Security
- Authorization is fail-safe: any forbid wins; default is deny.
- Type errors surface as diagnostic errors, never process crashes.
- Missing entities produce evaluation errors, never implicit allows.
- Arithmetic operations detect and report overflow.
- Extension functions enforce strict arity and type checks.
- Evaluation depth is bounded to prevent stack overflow on deeply nested expressions.

### Dependencies
- Sprint 5 completed (policies + JSON needed to construct test fixtures)

### Open Questions
1. Should `Policy` eagerly compile its evaluator on parse, or lazily on first `Authorize()` call? Go compiles eagerly in `Authorize()`.
2. Should the evaluator tree be pooled/cached? Premature optimization — defer until benchmarks exist.

---

## Sprint 7: Constant Folding, Conformance Corpus, and Parser Hardening

### Overview
Add constant folding optimization (compile-time evaluation of static expressions), integrate the 1.5MB conformance test corpus, and harden the parser with property-based tests. This sprint validates the entire stack end-to-end against the Cedar reference implementation. After this sprint, the core engine achieves full parity with cedar-go.

### Use Cases
- Constant expressions (`1 + 1`, `decimal("3.14")`, `[1,2,3].contains(2)`) are evaluated at compile time, reducing runtime work.
- Execute the full conformance corpus (60,000+ test scenarios) and match cedar-go decisions.
- Property-based tests verify parser robustness: arbitrary valid Cedar → parse → serialize → re-parse.
- Fuzz-equivalent seed tests exercise parser edge cases from Go's fuzz corpus.

### Architecture
- **Constant folder** (`ConstantFolder`): Pre-evaluation pass over AST. Recursively attempts to fold constant sub-expressions into `NodeValue` nodes. Expressions referencing PARC variables or entities are not folded. Matches Go's `eval/fold.go` (277 LOC).
- **Conformance runner**: Extract `corpus-tests.tar.gz` at test time, enumerate scenarios as xUnit `[Theory]` member data, compare decisions + reasons + error policy IDs.
- **Property tests**: FsCheck generators for valid Cedar expressions; parse → serialize → re-parse equivalence.

### Implementation

#### Phase 1: Constant folding (port of Go eval/fold.go)
| File | Purpose |
|------|---------|
| `src/Cedar/Internal/Eval/ConstantFolder.cs` | `FoldPolicy(PolicyAst)` → optimized `PolicyAst`; `Fold(INode)` → folded `INode`; `TryFold()`, `TryFoldBinary()`, `TryFoldUnary()` helpers. Only folds when all children are constants and evaluation succeeds. Never folds entity-dependent expressions (`In`, `IsIn`, `GetTag`, `HasTag`, `Access`/`Has` on EntityUID). |

#### Phase 2: Integrate constant folding into compilation pipeline
| File | Purpose |
|------|---------|
| Update `src/Cedar/Internal/Eval/Compiler.cs` | Insert `FoldPolicy()` before `ToEval()` — matching Go's `Compile()` flow: fold → scope-to-node → compile |

#### Phase 3: Conformance corpus
| File | Purpose |
|------|---------|
| `testdata/corpus-tests.tar.gz` | Copy from cedar-go (1.5MB embedded conformance corpus) |
| `tests/Cedar.Tests/Corpus/CorpusTestData.cs` | Tar.gz extraction utility; enumerate test scenarios; parse schema/policies/entities/requests |
| `tests/Cedar.Tests/Corpus/CorpusTests.cs` | `[Theory]` with member data: for each scenario, authorize and compare decision, reasons (policy IDs), error policy IDs against expected values |

#### Phase 4: Property-based and fuzz-seed tests
| File | Purpose |
|------|---------|
| `tests/Cedar.Tests/Parser/PropertyTests.cs` | FsCheck: generate valid Cedar policy text → parse → serialize → re-parse → assert AST equivalence (~3 property tests) |
| `tests/Cedar.Tests/Parser/FuzzSeedTests.cs` | Port Go's fuzz corpus seeds as `[Theory]` cases; verify no crashes on edge-case inputs (~20 tests from Go's 4 fuzz targets) |
| `tests/Cedar.Tests/Eval/ConstantFolderTests.cs` | Fold arithmetic, extension constructors, set/record literals, verify entity-dependent expressions are NOT folded (~18 tests) |

### Files Summary
- **New source files**: 1 (ConstantFolder.cs) + 1 update (Compiler.cs)
- **New test files**: 5
- **Key files**: `ConstantFolder.cs`, `CorpusTests.cs`, `PropertyTests.cs`

### Definition of Done
- `dotnet test` passes with **592+ tests** (551 prior + 41 new) + **full corpus suite** across 45 test files
- Constant folding reduces `1 + 1` to `NodeValue(2)`, `decimal("3.14")` to `NodeValue(CedarDecimal(3.14))`, etc.
- Constant folding does NOT fold expressions referencing PARC variables, entities, or entity operations
- Conformance corpus passes: decisions match cedar-go for all scenarios
- Property-based tests exercise 1000+ random valid Cedar policies without parser crashes
- Fuzz seed tests exercise all edge cases from Go's 4 fuzz targets

### Risks
- Corpus failures may expose bugs in any layer (parser, JSON, evaluator, extensions, type system). This sprint is the integration gauntlet. Mitigated by: strong unit test coverage in Sprints 1–6.
- FsCheck Cedar generators may be incomplete, missing valid syntax corners. Mitigated by: using fuzz seeds from Go as a baseline.
- Constant folding must preserve error semantics: expressions that would error at runtime should NOT be folded into errors at compile time (matching Go behavior).

### Security
- Conformance corpus inputs treated as untrusted: tar.gz extraction is in-memory, path-safe, bounded.
- Constant folding never evaluates expressions that depend on runtime data (PARC, entities).
- Property tests bounded by FsCheck's default test count and size parameters.

### Dependencies
- Sprint 6 completed (evaluator needed for constant folding and corpus authorization)
- FsCheck.Xunit NuGet package (test-only dependency)
- Access to cedar-go corpus archive

### Open Questions
1. Should the corpus runner report failures as individual xUnit test failures, or collect into a summary? Individual failures are more debuggable.
2. Should constant folding be opt-out (always on, like Go) or opt-in? Go always folds.

---

## Sprint 8: Schema Package and Parser Hardening

### Overview
Port the Cedar schema parsing capabilities from Go's `x/exp/schema/` (283 LOC) and `internal/schema/` packages. This sprint creates a separate `Cedar.Schema` assembly for human-readable and JSON schema parsing. Schema validation is out of scope (matching Go's current limitations).

### Use Cases
- Parse Cedar human-readable schema text into a `SchemaDocument`.
- Convert schema between human-readable and JSON formats.
- Use schemas in integration tests consuming the conformance corpus.
- Format schemas back to human-readable text.

### Architecture
- Separate `Cedar.Schema` assembly referencing `Cedar` for core types only.
- Schema AST: `SchemaDocument` containing namespace declarations, entity types, action declarations, common types.
- Schema tokenizer and parser: separate from policy parser (different grammar).

### Implementation

#### Phase 1: Schema project setup
| File | Purpose |
|------|---------|
| `src/Cedar.Schema/Cedar.Schema.csproj` | Schema library, net9.0, references Cedar |
| `tests/Cedar.Schema.Tests/Cedar.Schema.Tests.csproj` | Schema test project |

#### Phase 2: Schema AST and parser
| File | Purpose |
|------|---------|
| `src/Cedar.Schema/SchemaDocument.cs` | Top-level schema: namespaces, entity types, actions, common types |
| `src/Cedar.Schema/Internal/SchemaAst.cs` | Schema node types: `NamespaceDecl`, `EntityDecl`, `ActionDecl`, `TypeRef`, `AttributeDecl` |
| `src/Cedar.Schema/Internal/SchemaTokenizer.cs` | Schema-specific tokenizer |
| `src/Cedar.Schema/Internal/SchemaParser.cs` | Recursive descent schema parser |
| `src/Cedar.Schema/Internal/SchemaWriter.cs` | Schema → human-readable text |

#### Phase 3: Schema JSON
| File | Purpose |
|------|---------|
| `src/Cedar.Schema/Internal/SchemaJsonConverter.cs` | Schema JSON ↔ `SchemaDocument` |

#### Phase 4: Schema tests
| File | Purpose |
|------|---------|
| `tests/Cedar.Schema.Tests/SchemaParserTests.cs` | Parse entity types, actions, namespaces (~12 tests) |
| `tests/Cedar.Schema.Tests/SchemaWriterTests.cs` | Format schema to text (~8 tests) |
| `tests/Cedar.Schema.Tests/SchemaJsonTests.cs` | JSON round-trip (~8 tests) |
| `tests/Cedar.Schema.Tests/SchemaRoundTripTests.cs` | Text → parse → format → re-parse (~6 tests) |

### Files Summary
- **New source files**: 7
- **New test files**: 4
- **Key files**: `SchemaDocument.cs`, `SchemaParser.cs`, `SchemaJsonConverter.cs`

### Definition of Done
- `dotnet test` passes for both `Cedar.Tests` and `Cedar.Schema.Tests`
- Schema tests: **34+ tests** across 4 test files
- Schema text → parse → format → re-parse round-trip works
- Schema JSON → parse → JSON round-trip works
- No validation claims beyond parsing (matches Go's scope)

### Risks
- Schema grammar is less well-documented than policy grammar. Mitigated by direct port from Go source.
- Schema and policy tokenizers may diverge subtly. Mitigated by keeping them separate.

### Security
- Schema parser error accumulation is bounded.
- No code execution from schema content.

### Dependencies
- Sprint 7 completed (core engine stable)

### Open Questions
1. Should `Cedar.Schema` live in the same solution or a separate one? Same solution reduces friction; separate reduces compile times.

---

## Sprint 9: Batch Authorization, Experimental Surface, and DOT Export

### Overview
Port the remaining Go experimental surface: batch authorization (`x/exp/batch/`, 1,195 LOC), standalone node evaluation (`x/exp/eval/`, 784 LOC), and entity graph DOT export (`x/exp/dot/`, 246 LOC). These are sidecar assemblies with experimental APIs.

### Use Cases
- Run batch authorization with variable substitution across multiple request combinations.
- Evaluate standalone AST nodes in an environment (for testing, debugging, and query planning).
- Partially evaluate policies to identify which variables affect the decision.
- Export entity relationship graphs as Graphviz DOT format for visualization.

### Architecture
- `Cedar.Batch`: Batch authorization with request variable substitution. Iterates over variable combinations, collecting per-combination decisions.
- `Cedar.Experimental`: Node evaluation helpers, partial policy evaluation, DOT export.
- Both assemblies reference `Cedar` core and use `InternalsVisibleTo` for evaluator access.

### Implementation

#### Phase 1: Batch authorization
| File | Purpose |
|------|---------|
| `src/Cedar.Batch/Cedar.Batch.csproj` | Batch library |
| `src/Cedar.Batch/BatchAuthorization.cs` | `Authorize(policies, entities, batchRequest)` → results per combination |
| `src/Cedar.Batch/BatchRequest.cs` | Request template with variable placeholders |
| `src/Cedar.Batch/BatchResult.cs` | Per-combination result with decision + diagnostic |
| `src/Cedar.Batch/BatchVariable.cs` | Variable substitution types |

#### Phase 2: Experimental evaluation
| File | Purpose |
|------|---------|
| `src/Cedar.Experimental/Cedar.Experimental.csproj` | Experimental library |
| `src/Cedar.Experimental/NodeEvaluation.cs` | Evaluate standalone AST node in environment |
| `src/Cedar.Experimental/PartialEvaluation.cs` | Partially evaluate policy, returning residual AST |
| `src/Cedar.Experimental/PartialValue.cs` | Value-or-residual discriminated type |

#### Phase 3: DOT export
| File | Purpose |
|------|---------|
| `src/Cedar.Experimental/EntityGraphDotWriter.cs` | `EntityMap` → DOT format: entity nodes, parent edges, cluster by type |

#### Phase 4: Tests
| File | Purpose |
|------|---------|
| `tests/Cedar.Batch.Tests/Cedar.Batch.Tests.csproj` | Batch test project |
| `tests/Cedar.Batch.Tests/BatchAuthorizationTests.cs` | Variable substitution, multi-combo, decision correctness (~15 tests) |
| `tests/Cedar.Experimental.Tests/Cedar.Experimental.Tests.csproj` | Experimental test project |
| `tests/Cedar.Experimental.Tests/NodeEvaluationTests.cs` | Standalone node eval (~10 tests) |
| `tests/Cedar.Experimental.Tests/PartialEvaluationTests.cs` | Partial eval, residual expressions (~10 tests) |
| `tests/Cedar.Experimental.Tests/DotWriterTests.cs` | DOT output format, identifier quoting, entity type clustering (~8 tests) |

### Files Summary
- **New source files**: 10
- **New test files**: 4
- **Key files**: `BatchAuthorization.cs`, `NodeEvaluation.cs`, `PartialEvaluation.cs`, `EntityGraphDotWriter.cs`

### Definition of Done
- `dotnet test cedar-dotnet.sln` passes across all projects
- Batch tests: **15+ tests**; Experimental tests: **28+ tests**
- Batch authorization produces same decisions as sequential authorization for all variable combinations
- DOT export produces valid Graphviz DOT with properly quoted identifiers
- Partial evaluation correctly identifies variable-dependent sub-expressions

### Risks
- Batch authorization performance scales with the product of variable domain sizes. Mitigated by documenting limits.
- Partial evaluation is complex and edge-case-prone. Mitigated by matching Go's implementation closely.
- Experimental APIs are harder to retract than to ship.

### Security
- Batch APIs never treat unresolved variables as allow.
- DOT export quotes all identifiers to prevent DOT injection.
- Partial evaluation preserves fail-safe semantics.

### Dependencies
- Sprint 7 completed (core engine + constant folding)
- Sprint 8 not required (schema is independent)

### Open Questions
1. Should batch authorization support `CancellationToken` from day one?
2. Should partial evaluation reuse the constant folder, or be a separate pass?

---

## Sprint 10: Benchmarks, Packaging, and Release Hardening

### Overview
Add performance benchmarks, NuGet packaging metadata, CI hardening, and final polish. This sprint validates performance parity with cedar-go and prepares the library for distribution.

### Use Cases
- Measure authorization throughput (decisions/second) for various policy complexity levels.
- Measure parse throughput (policies/second) for Cedar text and JSON formats.
- Measure type operation performance (entity lookup, set contains, record access).
- Package `Cedar`, `Cedar.Schema`, `Cedar.Batch`, and `Cedar.Experimental` as NuGet packages.
- Run full CI pipeline: build, test, benchmark, package.

### Architecture
- `benchmarks/Cedar.Benchmarks/` using BenchmarkDotNet.
- NuGet packaging metadata in `.csproj` files.
- CI pipeline definition (GitHub Actions or equivalent).

### Implementation

#### Phase 1: Benchmark infrastructure
| File | Purpose |
|------|---------|
| `benchmarks/Cedar.Benchmarks/Cedar.Benchmarks.csproj` | BenchmarkDotNet project |
| `benchmarks/Cedar.Benchmarks/AuthorizeBenchmarks.cs` | Throughput: simple policy, complex policy, many policies, large entity store |
| `benchmarks/Cedar.Benchmarks/ParseBenchmarks.cs` | Parse throughput: Cedar text, Cedar JSON, various policy sizes |
| `benchmarks/Cedar.Benchmarks/TypeBenchmarks.cs` | Entity lookup, set contains, record access, pattern matching, decimal operations |
| `benchmarks/Cedar.Benchmarks/EvalBenchmarks.cs` | Evaluator throughput: individual operators, constant-folded vs unfolded |

#### Phase 2: NuGet packaging
| File | Purpose |
|------|---------|
| Update `src/Cedar/Cedar.csproj` | PackageId, Version, Authors, Description, License, RepositoryUrl, PackageTags |
| Update `src/Cedar.Schema/Cedar.Schema.csproj` | Packaging metadata |
| Update `src/Cedar.Batch/Cedar.Batch.csproj` | Packaging metadata |
| Update `src/Cedar.Experimental/Cedar.Experimental.csproj` | Packaging metadata + pre-release version |

#### Phase 3: CI and release
| File | Purpose |
|------|---------|
| `.github/workflows/ci.yml` | Build, test, package on push/PR |
| `CLAUDE.md` | Repository-specific Claude Code instructions |

#### Phase 4: Integration smoke tests
| File | Purpose |
|------|---------|
| `tests/Cedar.Tests/Integration/EndToEndTests.cs` | Full pipeline: parse Cedar text → authorize → verify decision (~10 tests) |
| `tests/Cedar.Tests/Integration/CrossFormatTests.cs` | Cedar text → JSON → text round-trip with authorization (~8 tests) |

### Files Summary
- **New source files**: 4 benchmark + 2 CI/config + 4 csproj updates
- **New test files**: 2
- **Key files**: `AuthorizeBenchmarks.cs`, `ParseBenchmarks.cs`, `ci.yml`

### Definition of Done
- `dotnet test cedar-dotnet.sln` passes across ALL projects
- Total test count: **670+ tests** (592 prior + 41 corpus suite + 34 schema + 43 batch/experimental + 18 integration) across 55+ test files
- Benchmarks run without errors; baseline numbers documented
- NuGet packages build: `dotnet pack` succeeds
- CI pipeline: `dotnet restore && dotnet build && dotnet test && dotnet pack` all green
- `CLAUDE.md` documents project conventions for future Claude Code sessions

### Risks
- Benchmark results may reveal hot paths that need optimization. This sprint identifies them; optimization is a follow-up.
- NuGet version strategy needs agreement (SemVer, pre-release tagging).

### Security
- Benchmarks run out-of-process and never affect release dependencies.
- CI pipeline uses pinned action versions.
- NuGet packages include license and source link metadata.

### Dependencies
- All prior sprints completed
- BenchmarkDotNet (benchmark-only dependency)

### Open Questions
1. Should initial NuGet version be 0.1.0 (pre-release) or 1.0.0?
2. Should benchmarks compare against cedar-go numbers, or just establish a C# baseline?

---

## Sprint Summary Matrix

| Sprint | Focus | New Source Files | New Test Files | Cumulative Tests | Key Deliverable |
|--------|-------|-----------------|----------------|------------------|-----------------|
| 1 | Bootstrap + primitives | 12 | 5 | 34+ | Solution builds, primitive values work |
| 2 | Extended types + entities | 20 | 11 | 162+ | Full Cedar type system |
| 3 | AST nodes + fluent builder | 20 | 5 | 227+ | Programmatic policy construction |
| 4 | Tokenizer + parser + serializer | 10 | 6 | 317+ | Parse Cedar text ↔ AST |
| 5 | JSON serialization + policy APIs | 16 | 7 | 401+ | Cedar JSON ↔ AST, PolicySet |
| 6 | Evaluator + extensions + authorize | 22 | 6 | 551+ | **Core authorization works** |
| 7 | Constant folding + corpus + property tests | 2 | 5 | 592+ (+ corpus) | **Full parity validated** |
| 8 | Schema package | 7 | 4 | 626+ | Schema parsing |
| 9 | Batch + experimental + DOT | 10 | 4 | 669+ | Experimental surface parity |
| 10 | Benchmarks + packaging + CI | 6 | 2 | 687+ | Release-ready |

## End-State After Sprint 10

- **Core parity**: Authorize, Policy, PolicySet, PolicyList, all 12 Cedar value types, full parser, Cedar JSON, AST builder, constant folding, 23 extension functions, conformance corpus passing
- **Sidecar parity**: Schema parsing, batch authorization, partial evaluation, DOT export
- **Test coverage**: 687+ xUnit tests, 3+ FsCheck property suites, full conformance corpus, 20+ fuzz-seed tests, 4 benchmark classes
- **Every sprint ships**: compilable, testable, independently useful increment
- **Architecture**: single core assembly with internal namespaces; sidecar assemblies for experimental features; BCL-only for core engine; idiomatic C# throughout

## Critical Path

```
Sprint 1 → Sprint 2 → Sprint 3 → Sprint 4 → Sprint 5 → Sprint 6 → Sprint 7
                                                                    ↓
                                                              Sprint 8 (parallel-ok)
                                                              Sprint 9 (parallel-ok)
                                                                    ↓
                                                              Sprint 10
```

Sprints 8 and 9 can execute in parallel after Sprint 7. Sprint 10 requires all prior sprints.

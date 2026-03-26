# Cedar-DotNet Sprint Plan Intent: Semantic Port of cedar-go to C#

## Seed

Semantic port of cedar-go (Go Cedar authorization policy engine) to C# (.NET 9.0+). This is a foundational authorization engine that must be equally performant and well-tested. The port must achieve complete feature parity with the Go implementation, organized as a series of incremental sprints that each produce a compilable, testable deliverable.

## Context

- **Bare target repo** — cedar-dotnet has nothing but `.git/`; no conventions, no existing code, no CLAUDE.md. Everything must be scaffolded from scratch.
- **Source is mature** — cedar-go is a production Cedar engine with 249+ unit tests across 75 files, 4 fuzz tests, 1.5MB embedded conformance corpus, clean layered architecture (types/ → ast/ → internal/eval,parser,json).
- **Recent Go work** — EntityUID binary marshalling, extended `has` operator, trailing comma support, DOT graph export for EntityMap.
- **Minimal dependencies** — Go source only depends on `golang.org/x/exp`; C# port should similarly minimize NuGet dependencies (BCL-only where possible).
- **Known Go gaps** — No CLI, no schema validator, no formatter, no partial evaluation, no policy templates. C# port scope matches Go's actual shipped capabilities.

## Recent Sprint Context

No prior sprints — this is a greenfield project. The Go source at `github.com/strongdm/cedar-go` (fork of `cedar-policy/cedar-go`) is the sole reference implementation.

## Source Architecture (cedar-go)

### Package Layout
```
cedar-go/
├── authorize.go              # Main Authorize() entry point
├── policy.go                 # Policy struct & Cedar/JSON marshalling
├── policy_set.go             # PolicySet: named policy collection
├── policy_list.go            # PolicyList: unnamed policy sequence
├── stream.go                 # Stream processing for policies
├── types.go                  # Type re-exports and constructors
├── types/                    # 12 value types + Entity + supporting types
├── ast/                      # Public fluent AST builder API
├── internal/
│   ├── eval/                 # Evaluation engine (compile, fold, convert, 25+ evaluators)
│   ├── parser/               # Cedar tokenizer + recursive descent parser
│   ├── json/                 # JSON marshal/unmarshal for policies
│   ├── extensions/           # Extension function registry (25+ functions)
│   ├── mapset/               # Immutable set implementation
│   ├── consts/               # Constants (PARC variables, time units)
│   ├── schema/               # Schema parsing (human-readable + JSON)
│   ├── testutil/             # Test assertion helpers
│   └── rust/                 # Rust string unquoting compatibility
├── x/exp/
│   ├── ast/                  # Internal AST representation
│   ├── batch/                # Batch evaluation with partial evaluation
│   ├── eval/                 # Experimental evaluation features
│   ├── dot/                  # Graph visualization (DOT export)
│   └── schema/               # Schema validation (experimental)
└── corpus-tests.tar.gz       # 1.5MB conformance test corpus
```

### Core Type System
All Cedar values implement a `Value` interface with `Equal()`, `MarshalCedar()`, `String()`, and private `hash()`:
- **Primitives**: Boolean, Long, String
- **Extended**: Decimal (4-decimal fixed-point), Datetime (ms epoch), Duration (ms), IPAddr (IPv4/6 + CIDR)
- **Complex**: EntityUID (Type+ID), Entity (UID+Parents+Attributes+Tags), Record (immutable map), Set (immutable hash-set), Pattern (for `like` operator)
- **Supporting**: EntityType, EntityUIDSet, EntityMap, EntityGetter interface

### Authorization Flow
```
Authorize(policies, entities, request) → (Decision, Diagnostic)

For each policy:
  1. Compile: Policy → scope conditions + when/unless → BoolEvaler
  2. Evaluate: BoolEvaler.Eval(env{entities, principal, action, resource, context})
  3. Collect: permits[], forbids[], errors[]

Decision:
  - Any forbid → Deny (fail-safe)
  - Any permit (no forbids) → Allow
  - No matches → Deny (default-deny)
```

### Parser Pipeline
```
Cedar text → Tokenizer → Token[] → Recursive Descent Parser → AST → Compile → Evaluator tree
```

### Extension Functions (25+)
- Type constructors: `ip()`, `decimal()`, `datetime()`, `duration()`
- Decimal methods: `lessThan`, `lessThanOrEqual`, `greaterThan`, `greaterThanOrEqual`
- IP methods: `isIpv4`, `isIpv6`, `isLoopback`, `isMulticast`, `isInRange`
- Datetime methods: `toDate`, `toTime`, `offset`, `durationSince`
- Duration methods: `toDays`, `toHours`, `toMinutes`, `toSeconds`, `toMilliseconds`

### Test Infrastructure
- 249+ unit tests across 75 files
- 4 fuzz tests (tokenizer, parser, schema parser)
- 1.5MB embedded conformance corpus (from cedar-integration-tests)
- Table-driven test patterns throughout
- Roundtrip tests for all serialization paths
- Internal testutil package with typed assertions

## Proposed C# Architecture Mapping

| Go | C# |
|----|-----|
| `types/` package | `Cedar.Types` project — readonly record structs, abstract `Value` base |
| `ast/` package | `Cedar.Ast` project — fluent builder API, node types as abstract records |
| `internal/parser/` | `Cedar.Parser` project (internal) — tokenizer + recursive descent |
| `internal/eval/` | `Cedar.Eval` project (internal) — compiled evaluators, constant folding |
| `internal/json/` | `Cedar.Json` project (internal) or integrated into Cedar.Core |
| `internal/extensions/` | `Cedar.Eval` — extension registry as static dictionary |
| `internal/mapset/` | BCL `FrozenSet<T>` / `ImmutableHashSet<T>` |
| Root package (authorize.go, policy.go, etc.) | `Cedar.Core` project — public API facade |
| `x/exp/batch/` | `Cedar.Batch` project (experimental) |
| `x/exp/schema/` | `Cedar.Schema` project |
| Test files | `Cedar.Tests` project — xUnit with `[Theory]`/`[Fact]` |

### Key C# Design Decisions

1. **Value type hierarchy**: Abstract record class `Value` with sealed derived records (Boolean, Long, String, etc.) enabling pattern matching via `switch` expressions
2. **Immutability**: `readonly record struct` for small value types (EntityUID, Boolean, Long); `sealed record class` for reference types (Record, Set, Entity)
3. **Error handling**: `Result<T, CedarError>` for parse operations; exceptions only for programmer errors; evaluation errors collected in Diagnostic (matching Go semantics)
4. **Collections**: `ImmutableDictionary<CedarString, Value>` for Record; custom `ImmutableHashSet<Value>` for Set (needs custom equality)
5. **Serialization**: System.Text.Json with custom converters for Cedar JSON format
6. **Testing**: xUnit + FluentAssertions; FsCheck for property-based tests; embedded resource for conformance corpus

## Constraints

- .NET 9.0+ target framework
- Minimize NuGet dependencies — BCL-only for core engine; xUnit + FluentAssertions for tests
- Semantic port — idiomatic C# throughout, not line-for-line translation
- All Go tests must have C# equivalents; conformance corpus must pass
- Performance parity — compiled evaluators, constant folding, hash-based lookups
- `internal` access modifier for parser/eval/json — public API surface matches Go's exported types
- No CLI in scope (matches Go implementation's current state)

## Success Criteria

1. **Complete type system** — All 12 value types + Entity + supporting types with equality, hashing, Cedar/JSON serialization
2. **Full parser** — Tokenizer + recursive descent parser producing correct AST for all Cedar syntax
3. **Correct evaluator** — All 25+ evaluator types, constant folding, extension functions
4. **Authorization parity** — `Authorize()` produces identical decisions to Go for all test cases
5. **Test coverage** — 249+ unit test equivalents + conformance corpus passing + property-based tests
6. **Performance** — Benchmark suite demonstrating competitive evaluation throughput
7. **Clean API** — Fluent AST builder, PolicySet/PolicyList management, idiomatic C# public surface

## Verification Strategy

- **Reference implementation**: cedar-go test suite as ground truth — every test case ported
- **Conformance corpus**: Embedded corpus-tests.tar.gz extracted and run as xUnit theory data
- **Differential testing**: For complex scenarios, compare C# output against Go output
- **Roundtrip testing**: Cedar text → parse → marshal → compare for all policy formats
- **Property-based testing**: FsCheck for parser (arbitrary valid Cedar → parse → marshal → reparse)
- **Benchmarks**: BenchmarkDotNet for authorization throughput, parse speed, type operations

## Uncertainty Assessment

- **Correctness uncertainty: Low** — Reference Go implementation exists with comprehensive test suite; Cedar spec is well-defined
- **Scope uncertainty: Low** — Bounded by cedar-go's feature set; no ambiguity about what to port
- **Architecture uncertainty: Medium** — C# idioms differ significantly from Go (value types vs reference types, error handling, generics, pattern matching). Key decisions around Value hierarchy, Result vs exceptions, and immutable collection choices will shape the entire codebase.

## Open Questions

1. **Sprint granularity** — How many sprints? Should each sprint be independently useful (e.g., "Sprint 1 gives you types you can use") or purely incremental build-up?
2. **Value type hierarchy** — Abstract class with sealed records vs interface with structs? Trade-offs around boxing, pattern matching, and allocation.
3. **Error handling pattern** — Result<T> monad throughout vs exceptions at parse boundary + diagnostic collection at eval? Go uses `(T, error)` returns everywhere.
4. **Schema scope** — Include schema parsing (experimental in Go) or defer? The Go README lists validator as "not yet included."
5. **Batch evaluation scope** — Include x/exp/batch (partial evaluation) or defer to a future sprint series?
6. **Conformance corpus format** — Extract tar.gz at test time or pre-extract into test project?
7. **Project structure** — Single assembly (`Cedar`) with internal namespaces, or multi-project solution (`Cedar.Types`, `Cedar.Ast`, `Cedar.Core`, etc.)?
8. **Naming conventions** — Follow Cedar spec naming (e.g., `EntityUID`, `PolicySet`) or adapt to .NET conventions (e.g., `EntityUid`, `PolicyCollection`)?

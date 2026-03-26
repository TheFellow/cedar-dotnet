# Cedar-DotNet Multi-Sprint Plan (Codex Draft)

## Planning Baseline

- Reference implementation: `github.com/strongdm/cedar-go`
- Observed reference surface: 75 Go test files, 249 unit tests, 4 fuzz tests, embedded `corpus-tests.tar.gz`, core `cedar` package plus `ast`, `types`, `x/exp/batch`, `x/exp/schema`, `x/exp/dot`, and `x/exp/eval`
- Repository starting state: greenfield; only `docs/` exists in `cedar-dotnet`
- Runtime constraint: .NET 9.0+, BCL-only for engine projects where possible
- Test stack: `xunit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`; add `FsCheck.Xunit` only when parser/property coverage becomes useful; add `BenchmarkDotNet` only in the final benchmark sprint

## Proposed Solution Layout

```text
cedar-dotnet.sln
Directory.Build.props
Directory.Packages.props
src/
  Cedar/
    Cedar.csproj
    Authorization.cs
    Policy.cs
    PolicySet.cs
    PolicyList.cs
    Encoder.cs
    Decoder.cs
    Types/
    Ast/
    Internal/
      Parsing/
      Evaluation/
      Serialization/
  Cedar.Schema/
    Cedar.Schema.csproj
  Cedar.Batch/
    Cedar.Batch.csproj
  Cedar.Experimental/
    Cedar.Experimental.csproj
tests/
  Cedar.Tests/
  Cedar.Schema.Tests/
  Cedar.Batch.Tests/
  Cedar.Experimental.Tests/
benchmarks/
  Cedar.Benchmarks/
testdata/
  corpus/
    corpus-tests.tar.gz
```

## Series-Level Design Decisions

- Keep the core engine in a single `Cedar` assembly. Use internal namespaces, not extra assemblies, for parser/eval/serialization. That keeps the critical path shorter while still matching the Go package layering.
- Add sidecar assemblies only where the Go module exposes distinct public surface area: `Cedar.Schema`, `Cedar.Batch`, and `Cedar.Experimental`.
- Preserve Cedar domain names, but use idiomatic .NET type casing for acronyms: `EntityUid`, `PolicySet`, `Authorize`, `SchemaDocument`.
- Model Cedar values as immutable sealed records under a common `CedarValue` base class. This is less allocation-efficient than a hand-built tagged union, but it is far simpler to land incrementally and easier to verify against the Go reference. If performance proves insufficient, optimize behind the same public API after Sprint 6.
- Reach full core authorization parity first. Experimental parity follows after the main authorizer, parser, JSON, and corpus suite are stable.

## Sprint 1: Repository Bootstrap and Core Contracts

### Overview

Create the initial .NET solution, package structure, and the smallest possible Cedar runtime surface: primitive scalar values, diagnostics, source positions, and test infrastructure. This sprint does not authorize requests yet, but it establishes the naming, packaging, and immutability rules that all later sprints build on.

### Use Cases

- A consumer can reference `Cedar.csproj` and construct `CedarBoolean`, `CedarLong`, and `CedarString` values.
- A consumer can create `Decision`, `Effect`, `Position`, `Diagnostic`, and `PolicyId` instances.
- CI can restore, build, and test the new solution from a blank clone.

### Architecture

- Create `cedar-dotnet.sln` with one runtime project and one test project.
- Place public contracts in `src/Cedar/`.
- Place future internal seams under `src/Cedar/Internal/` even if some folders are empty in this sprint.
- Introduce a shared assertion helper layer in `tests/Cedar.Tests/TestSupport/` mirroring the role of Go `internal/testutil`.

### Implementation

#### Phase 1: Solution and build scaffolding

- Add `cedar-dotnet.sln`
- Add `Directory.Build.props` with nullable enabled, implicit usings disabled, deterministic builds enabled, and warnings-as-errors for engine projects
- Add `Directory.Packages.props` for central test package versions
- Add `src/Cedar/Cedar.csproj`
- Add `tests/Cedar.Tests/Cedar.Tests.csproj`

#### Phase 2: Core contracts

- Add `src/Cedar/CedarValue.cs`
- Add `src/Cedar/Decision.cs`
- Add `src/Cedar/Effect.cs`
- Add `src/Cedar/PolicyId.cs`
- Add `src/Cedar/Position.cs`
- Add `src/Cedar/Diagnostic.cs`
- Add `src/Cedar/DiagnosticReason.cs`
- Add `src/Cedar/DiagnosticError.cs`

#### Phase 3: Primitive Cedar values

- Add `src/Cedar/Types/CedarBoolean.cs`
- Add `src/Cedar/Types/CedarLong.cs`
- Add `src/Cedar/Types/CedarString.cs`
- Add `src/Cedar/Types/CedarTypeNames.cs`
- Implement equality, stable hashing, `ToString()`, and Cedar literal rendering for these types

#### Phase 4: Test harness

- Add `tests/Cedar.Tests/TestSupport/CedarAssert.cs`
- Add `tests/Cedar.Tests/TestSupport/JsonAssert.cs`
- Add `tests/Cedar.Tests/TestSupport/GoldenFile.cs`
- Add smoke tests that validate basic construction and serialization behavior

### Files Summary

- `cedar-dotnet.sln`
- `Directory.Build.props`
- `Directory.Packages.props`
- `src/Cedar/Cedar.csproj`
- `src/Cedar/CedarValue.cs`
- `src/Cedar/Decision.cs`
- `src/Cedar/Diagnostic.cs`
- `src/Cedar/Types/CedarBoolean.cs`
- `src/Cedar/Types/CedarLong.cs`
- `src/Cedar/Types/CedarString.cs`
- `tests/Cedar.Tests/Cedar.Tests.csproj`
- `tests/Cedar.Tests/TestSupport/CedarAssert.cs`

### Definition of Done

- `dotnet build cedar-dotnet.sln` succeeds on a clean machine
- `dotnet test tests/Cedar.Tests/Cedar.Tests.csproj` succeeds
- Minimum automated coverage: 32 tests across 8 test files
- A README-quality sample can construct primitive Cedar values without reflection or dynamic typing

### Risks

- The initial `CedarValue` abstraction may become a hot path later
- Early naming choices can create avoidable churn if they diverge from desired public API conventions

### Security

- All types are immutable
- Hash code implementations are deterministic and do not depend on process-randomized ordering
- Diagnostic and string rendering APIs never emit unescaped Cedar string literals

### Dependencies

- .NET 9 SDK
- `xunit`
- `Microsoft.NET.Test.Sdk`
- `coverlet.collector`

### Open Questions

- Should `PolicyId` be a dedicated readonly record or a strong typedef over `string`?
- Should primitive Cedar values expose implicit conversions, or should construction stay explicit until the full API settles?

## Sprint 2: Full Value System, Entities, and Type Serialization

### Overview

Port the complete Cedar value layer: extended scalar types, collection types, entities, entity graphs, and JSON/binary serialization. By the end of this sprint, the library can model requests and entity data exactly, even though policy parsing and evaluation are still incomplete.

### Use Cases

- A consumer can build `EntityUid`, `Entity`, `EntityMap`, `CedarRecord`, and `CedarSet`
- A consumer can round-trip entity JSON from Cedar integration test data
- A consumer can parse and serialize extended values: decimal, datetime, duration, and IP/CIDR
- `EntityUid` supports binary round-trip equivalent to the recent Go addition

### Architecture

- Keep all runtime types in `src/Cedar/Types/`
- Introduce internal JSON converter infrastructure in `src/Cedar/Internal/Serialization/`
- Use read-only collection wrappers with stable serialization ordering

### Implementation

#### Phase 1: Extended scalar types

- Add `src/Cedar/Types/CedarDecimal.cs`
- Add `src/Cedar/Types/CedarDatetime.cs`
- Add `src/Cedar/Types/CedarDuration.cs`
- Add `src/Cedar/Types/CedarIpAddress.cs`
- Add `src/Cedar/Types/CedarPattern.cs`
- Implement parse helpers and overflow/format validation

#### Phase 2: Entity and collection types

- Add `src/Cedar/Types/EntityUid.cs`
- Add `src/Cedar/Types/Entity.cs`
- Add `src/Cedar/Types/EntityMap.cs`
- Add `src/Cedar/Types/EntityUidSet.cs`
- Add `src/Cedar/Types/CedarRecord.cs`
- Add `src/Cedar/Types/CedarRecordMap.cs`
- Add `src/Cedar/Types/CedarSet.cs`
- Add `src/Cedar/Request.cs`

#### Phase 3: Serialization

- Add `src/Cedar/Internal/Serialization/CedarValueJsonConverter.cs`
- Add `src/Cedar/Internal/Serialization/EntityUidJsonConverter.cs`
- Add `src/Cedar/Internal/Serialization/EntityJsonConverter.cs`
- Add `src/Cedar/Internal/Serialization/RecordJsonConverter.cs`
- Add `src/Cedar/Internal/Serialization/SetJsonConverter.cs`
- Add `src/Cedar/Internal/Serialization/EntityUidBinaryCodec.cs`

#### Phase 4: Type parity tests

- Add type-focused test classes such as `CedarDecimalTests`, `CedarDatetimeTests`, `CedarDurationTests`, `CedarIpAddressTests`, `EntityUidTests`, `EntityTests`, `EntityMapTests`, `CedarRecordTests`, and `CedarSetTests`

### Files Summary

- `src/Cedar/Types/CedarDecimal.cs`
- `src/Cedar/Types/CedarDatetime.cs`
- `src/Cedar/Types/CedarDuration.cs`
- `src/Cedar/Types/CedarIpAddress.cs`
- `src/Cedar/Types/CedarPattern.cs`
- `src/Cedar/Types/EntityUid.cs`
- `src/Cedar/Types/Entity.cs`
- `src/Cedar/Types/EntityMap.cs`
- `src/Cedar/Types/CedarRecord.cs`
- `src/Cedar/Types/CedarSet.cs`
- `src/Cedar/Request.cs`
- `src/Cedar/Internal/Serialization/CedarValueJsonConverter.cs`
- `src/Cedar/Internal/Serialization/EntityUidBinaryCodec.cs`
- `tests/Cedar.Tests/Types/EntityUidTests.cs`

### Definition of Done

- `dotnet test tests/Cedar.Tests/Cedar.Tests.csproj` succeeds
- Minimum automated coverage: 46 tests across 12 test files
- Entity JSON fixtures from cedar-go style samples round-trip without semantic loss
- `EntityUid` binary and Cedar text round-trips both pass

### Risks

- `System.Text.Json` converter design can become difficult to change once `Policy` JSON is layered on top
- Decimal precision and overflow semantics must match Go exactly, or later evaluator behavior will drift

### Security

- Reject malformed extension JSON and malformed implicit/explicit entity JSON
- Reject invalid CIDR prefixes and invalid datetime/duration text during parse
- Keep set and record construction immutable to avoid caller mutation after authorization starts

### Dependencies

- Sprint 1 completed
- BCL networking/time APIs validated against Cedar semantics

### Open Questions

- Should `CedarPattern` eagerly compile wildcard segments or preserve raw text until evaluation?
- Should record keys be `string` or `CedarString` internally?

## Sprint 3: Public AST Builder and Tokenizer Foundation

### Overview

Build the internal AST model and public builder API, then land the tokenizer and pattern parser. This gives the port a stable syntax tree shape before the full recursive-descent policy parser is added.

### Use Cases

- A consumer can programmatically construct policies and expressions via `Cedar.Ast`
- A consumer can tokenize Cedar policy text and inspect source positions
- The team can write parser tests against tokens and AST nodes before evaluation exists

### Architecture

- Add `Cedar.Ast` namespace inside `src/Cedar/Ast/`
- Separate public builder wrappers from the internal node hierarchy
- Add tokenizer under `src/Cedar/Internal/Parsing/`

### Implementation

#### Phase 1: Internal AST nodes

- Add `src/Cedar/Ast/AstNode.cs`
- Add `src/Cedar/Ast/BinaryNodes.cs`
- Add `src/Cedar/Ast/UnaryNodes.cs`
- Add `src/Cedar/Ast/ValueNode.cs`
- Add `src/Cedar/Ast/RecordNode.cs`
- Add `src/Cedar/Ast/SetNode.cs`
- Add `src/Cedar/Ast/VariableNode.cs`
- Add `src/Cedar/Ast/ScopeConstraint.cs`
- Add `src/Cedar/Ast/PolicyNode.cs`

#### Phase 2: Public builders

- Add `src/Cedar/Ast/CedarAst.cs`
- Add `src/Cedar/Ast/PolicyBuilder.cs`
- Add `src/Cedar/Ast/NodeBuilderExtensions.cs`
- Add `src/Cedar/Ast/Annotation.cs`
- Expose methods equivalent to the Go builder surface for equality, arithmetic, collection, `like`, `is`, tag, and extension-call expression construction

#### Phase 3: Tokenizer

- Add `src/Cedar/Internal/Parsing/CedarTokenType.cs`
- Add `src/Cedar/Internal/Parsing/CedarToken.cs`
- Add `src/Cedar/Internal/Parsing/CedarTokenizer.cs`
- Add `src/Cedar/Internal/Parsing/CedarTextReader.cs`
- Add `src/Cedar/Internal/Parsing/PatternTokenizer.cs`

#### Phase 4: AST and token tests

- Add `tests/Cedar.Tests/Ast/PolicyBuilderTests.cs`
- Add `tests/Cedar.Tests/Ast/NodeBuilderTests.cs`
- Add `tests/Cedar.Tests/Parsing/CedarTokenizerTests.cs`
- Add `tests/Cedar.Tests/Parsing/PatternTokenizerTests.cs`

### Files Summary

- `src/Cedar/Ast/AstNode.cs`
- `src/Cedar/Ast/PolicyNode.cs`
- `src/Cedar/Ast/ScopeConstraint.cs`
- `src/Cedar/Ast/PolicyBuilder.cs`
- `src/Cedar/Ast/NodeBuilderExtensions.cs`
- `src/Cedar/Internal/Parsing/CedarTokenType.cs`
- `src/Cedar/Internal/Parsing/CedarToken.cs`
- `src/Cedar/Internal/Parsing/CedarTokenizer.cs`
- `src/Cedar/Internal/Parsing/PatternTokenizer.cs`
- `tests/Cedar.Tests/Parsing/CedarTokenizerTests.cs`

### Definition of Done

- `dotnet test tests/Cedar.Tests/Cedar.Tests.csproj` succeeds
- Minimum automated coverage: 52 tests across 10 test files
- Tokenizer handles Cedar reserved keywords, string escaping, integer literals, comments, and source positions
- AST builder can reproduce the README authorization example as an object graph

### Risks

- If AST node naming is too Go-shaped, the C# API will feel unnatural
- If the tokenizer over-specializes for current tests, the recursive-descent parser will become brittle

### Security

- Reject invalid UTF-8 or invalid escape sequences during lexing
- Enforce token-length and error-count ceilings so hostile inputs do not create unbounded parser work

### Dependencies

- Sprint 2 completed

### Open Questions

- Should public AST nodes be serializable, or remain pure builder/runtime objects?
- Should tokenizer expose `ReadOnlySpan<char>`-based APIs immediately, or wait until performance tuning sprint?

## Sprint 4: Policy Parsing, Policy Containers, and Cedar/JSON Round-Trips

### Overview

Implement the full policy parser, Cedar text writer, JSON writer/reader, and public policy container APIs. After this sprint, the library can parse, store, stream, and round-trip policies, but evaluation is still limited to smoke coverage.

### Use Cases

- Parse a Cedar policy file into `PolicyList` or `PolicySet`
- Serialize policies to Cedar or Cedar JSON
- Stream policies through `Encoder` and `Decoder`
- Attach source filenames and positions to parsed policies

### Architecture

- Keep parser and serializer internal to `Cedar`
- Expose only `Policy`, `PolicyList`, `PolicySet`, `Encoder`, and `Decoder`
- Use dedicated JSON DTO classes rather than serializing AST nodes directly

### Implementation

#### Phase 1: Recursive-descent parser

- Add `src/Cedar/Internal/Parsing/CedarParser.cs`
- Add `src/Cedar/Internal/Parsing/PolicyParser.cs`
- Add `src/Cedar/Internal/Parsing/ScopeParser.cs`
- Add `src/Cedar/Internal/Parsing/NodeParser.cs`
- Add support for recent Go parity points, including trailing commas

#### Phase 2: Cedar text writing

- Add `src/Cedar/Internal/Parsing/CedarSyntaxWriter.cs`
- Add `src/Cedar/Internal/Parsing/CedarEncoder.cs`
- Add `src/Cedar/Internal/Parsing/CedarDecoder.cs`

#### Phase 3: JSON policy serialization

- Add `src/Cedar/Internal/Serialization/PolicyJsonModel.cs`
- Add `src/Cedar/Internal/Serialization/PolicyJsonConverter.cs`
- Add `src/Cedar/Internal/Serialization/NodeJsonConverter.cs`

#### Phase 4: Public policy API

- Add `src/Cedar/Policy.cs`
- Add `src/Cedar/PolicySet.cs`
- Add `src/Cedar/PolicyList.cs`
- Add `src/Cedar/Encoder.cs`
- Add `src/Cedar/Decoder.cs`

### Files Summary

- `src/Cedar/Internal/Parsing/CedarParser.cs`
- `src/Cedar/Internal/Parsing/PolicyParser.cs`
- `src/Cedar/Internal/Parsing/NodeParser.cs`
- `src/Cedar/Internal/Parsing/CedarSyntaxWriter.cs`
- `src/Cedar/Internal/Serialization/PolicyJsonModel.cs`
- `src/Cedar/Internal/Serialization/PolicyJsonConverter.cs`
- `src/Cedar/Policy.cs`
- `src/Cedar/PolicySet.cs`
- `src/Cedar/PolicyList.cs`
- `src/Cedar/Encoder.cs`
- `src/Cedar/Decoder.cs`
- `tests/Cedar.Tests/Policies/PolicyRoundTripTests.cs`

### Definition of Done

- `dotnet test tests/Cedar.Tests/Cedar.Tests.csproj` succeeds
- Minimum automated coverage: 38 tests across 9 test files
- Cedar text to AST to Cedar text round-trips for representative policies
- Cedar JSON to AST to Cedar JSON round-trips for representative policies
- `PolicySet` supports add, replace, remove, get, and deterministic Cedar serialization order

### Risks

- Parser precedence bugs will be hard to unwind if evaluator work begins before round-trip coverage is strong
- JSON DTO shape must match Cedar JSON exactly or the corpus work will fail later

### Security

- Parser reports bounded, source-positioned errors without exposing partial internal state
- Streaming decoder rejects malformed policy boundaries instead of silently skipping content

### Dependencies

- Sprint 3 completed

### Open Questions

- Should `PolicySet` preserve insertion order internally, or sort only on serialization like cedar-go?
- Should `Policy` cache its compiled evaluator immediately, or only after Sprint 5 lands?

## Sprint 5: Core Authorization Engine

### Overview

Port the first complete authorization path: compile a policy into evaluator nodes, evaluate PARC requests, and emit allow/deny decisions plus diagnostics. This sprint targets the core operator set first, leaving extension functions, tags, and corpus parity for the next sprint.

### Use Cases

- Authorize a request against permit/forbid policies
- Evaluate scope constraints on principal, action, and resource
- Evaluate boolean, comparison, arithmetic, collection, record access, `in`, `has`, and `if-then-else` expressions
- Collect policy-level diagnostic reasons and errors

### Architecture

- Compiler and evaluator live under `src/Cedar/Internal/Evaluation/`
- Public entry point is `Authorization.Authorize(PolicySet, EntityMap, Request)`
- Keep evaluator nodes small and specialized instead of one giant switch-based interpreter

### Implementation

#### Phase 1: Evaluation environment and compiler

- Add `src/Cedar/Internal/Evaluation/EvaluationEnvironment.cs`
- Add `src/Cedar/Internal/Evaluation/PolicyCompiler.cs`
- Add `src/Cedar/Internal/Evaluation/NodeCompiler.cs`
- Add `src/Cedar/Internal/Evaluation/TypeConversion.cs`

#### Phase 2: Core evaluators

- Add `src/Cedar/Internal/Evaluation/Evaluators/LiteralEvaluator.cs`
- Add `src/Cedar/Internal/Evaluation/Evaluators/VariableEvaluator.cs`
- Add `src/Cedar/Internal/Evaluation/Evaluators/BooleanEvaluators.cs`
- Add `src/Cedar/Internal/Evaluation/Evaluators/ComparisonEvaluators.cs`
- Add `src/Cedar/Internal/Evaluation/Evaluators/ArithmeticEvaluators.cs`
- Add `src/Cedar/Internal/Evaluation/Evaluators/CollectionEvaluators.cs`
- Add `src/Cedar/Internal/Evaluation/Evaluators/RecordEvaluators.cs`
- Add `src/Cedar/Internal/Evaluation/Evaluators/ScopeEvaluators.cs`

#### Phase 3: Public authorizer

- Add `src/Cedar/Authorization.cs`
- Add evaluator caching on `Policy`
- Add `PolicyIterator` equivalent by making `PolicySet` and later `PolicyList` enumerable via a common interface or iterator contract

#### Phase 4: Core authorization tests

- Add `tests/Cedar.Tests/Authorization/AuthorizeTests.cs`
- Add `tests/Cedar.Tests/Authorization/DiagnosticTests.cs`
- Add `tests/Cedar.Tests/Evaluation/CompilerTests.cs`
- Add `tests/Cedar.Tests/Evaluation/ConversionTests.cs`

### Files Summary

- `src/Cedar/Internal/Evaluation/EvaluationEnvironment.cs`
- `src/Cedar/Internal/Evaluation/PolicyCompiler.cs`
- `src/Cedar/Internal/Evaluation/NodeCompiler.cs`
- `src/Cedar/Internal/Evaluation/TypeConversion.cs`
- `src/Cedar/Internal/Evaluation/Evaluators/BooleanEvaluators.cs`
- `src/Cedar/Internal/Evaluation/Evaluators/ComparisonEvaluators.cs`
- `src/Cedar/Internal/Evaluation/Evaluators/ArithmeticEvaluators.cs`
- `src/Cedar/Internal/Evaluation/Evaluators/CollectionEvaluators.cs`
- `src/Cedar/Internal/Evaluation/Evaluators/ScopeEvaluators.cs`
- `src/Cedar/Authorization.cs`
- `tests/Cedar.Tests/Authorization/AuthorizeTests.cs`

### Definition of Done

- `dotnet test tests/Cedar.Tests/Cedar.Tests.csproj` succeeds
- Minimum automated coverage: 47 tests across 10 test files
- README-style examples authorize correctly
- Permit/forbid/default-deny semantics match cedar-go on targeted parity fixtures
- Diagnostics capture matched policies and evaluation errors without aborting the full authorization pass

### Risks

- A switch-heavy compiler may be simpler initially but can become difficult to profile and optimize
- Type conversion semantics are easy to get subtly wrong across `long`, `decimal`, `datetime`, and entity values

### Security

- Authorization remains fail-safe: any forbid wins, no-match denies
- Type errors surface as diagnostics, not process crashes
- Missing entities are handled as evaluation errors, never implicit allow

### Dependencies

- Sprint 4 completed

### Open Questions

- Should `Authorization.Authorize` accept `IEnumerable<KeyValuePair<PolicyId, Policy>>` or a dedicated `IPolicyIterator`?
- Is it worth introducing evaluator object pooling now, or only after corpus and benchmark data exists?

## Sprint 6: Full Authorizer Parity, Constant Folding, Extensions, and Corpus

### Overview

Finish the main engine. This sprint closes the remaining evaluation gaps: extension functions, `like`, `is`, `is in`, tag operators, constant folding, partial evaluation primitives needed by later experimental work, and full conformance-corpus coverage. This is the sprint that delivers the full core authorization engine.

### Use Cases

- Evaluate all Cedar operators shipped by cedar-go, including tags and extension methods
- Evaluate constant expressions once at compile time
- Execute the embedded integration corpus and produce the same decisions and diagnostics as cedar-go
- Support newly added Go behavior such as the extended `has` operator and trailing comma parsing

### Architecture

- Keep extension logic isolated in an internal registry
- Keep constant folding separate from normal evaluation so later batch/partial work can reuse the same nodes
- Add corpus and differential harnesses to tests, not runtime code

### Implementation

#### Phase 1: Remaining core evaluators

- Add `src/Cedar/Internal/Evaluation/Evaluators/TagEvaluators.cs`
- Add `src/Cedar/Internal/Evaluation/Evaluators/PatternEvaluators.cs`
- Add `src/Cedar/Internal/Evaluation/Evaluators/ExtensionEvaluator.cs`
- Add `src/Cedar/Internal/Evaluation/ComparableValue.cs`

#### Phase 2: Compile-time folding and partial primitives

- Add `src/Cedar/Internal/Evaluation/ConstantFolder.cs`
- Add `src/Cedar/Internal/Evaluation/PartialEvaluator.cs`
- Add `src/Cedar/Internal/Evaluation/PartialValue.cs`

#### Phase 3: Extension function registry

- Add `src/Cedar/Internal/Evaluation/Extensions/ExtensionRegistry.cs`
- Add `src/Cedar/Internal/Evaluation/Extensions/DecimalExtensions.cs`
- Add `src/Cedar/Internal/Evaluation/Extensions/IpAddressExtensions.cs`
- Add `src/Cedar/Internal/Evaluation/Extensions/DatetimeExtensions.cs`
- Add `src/Cedar/Internal/Evaluation/Extensions/DurationExtensions.cs`

#### Phase 4: Conformance and parity testing

- Add `testdata/corpus/corpus-tests.tar.gz`
- Add `tests/Cedar.Tests/Corpus/CorpusTests.cs`
- Add `tests/Cedar.Tests/Evaluation/ConstantFolderTests.cs`
- Add `tests/Cedar.Tests/Evaluation/ExtensionTests.cs`
- Add `tests/Cedar.Tests/Evaluation/TagOperatorTests.cs`
- Add a small differential test harness that can compare selected C# results against local `cedar-go` outputs when available

### Files Summary

- `src/Cedar/Internal/Evaluation/Evaluators/TagEvaluators.cs`
- `src/Cedar/Internal/Evaluation/Evaluators/PatternEvaluators.cs`
- `src/Cedar/Internal/Evaluation/Evaluators/ExtensionEvaluator.cs`
- `src/Cedar/Internal/Evaluation/ConstantFolder.cs`
- `src/Cedar/Internal/Evaluation/PartialEvaluator.cs`
- `src/Cedar/Internal/Evaluation/Extensions/ExtensionRegistry.cs`
- `src/Cedar/Internal/Evaluation/Extensions/DecimalExtensions.cs`
- `src/Cedar/Internal/Evaluation/Extensions/IpAddressExtensions.cs`
- `src/Cedar/Internal/Evaluation/Extensions/DatetimeExtensions.cs`
- `src/Cedar/Internal/Evaluation/Extensions/DurationExtensions.cs`
- `testdata/corpus/corpus-tests.tar.gz`
- `tests/Cedar.Tests/Corpus/CorpusTests.cs`

### Definition of Done

- `dotnet test tests/Cedar.Tests/Cedar.Tests.csproj` succeeds
- Minimum automated coverage: 62 tests across 14 test files, plus a full corpus theory suite
- Core public API parity is reached for `Authorize`, `Policy`, `PolicySet`, `PolicyList`, `Encoder`, `Decoder`, `ast`, and all Cedar types
- Corpus results match cedar-go decisions, reasons, and error policy IDs for the embedded suite
- Constant folding is enabled for compile-time-safe expressions only

### Risks

- Extension functions are a major semantic trap, especially around type mismatch behavior
- Corpus failures may expose issues in parser, serialization, evaluator, or diagnostics simultaneously

### Security

- Extension functions perform strict arity and type checks
- Constant folding never evaluates expressions that depend on request data or entity graph state
- Corpus inputs are treated as untrusted; decompression and archive traversal stay fully in-memory and path-safe

### Dependencies

- Sprint 5 completed
- Access to the cedar-go corpus archive for parity fixtures

### Open Questions

- Should the partial-evaluation internals remain fully internal until Sprint 8, or should a small experimental hook land here?
- Do we want a strict “no new allocations in hot evaluator paths” pass before leaving this sprint?

## Sprint 7: Schema Package Parity and Parser Hardening

### Overview

Port the human-readable and JSON schema package exposed by cedar-go without pulling in full validation semantics that cedar-go does not ship. This sprint also hardens parser behavior with property-based and fuzz-equivalent testing.

### Use Cases

- Parse Cedar schema text into a `SchemaDocument`
- Convert schema text to Cedar JSON and back
- Preserve comments, ordering, and formatting-relevant trivia well enough to re-emit human-readable schema
- Use schema parsing in integration tests that consume the shared corpus

### Architecture

- Create a separate `Cedar.Schema` assembly to mirror the public split in the Go module
- Keep schema tokenization/parsing/formatting internal to `Cedar.Schema`
- Reference core Cedar types only where needed

### Implementation

#### Phase 1: Schema project setup

- Add `src/Cedar.Schema/Cedar.Schema.csproj`
- Add `tests/Cedar.Schema.Tests/Cedar.Schema.Tests.csproj`

#### Phase 2: Schema AST and parser

- Add `src/Cedar.Schema/SchemaDocument.cs`
- Add `src/Cedar.Schema/Internal/Ast/SchemaNode.cs`
- Add `src/Cedar.Schema/Internal/Ast/NamespaceNode.cs`
- Add `src/Cedar.Schema/Internal/Ast/EntityDeclarationNode.cs`
- Add `src/Cedar.Schema/Internal/Ast/ActionDeclarationNode.cs`
- Add `src/Cedar.Schema/Internal/Parsing/SchemaTokenType.cs`
- Add `src/Cedar.Schema/Internal/Parsing/SchemaLexer.cs`
- Add `src/Cedar.Schema/Internal/Parsing/SchemaParser.cs`

#### Phase 3: Schema formatting and JSON conversion

- Add `src/Cedar.Schema/Internal/Formatting/SchemaFormatter.cs`
- Add `src/Cedar.Schema/Internal/Serialization/SchemaJsonConverter.cs`
- Add `src/Cedar.Schema/Internal/Conversion/HumanToJsonSchemaConverter.cs`
- Add `src/Cedar.Schema/Internal/Conversion/JsonToHumanSchemaConverter.cs`

#### Phase 4: Hardening tests

- Add `tests/Cedar.Schema.Tests/SchemaParserTests.cs`
- Add `tests/Cedar.Schema.Tests/SchemaFormatterTests.cs`
- Add `tests/Cedar.Schema.Tests/SchemaJsonRoundTripTests.cs`
- Add `tests/Cedar.Schema.Tests/SchemaPropertyTests.cs`
- Add `tests/Cedar.Schema.Tests/SchemaFuzzSeedsTests.cs`

### Files Summary

- `src/Cedar.Schema/Cedar.Schema.csproj`
- `src/Cedar.Schema/SchemaDocument.cs`
- `src/Cedar.Schema/Internal/Parsing/SchemaTokenType.cs`
- `src/Cedar.Schema/Internal/Parsing/SchemaLexer.cs`
- `src/Cedar.Schema/Internal/Parsing/SchemaParser.cs`
- `src/Cedar.Schema/Internal/Formatting/SchemaFormatter.cs`
- `src/Cedar.Schema/Internal/Serialization/SchemaJsonConverter.cs`
- `src/Cedar.Schema/Internal/Conversion/HumanToJsonSchemaConverter.cs`
- `tests/Cedar.Schema.Tests/Cedar.Schema.Tests.csproj`
- `tests/Cedar.Schema.Tests/SchemaParserTests.cs`

### Definition of Done

- `dotnet test tests/Cedar.Schema.Tests/Cedar.Schema.Tests.csproj` succeeds
- Minimum automated coverage: 26 tests across 7 test files, plus 2 property/fuzz-equivalent suites
- `SchemaDocument` supports Cedar text input, Cedar text output, JSON input, and JSON output
- Comments and ordering are preserved well enough to support formatter-style re-emission
- The package does not claim validator support beyond cedar-go’s current scope

### Risks

- Trivia preservation can bloat the AST if not designed carefully
- Schema JSON conversion bugs will be hard to spot without strong golden coverage

### Security

- Parser error accumulation is bounded
- Schema formatting never executes user-supplied content; it only re-emits parsed AST trivia
- No validation claims beyond implemented semantics

### Dependencies

- Sprint 6 completed

### Open Questions

- Should schema comments be preserved in full-fidelity nodes or as detached trivia lists?
- Should `SchemaDocument` live under namespace `Cedar.Schema` or `Cedar.Experimental.Schema`?

## Sprint 8: Experimental Surface Parity, Benchmarks, and Release Hardening

### Overview

Close the remaining public surface exposed by cedar-go: batch authorization, experimental AST/node evaluation helpers, DOT export for entity graphs, packaging, and performance/regression benchmarks. After this sprint, the C# port has full practical feature parity with the current Go codebase.

### Use Cases

- Run batch authorization with variables and ignored request parts
- Evaluate a standalone AST node in an environment
- Partially evaluate a policy for downstream query planning
- Export an `EntityMap` relationship graph as DOT
- Run benchmarks for parse throughput and authorization throughput

### Architecture

- Create sidecar assemblies `Cedar.Batch` and `Cedar.Experimental`
- Keep the core `Cedar` runtime unchanged except for clearly bounded experimental hooks reused by batch and experimental evaluation
- Add benchmarks in a separate project so production consumers do not pull test-only dependencies

### Implementation

#### Phase 1: Batch authorization package

- Add `src/Cedar.Batch/Cedar.Batch.csproj`
- Add `src/Cedar.Batch/BatchAuthorization.cs`
- Add `src/Cedar.Batch/BatchRequest.cs`
- Add `src/Cedar.Batch/BatchResult.cs`
- Add `src/Cedar.Batch/BatchVariables.cs`

#### Phase 2: Experimental evaluation and DOT export

- Add `src/Cedar.Experimental/Cedar.Experimental.csproj`
- Add `src/Cedar.Experimental/NodeEvaluation.cs`
- Add `src/Cedar.Experimental/PartialPolicyEvaluation.cs`
- Add `src/Cedar.Experimental/VariableValue.cs`
- Add `src/Cedar.Experimental/EntityGraphDotWriter.cs`

#### Phase 3: Tests

- Add `tests/Cedar.Batch.Tests/Cedar.Batch.Tests.csproj`
- Add `tests/Cedar.Batch.Tests/BatchAuthorizationTests.cs`
- Add `tests/Cedar.Batch.Tests/VariableSubstitutionTests.cs`
- Add `tests/Cedar.Experimental.Tests/Cedar.Experimental.Tests.csproj`
- Add `tests/Cedar.Experimental.Tests/NodeEvaluationTests.cs`
- Add `tests/Cedar.Experimental.Tests/EntityGraphDotWriterTests.cs`

#### Phase 4: Benchmarking and packaging

- Add `benchmarks/Cedar.Benchmarks/Cedar.Benchmarks.csproj`
- Add `benchmarks/Cedar.Benchmarks/AuthorizeBenchmarks.cs`
- Add `benchmarks/Cedar.Benchmarks/ParseBenchmarks.cs`
- Add `benchmarks/Cedar.Benchmarks/EntityMapBenchmarks.cs`
- Add NuGet packaging metadata and solution-wide CI targets

### Files Summary

- `src/Cedar.Batch/Cedar.Batch.csproj`
- `src/Cedar.Batch/BatchAuthorization.cs`
- `src/Cedar.Batch/BatchRequest.cs`
- `src/Cedar.Batch/BatchResult.cs`
- `src/Cedar.Experimental/Cedar.Experimental.csproj`
- `src/Cedar.Experimental/NodeEvaluation.cs`
- `src/Cedar.Experimental/PartialPolicyEvaluation.cs`
- `src/Cedar.Experimental/EntityGraphDotWriter.cs`
- `tests/Cedar.Batch.Tests/BatchAuthorizationTests.cs`
- `tests/Cedar.Experimental.Tests/NodeEvaluationTests.cs`
- `benchmarks/Cedar.Benchmarks/AuthorizeBenchmarks.cs`

### Definition of Done

- `dotnet test cedar-dotnet.sln` succeeds across all projects
- Minimum automated coverage: 24 tests across 6 test files, plus 3 benchmark classes
- `Cedar.Batch` reaches semantic parity with the cedar-go `x/exp/batch` package for supported scenarios
- `Cedar.Experimental` exposes node evaluation, partial-policy helpers, and DOT export
- NuGet packages can be built for `Cedar`, `Cedar.Schema`, `Cedar.Batch`, and `Cedar.Experimental`

### Risks

- Batch authorization can magnify inefficiencies in partial evaluation very quickly
- Experimental APIs are easier to ship than to retract, so naming and namespace boundaries matter

### Security

- Batch APIs never treat unresolved variables as allow
- DOT export properly quotes all node and cluster identifiers
- Benchmarks run out of process and never affect release runtime dependencies

### Dependencies

- Sprint 7 completed
- BenchmarkDotNet added only in benchmark project

### Open Questions

- Should `Cedar.Experimental` include AST inspection helpers beyond node evaluation, or should that remain internal?
- Should batch authorization support cancellation tokens from day one, matching the Go `context.Context` behavior?

## End-State Expected After Sprint 8

- Core parity delivered for the main authorizer, types, parser, JSON, AST builder, policy containers, stream APIs, and conformance corpus
- Public sidecar parity delivered for schema parsing, batch authorization, DOT export, and experimental node evaluation
- Planned automated coverage: 327 xUnit-style tests across the solution, 4 property/fuzz-equivalent suites, the embedded corpus suite, and 3 benchmark classes
- Every sprint leaves the repository in a compilable, testable state with a shippable increment rather than a partial rewrite branch

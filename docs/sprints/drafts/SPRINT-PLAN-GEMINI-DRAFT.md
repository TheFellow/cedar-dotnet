# Cedar-DotNet Sprint Plan

## Intent
Semantic port of `cedar-go` (Go Cedar authorization policy engine) to C# (.NET 9.0+). The port must achieve complete feature parity with the Go implementation, organized as a series of incremental sprints. Each sprint produces a compilable, testable deliverable.

---

## Sprint 1: Project Scaffolding & Core Values

### Overview
Establish the `cedar-dotnet` solution structure, testing framework, and implement the foundational `Value` type hierarchy (primitives and simple extensions).

### Use Cases
Developers can construct, compare, hash, and format basic Cedar values (`Boolean`, `Long`, `String`, etc.) in C#.

### Architecture
- `Cedar.Types` project containing an abstract `Value` record and sealed derivations to enable pattern matching.
- Deterministic error handling through a `Result<T, CedarError>` structure, avoiding exceptions for business logic.

### Implementation
- **Phase 1:** Solution setup (`Cedar.Types`, `Cedar.Core`, `Cedar.Tests`) and CI pipeline with .NET 9.0, xUnit, and FluentAssertions.
- **Phase 2:** `Value` abstract base class/record with `MarshalCedar`, `Equals`, `GetHashCode`, and `ToString()`.
- **Phase 3:** Implement core primitives: `Boolean`, `Long`, `String`.
- **Phase 4:** Implement extended primitives: `Decimal`, `Duration`, `Datetime`, `IPAddr`.

### Files Summary
`Cedar.Types/Value.cs`, `Cedar.Types/Boolean.cs`, `Cedar.Types/Long.cs`, `Cedar.Types/String.cs`, `Cedar.Types/Decimal.cs`, `Cedar.Types/Duration.cs`, `Cedar.Types/Datetime.cs`, `Cedar.Types/IPAddr.cs`, `Cedar.Types/Result.cs`, `Cedar.Tests/ValueTests.cs`.

### Definition of Done
- Solution compiles cleanly.
- All basic value types implemented with equality, hashing, and Cedar string formatting.
- Unit tests for each type port the equivalent tests from `cedar-go`.

### Risks
Getting the `Value` inheritance hierarchy wrong early could cause cascading issues with memory allocation and boxing.

### Security
N/A

### Dependencies
.NET 9.0 SDK, xUnit, FluentAssertions.

### Open Questions
Should we use `readonly record struct` vs `sealed record class` for small types like `Boolean`/`Long`?

---

## Sprint 2: Complex Types & Entities

### Overview
Implement complex Cedar value types (`Record`, `Set`, `Pattern`) and Entity-related constructs (`EntityUID`, `Entity`, `EntityMap`).

### Use Cases
Ability to construct composite types, look up entities by UID, evaluate equality on collections, and represent principal/action/resource objects.

### Architecture
Immutable collections using BCL's `FrozenSet<T>`/`ImmutableHashSet<T>` and `ImmutableDictionary<K, V>`. Custom equality comparers mirroring `cedar-go`'s `mapset`.

### Implementation
- **Phase 1:** Implement `Set` and `Record` values.
- **Phase 2:** Implement `Pattern` and `Wildcard` types.
- **Phase 3:** Implement `EntityUID`, `EntityType`, and `EntityUIDSet`.
- **Phase 4:** Implement `Entity` and `EntityMap`.
- **Phase 5:** Define `EntityGetter` interface.

### Files Summary
`Cedar.Types/Set.cs`, `Cedar.Types/Record.cs`, `Cedar.Types/Pattern.cs`, `Cedar.Types/EntityUID.cs`, `Cedar.Types/Entity.cs`, `Cedar.Types/EntityMap.cs`, `Cedar.Types/EntityGetter.cs`, `Cedar.Tests/ComplexTypeTests.cs`.

### Definition of Done
- Complex types function identically to Go counterparts.
- Deep equality and deterministic hashing are verified.
- All relevant `cedar-go` tests ported.

### Risks
Custom equality comparers for `Set` and `Record` must exactly match Go's semantics or evaluation will yield incorrect authorization decisions.

### Security
Memory limits on large sets/records should be considered to prevent DoS.

### Dependencies
Sprint 1, `System.Collections.Immutable`.

### Open Questions
How to achieve optimal hashing performance for `Set` matching Go's `mapset`?

---

## Sprint 3: AST Builder & JSON Marshalling

### Overview
Implement the Abstract Syntax Tree (AST) nodes, a fluent builder API to construct policies programmatically, and JSON serialization.

### Use Cases
Developers can construct valid Cedar policies using a C# fluent API and serialize/deserialize Cedar JSON formats.

### Architecture
- `Cedar.Ast` project with record hierarchies for AST nodes.
- Integration of `System.Text.Json` custom converters.

### Implementation
- **Phase 1:** AST node definitions (`Node`, `Expr`, `Condition`, `Effect`).
- **Phase 2:** Fluent builder API mimicking Go's `ast` package.
- **Phase 3:** JSON serializers/deserializers for Values and AST to match the official Cedar JSON schema (handling `__extn` formats).

### Files Summary
`Cedar.Ast/Node.cs`, `Cedar.Ast/Expr.cs`, `Cedar.Ast/Builder.cs`, `Cedar.Json/CedarJsonConverter.cs`, `Cedar.Tests/AstTests.cs`, `Cedar.Tests/JsonTests.cs`.

### Definition of Done
- AST fully represents the Cedar language.
- Fluent builder covers all constructs.
- JSON round-tripping works flawlessly and matches Go output.

### Risks
JSON serialization details in Cedar are complex, particularly around implicit vs explicit extension types.

### Security
JSON deserialization must be safe from nested structure stack overflows.

### Dependencies
Sprint 2, `System.Text.Json`.

### Open Questions
None.

---

## Sprint 4: Tokenizer & Parser

### Overview
Build the tokenizer and recursive descent parser to compile Cedar text policies into the AST.

### Use Cases
Converting raw Cedar policy strings into the AST.

### Architecture
`Cedar.Parser` project (internal). Hand-written tokenizer and recursive descent parser strictly matching Go's `internal/parser` implementation.

### Implementation
- **Phase 1:** Tokenizer (`Scanner`, `Token`, `TokenType`).
- **Phase 2:** Recursive descent parser logic for expressions.
- **Phase 3:** Parser logic for policy structure (effect, scope, conditions).
- **Phase 4:** Setup FsCheck property-based tests (arbitrary string -> parse -> format -> parse).

### Files Summary
`Cedar.Parser/Token.cs`, `Cedar.Parser/Tokenizer.cs`, `Cedar.Parser/Parser.cs`, `Cedar.Tests/ParserTests.cs`.

### Definition of Done
- Parser produces identical AST to Go parser for all valid inputs.
- Parses all valid Cedar syntax.
- Gracefully handles syntax errors with precise line/column tracking and clear error messages.

### Risks
Ensuring correct precedence rules and operator associativity identical to the Go parser.

### Security
Limit maximum parse depth to prevent stack overflow on deeply nested expressions.

### Dependencies
Sprint 3.

### Open Questions
None. (Keeping dependencies minimal dictates a hand-written parser rather than parser combinator libraries).

---

## Sprint 5: Evaluation Engine & Extension Functions

### Overview
Implement compiled evaluators, constant folding, and the extension function registry.

### Use Cases
The core engine evaluates an AST against an environment (entities, principal, action, resource, context) and produces a boolean/value.

### Architecture
`Cedar.Eval` internal project. Evaluator tree mimicking `internal/eval` in Go.

### Implementation
- **Phase 1:** `Environment`, `EvalContext`, and the core `Evaluator` interface.
- **Phase 2:** Implement the 25+ AST node evaluators.
- **Phase 3:** Implement constant folding logic.
- **Phase 4:** Extension function registry and all 25+ extension methods (for IP, Decimal, Datetime, Duration).

### Files Summary
`Cedar.Eval/Evaluator.cs`, `Cedar.Eval/Environment.cs`, `Cedar.Eval/ConstantFolder.cs`, `Cedar.Eval/Extensions.cs`, `Cedar.Tests/EvalTests.cs`.

### Definition of Done
- Evaluators correctly evaluate all expressions.
- Extension functions return identical results to Go implementations.
- Constant folding optimizes AST correctly.

### Risks
Subtle bugs in evaluator semantics (e.g., error short-circuiting in `&&` and `||`, or `has` operator specifics).

### Security
Evaluation must not panic on invalid types, but instead yield deterministic Cedar errors collected in the diagnostic.

### Dependencies
Sprint 4.

### Open Questions
None.

---

## Sprint 6: Authorizer, Policies & Conformance

### Overview
Build the public `Authorize` API, `PolicySet`, `PolicyList`, and integrate the 1.5MB embedded conformance corpus.

### Use Cases
End-to-end authorization requests returning Allow/Deny decisions with detailed diagnostics.

### Architecture
`Cedar.Core` public facade. Unifying the AST, Parser, and Eval into `Authorize(policies, entities, request)`.

### Implementation
- **Phase 1:** `PolicySet`, `PolicyList`, and `Policy` wrappers.
- **Phase 2:** `Request`, `Decision`, `Diagnostic`, `DiagnosticError`.
- **Phase 3:** `Authorizer` class implementing the fail-safe evaluation loop (Any forbid -> Deny; Any permit + no forbid -> Allow; Else -> Deny).
- **Phase 4:** Import `corpus-tests.tar.gz` and write the xUnit integration runner.

### Files Summary
`Cedar.Core/Authorizer.cs`, `Cedar.Core/PolicySet.cs`, `Cedar.Core/Decision.cs`, `Cedar.Core/Request.cs`, `Cedar.Tests/CorpusTests.cs`.

### Definition of Done
- `Authorize` produces identical decisions to Go.
- All conformance corpus tests pass.
- 249+ ported unit tests are passing.
- Test coverage matches `cedar-go`.

### Risks
Conformance tests might reveal subtle bugs from earlier sprints, requiring significant debugging time.

### Security
N/A

### Dependencies
Sprint 5.

### Open Questions
Should the conformance corpus be extracted dynamically at test runtime, or pre-extracted into the test project?
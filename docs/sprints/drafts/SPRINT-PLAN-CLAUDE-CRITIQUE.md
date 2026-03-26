# Cedar-DotNet Sprint Plan Critique

Reviewer: Claude Opus 4.6
Date: 2026-03-26
Reference: `github.com/strongdm/cedar-go` (75 test files, 249 unit tests, 4 fuzz tests, embedded corpus suite)
User constraints: .NET 9.0+, multi-project solution, `EntityUid` naming, full parity including schema + batch, 8-10 fine-grained sprints

---

## Codex Draft Critique

### Strengths

1. **Solution layout is well-considered.** The `Cedar` / `Cedar.Schema` / `Cedar.Batch` / `Cedar.Experimental` assembly split mirrors the Go module boundaries while keeping internals (parser, eval, serialization) as internal namespaces rather than separate projects. This is the correct architecture for a C# port.

2. **Naming conventions are correct.** `EntityUid`, `PolicySet`, `CedarValue` all follow the user's stated preferences and .NET conventions.

3. **Every sprint has a Security section** with concrete mitigations (bounded error accumulation, immutable types, fail-safe authorization). This is rare in sprint plans and welcome.

4. **Open Questions are thoughtful and actionable.** Questions like "`PolicyId` as record vs strong typedef" and "eager vs lazy `CedarPattern` compilation" show the author has thought about downstream consequences of early decisions.

5. **File-level implementation detail** makes each sprint directly executable by an agent. No guesswork about where code lives.

6. **Incremental deliverability.** Each sprint explicitly leaves the repo compilable and testable. The dependency chain is linear and clear.

7. **The `Directory.Build.props` / `Directory.Packages.props` scaffolding** is a mature .NET practice that will prevent version drift across test projects.

### Weaknesses

1. **Sprint 6 is a mega-sprint.** It combines: remaining evaluators (tags, patterns, extensions), constant folding, partial evaluation primitives, the full extension function registry (5 extension files), AND the entire conformance corpus integration. This is at least two sprints of work. The corpus alone will surface bugs across parser, serialization, and evaluation that will require backtracking. **Recommendation:** Split into Sprint 6a (extensions + remaining evaluators + extension tests) and Sprint 6b (constant folding + partial eval + corpus integration).

2. **Sprint 8 is also overloaded.** Batch authorization is a substantial feature (variable substitution, partial evaluation, callback-based enumeration). Combining it with experimental evaluation, DOT export, benchmarks, AND NuGet packaging is too much. **Recommendation:** Batch gets its own sprint; experimental + DOT + benchmarks + packaging is a separate sprint.

3. **Test count targets feel arbitrary and low.** 327 total tests across the solution for a reference that has 249 unit tests + a full corpus suite seems insufficient. The C# port should have at least as many unit tests as Go, plus additional tests for C#-specific serialization paths (System.Text.Json), immutability guarantees, and edge cases around nullable reference types. A more realistic target is 400-500 unit tests plus the corpus.

4. **Missing: `CancellationToken` strategy.** Go's `context.Context` is pervasive in the batch authorization API. The plan mentions no equivalent. Batch authorization must accept `CancellationToken` to match Go's cancellation semantics. This should be a design decision in Sprint 1, not discovered during Sprint 8.

5. **Missing: `PolicyIterator` / iteration pattern.** Go uses `iter.Seq` (Go 1.23+ range-over-func). The plan mentions "making `PolicySet` enumerable via a common interface or iterator contract" but doesn't decide what that interface looks like. `IEnumerable<KeyValuePair<PolicyId, Policy>>` is the obvious choice, but this affects the `Authorize` signature. Decide in Sprint 1 contracts.

6. **Missing: thread safety discussion.** Cedar's authorization is inherently read-only once policies are parsed, but `PolicySet` mutation (add/replace/remove) needs a concurrency story. Is `PolicySet` thread-safe for reads? Does it support concurrent modification? Go's design is implicitly safe because values are immutable, but C# has different guarantees.

7. **Missing: `isEmpty()` operator.** This was added in cedar-go 1.2.0 and is not mentioned anywhere in the sprint plan.

8. **Missing: `ImplicitlyMarshaledEntityUID`.** Go has a special wrapper type for JSON contexts where the entity UID should serialize without the explicit `__entity` wrapper. This is important for Request JSON serialization and is not mentioned.

9. **`CedarValue` as sealed records under a common base class** is acknowledged as "less allocation-efficient" but the plan underestimates the impact. Every evaluation step will box/unbox through the base class. The Go implementation uses interface dispatch which is more efficient for this pattern. C# discriminated unions (or a struct-based tagged union with `[StructLayout]`) would be closer to the Go model. At minimum, this deserves a spike in Sprint 1 rather than deferring optimization to "after Sprint 6."

10. **The `Encoder`/`Decoder` API mapping is unclear.** Go's `stream.go` uses `io.Reader`/`io.Writer` patterns. The plan lists `Encoder.cs` and `Decoder.cs` but doesn't describe whether these wrap `Stream`, `TextReader`/`TextWriter`, `PipeReader`/`PipeWriter`, or something else. This affects the public API surface.

### Gaps in Risk Analysis

- **No risk identified for System.Text.Json limitations.** STJ's converter model has known limitations with polymorphic serialization, which will be hit immediately by the `CedarValue` hierarchy. The plan should acknowledge this and decide whether to use `JsonDerivedType` attributes or a custom converter with discriminator logic.
- **No risk for Go-isms that don't translate.** Go's multiple return values, implicit interface satisfaction, and structural typing all require deliberate C# translations. The plan assumes a mechanical port but doesn't flag where the semantic gap is widest.
- **Corpus test runtime.** Extracting a 1.5MB tar.gz and running hundreds of test cases adds significant CI time. The plan should discuss whether corpus tests run in a separate test category or are always-on.

### Missing Edge Cases

- Error accumulation limits (what happens with 10,000 parse errors?)
- Unicode normalization in `CedarString` comparisons
- `EntityMap` cycle detection (parent-of cycles in entity graphs)
- Overflow behavior for `CedarLong` arithmetic (Go uses checked arithmetic)
- `CedarDecimal` precision: Cedar specifies exactly 4 decimal places; .NET's `decimal` has 28-29 digits
- `CedarDuration` / `CedarDatetime` representation: Go uses custom types; .NET has `TimeSpan` and `DateTimeOffset` which have different range/precision

### Definition of Done Completeness

The DoDs are specific (test counts, named behaviors) but have gaps:
- No coverage percentage targets
- No mention of XML documentation on public APIs
- No mention of `dotnet pack` producing valid NuGet packages until Sprint 8
- No mention of static analysis (nullable warnings, IDE analyzers)
- Sprint 6 DoD says "corpus results match cedar-go decisions, reasons, and error policy IDs" but doesn't define the acceptance threshold (100%? 99%? Which corpus version?)

### Sprint Granularity Assessment

At **8 sprints**, the plan is at the low end of the requested 8-10 range. Sprints 1-5 are well-sized. Sprint 6 and Sprint 8 are each doing the work of two sprints. Splitting those two would yield 10 sprints, which is within range and produces more genuinely useful checkpoints.

**Sprint-by-sprint deliverable usefulness:**
- Sprint 1: Useful (build infra + contracts)
- Sprint 2: Useful (complete type system, enables integration with entity stores)
- Sprint 3: Useful (AST builder enables programmatic policy construction)
- Sprint 4: Useful (parsing enables Cedar text ingestion)
- Sprint 5: Useful (first real authorization)
- Sprint 6: Too large to be a single deliverable
- Sprint 7: Useful (schema is independently valuable)
- Sprint 8: Too large; batch is independently valuable but buried with benchmarks

---

## Gemini Draft Critique

### Strengths

1. **Mentions `Result<T, CedarError>` for error handling.** This is a genuine design consideration — Go's error returns don't map directly to C# exceptions. Railway-oriented error handling is worth considering, though the implementation approach needs refinement (see weaknesses).

2. **Recommends `FrozenSet<T>` / `ImmutableDictionary<K,V>`.** These are excellent BCL choices for Cedar's immutable collection semantics. `FrozenSet<T>` in particular is a .NET 8+ feature that provides optimal read performance for sets that don't change after construction — exactly the Cedar use case.

3. **Recommends FsCheck for property-based testing.** The `arbitrary string -> parse -> format -> parse` round-trip is a powerful parser correctness strategy that the Codex draft mentions only in passing.

4. **Separating parser into its own sprint (Sprint 4)** is cleaner than Codex's approach of bundling tokenizer with AST (Sprint 3) then parser with containers (Sprint 4). Having a dedicated parser sprint makes the work more focused.

5. **Risk identification around Set/Record equality** is precise: "Custom equality comparers must exactly match Go's semantics or evaluation will yield incorrect authorization decisions." This is the single most dangerous semantic gap in the entire port.

### Weaknesses

1. **Only 6 sprints — well below the requested 8-10.** The plan simply stops after the authorizer and corpus. Four entire feature areas are missing.

2. **Missing: Schema package.** cedar-go's `x/exp/schema` provides Cedar schema parsing in both human-readable and JSON formats with bidirectional conversion. This is a user-stated requirement ("full feature parity including schema+batch"). Its absence is a critical gap.

3. **Missing: Batch authorization.** cedar-go's `x/exp/batch` provides high-performance batch authorization with variable substitution and partial evaluation. Also explicitly required by the user. Not mentioned at all.

4. **Missing: DOT export.** `x/exp/dot` provides Graphviz entity graph visualization. Not mentioned.

5. **Missing: Experimental evaluation.** `x/exp/eval` provides direct AST node evaluation and partial policy evaluation. Not mentioned.

6. **Missing: Benchmarks.** No performance validation sprint.

7. **Missing: Encoder/Decoder/stream API.** Go's `stream.go` provides policy streaming. Not mentioned.

8. **Wrong naming convention.** Uses `EntityUID` throughout, but the user explicitly specified `EntityUid` (.NET naming conventions, not Go acronym casing). Also uses `IPAddr` instead of `IpAddress` or similar .NET-idiomatic name.

9. **Excessive project proliferation.** Proposes `Cedar.Types`, `Cedar.Core`, `Cedar.Ast`, `Cedar.Parser`, `Cedar.Eval` as separate projects. This is 5 assemblies for what cedar-go ships as a single module with internal packages. The Codex draft correctly keeps parser/eval/serialization internal to one `Cedar` assembly with separate assemblies only for genuinely separate public surfaces (Schema, Batch, Experimental). Five assemblies means five sets of `InternalsVisibleTo`, five NuGet packages to version, and a dependency graph that complicates consumption.

10. **`Result<T, CedarError>` is non-standard .NET.** While railway-oriented programming is valuable in F#, introducing a custom `Result` type in a C# library is friction for consumers who expect exceptions or `bool TryParse(...)` patterns. The Go reference uses Go's native error returns; the C# equivalent is exceptions for exceptional conditions and `Try*` patterns for expected failures. A custom `Result` type would make the library feel foreign to C# consumers.

11. **FluentAssertions adds a NuGet dependency** to the test projects. The user specified "BCL-only for engine projects where possible." While FluentAssertions is test-only, xUnit's built-in assertions are sufficient and avoid the dependency. This is a minor point but signals a pattern of reaching for external packages.

12. **"Open Questions: None" appears three times** (Sprints 3, 5, 6). This is a red flag. A Cedar-to-C# port has open questions at every layer — JSON serialization polymorphism, async patterns, nullable reference type strategy, collection interface choices. "None" suggests insufficient analysis rather than a clean design.

13. **Security sections are often "N/A."** Sprint 1 and Sprint 6 both say "N/A" for security. Sprint 1 should address hash-collision DoS for value types. Sprint 6 should address fail-safe authorization guarantees, missing entity handling, and diagnostic information leakage.

### Gaps in Risk Analysis

- **No mention of System.Text.Json challenges.** The plan says "integration of System.Text.Json custom converters" but doesn't flag the polymorphic serialization problem that will hit immediately.
- **No mention of Cedar decimal precision mismatch** with .NET's `decimal` type.
- **No mention of Go iterator pattern translation** (`iter.Seq` -> `IEnumerable<T>`).
- **No mention of context.Context / CancellationToken mapping.**
- **No mention of CI time for corpus tests.**
- **No risk analysis for the AST being used before the parser exists** (Sprint 3 builds AST + JSON before Sprint 4 builds the parser). This means Sprint 3's JSON round-trip testing is limited to programmatically-constructed ASTs, which may not exercise the same shapes as parsed policies.

### Missing Edge Cases

- All the same edge cases missing from Codex (Unicode normalization, cycle detection, overflow, precision), plus:
- No mention of `MarshalCedar()` / `UnmarshalCedar()` equivalents
- No mention of binary EntityUID codec
- No mention of tag operators (`getTag`, `hasTag`)
- No mention of `isEmpty()` operator
- No mention of trailing comma support in parser
- No mention of extended `has` operator
- No mention of `Pattern` / `Wildcard` JSON serialization format

### Definition of Done Completeness

- Sprint 1 DoD: "Solution compiles cleanly" and "Unit tests for each type" — no test count, no specific behaviors
- Sprint 3 DoD: "JSON round-tripping works flawlessly" — "flawlessly" is not measurable
- Sprint 6 DoD: "249+ ported unit tests" — this conflates Go's test count with C# targets. The C# port should have its own test count based on coverage needs, not a 1:1 mapping of Go test functions
- No DoD mentions static analysis, nullable warnings, or documentation
- No DoD specifies corpus pass rate (must be 100%)

### Sprint Granularity Assessment

At **6 sprints**, this plan is well below the requested range and attempts to cover only the core authorization path. Even for the core path, the granularity is uneven:

- Sprint 1 includes extended types (Decimal, Duration, Datetime, IPAddr) that Codex correctly defers to Sprint 2
- Sprint 5 combines evaluation engine + constant folding + ALL extension functions — this is a mega-sprint
- Sprint 6 combines authorizer + policy containers + conformance corpus — also a mega-sprint

**Sprint-by-sprint deliverable usefulness:**
- Sprint 1: Useful (values)
- Sprint 2: Useful (entities + collections)
- Sprint 3: Partially useful (AST without parser has limited standalone value)
- Sprint 4: Useful (parsing)
- Sprint 5: Too large, but the eval engine is critical
- Sprint 6: Too large, but authorization is the whole point
- **Sprints 7-10: Don't exist.** Schema, batch, experimental, benchmarks, and packaging are all missing.

---

## Comparative Assessment

| Dimension | Codex | Gemini |
|---|---|---|
| Sprint count | 8 (low end of range) | 6 (below range) |
| Feature parity | Complete | Core only — missing schema, batch, experimental, DOT |
| Naming conventions | Correct (`EntityUid`) | Incorrect (`EntityUID`) |
| Project structure | Correct (internal namespaces + sidecar assemblies) | Over-proliferated (5+ assemblies) |
| File-level detail | Comprehensive | Minimal |
| Security analysis | Every sprint | Often "N/A" |
| Open questions | Thoughtful | Often "None" |
| Risk analysis | Good but incomplete | Sparse |
| DoD specificity | Test counts + named behaviors | Vague ("flawlessly") |
| External dependencies | BCL-only for engine | FluentAssertions |
| Benchmark coverage | Yes (Sprint 8) | No |
| NuGet packaging | Yes (Sprint 8) | No |

### Recommendation

**The Codex draft is the stronger foundation.** It should be refined by:

1. Splitting Sprint 6 into two sprints (extensions/evaluators vs corpus/partial-eval)
2. Splitting Sprint 8 into two sprints (batch vs experimental/benchmarks/packaging)
3. Adding `CancellationToken` and iteration strategy to Sprint 1 contracts
4. Adding a `CedarValue` representation spike to Sprint 1
5. Strengthening test count targets (aim for 400-500+ unit tests plus full corpus)
6. Adding `isEmpty()`, tag operators, trailing commas, and extended `has` to the operator checklist
7. Addressing System.Text.Json polymorphic serialization as a named risk
8. Deciding on `Stream` vs `TextReader` for Encoder/Decoder in Sprint 4
9. Adding corpus pass rate (100%) to Sprint 6 DoD
10. Adding thread safety / immutability guarantees to Sprint 1 design decisions

The Gemini draft's contributions worth incorporating:
- `FrozenSet<T>` recommendation for immutable sets
- FsCheck property-based testing for parser round-trips
- The risk callout on Set/Record equality semantics
- Keeping the parser as a dedicated, focused sprint

With these refinements, a 10-sprint plan would provide the right granularity, full feature parity, and genuinely useful deliverables at each checkpoint.

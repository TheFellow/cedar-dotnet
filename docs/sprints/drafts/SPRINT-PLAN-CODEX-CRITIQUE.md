# Cedar-DotNet Sprint Plan Critique

## Evaluation Lens

The critique below uses the explicit project constraints, not just internal draft coherence:

- Semantic port of `cedar-go` to C# on .NET 9.0+
- Full feature parity, including `schema` and `batch`
- Multi-project solution preferred over a single monolith assembly
- .NET naming conventions preferred (`EntityUid`, not `EntityUID`)
- Roughly 8-10 fine-grained sprints
- Each sprint should end in a genuinely useful, compilable, testable increment

## Claude Draft

### Strengths

- Strongest overall understanding of `cedar-go` surface area. It is the only draft here that clearly tries to cover core engine, schema, batch, DOT/export-adjacent experimental APIs, benchmarks, and release hardening.
- Good sprint count. Ten sprints is close to the requested cadence, and the early-to-late progression is mostly legible.
- The draft is materially more execution-ready than the Gemini version. The implementation phases, file inventories, and cumulative test intent make it much easier to actually start work.
- Definitions of Done are usually measurable. Even where the exact numbers are speculative, the structure is useful.
- It takes testing seriously: unit tests, round-trip tests, fuzz-seed tests, corpus tests, and benchmarks are all present.
- Most sprints end with something meaningful for developers, not just internal scaffolding.

### Weaknesses

- It directly conflicts with two stated preferences:
  - It chooses a single core assembly instead of the preferred multi-project solution.
  - It explicitly prefers Cedar-spec naming such as `EntityUID`, which contradicts the stated .NET naming preference.
- It claims "full parity" too early. Sprint 7 says the core engine achieves full parity, but schema and batch are still deferred to Sprints 8 and 9. Given the user explicitly wants parity including schema and batch, that is not actually full parity.
- Sprint 6 is too large and too critical. Compiler, evaluator tree, extension registry, and public authorization API all landing together means the first truly end-to-end useful authorization deliverable arrives late and with high integration risk.
- Sprint 9 is overloaded and partly off-target. Batch authorization is in-scope, but bundling batch, standalone node evaluation, partial evaluation, and DOT export in one sprint mixes required parity work with questionable scope creep.
- Partial evaluation looks especially suspect. The intent document says no partial evaluation is in scope for the shipped capability set, yet the draft schedules it anyway.
- The draft is overly file-plan-driven in places. It looks concrete, but some file breakdowns imply design certainty before the difficult semantic questions are settled.
- The "single assembly for core" decision increases the chance of namespace sprawl and blurry boundaries in a repo that is otherwise greenfield and would benefit from clearer project seams.

### Gaps In Risk Analysis

- It underplays the risk of late parity discovery. Deferring schema and batch until after the draft says parity is already achieved creates a planning blind spot.
- It does not sufficiently call out cross-runtime semantic mismatch risk between Go and .NET for:
  - fixed-point decimal boundaries
  - datetime parsing and offset behavior
  - IP parsing and canonicalization
  - hashing and equality semantics
- It does not treat public API premature commitment as a major risk. Locking in a fluent builder before parser/eval parity is proven may force API churn later.
- It does not call out allocation/performance risk from the "everything is a class" value hierarchy.
- Differential testing against live `cedar-go` behavior is mentioned in the intent, but the draft does not turn it into an early, explicit risk-mitigation tactic.
- It does not discuss corpus version skew, fixture ingestion drift, or licensing/provenance handling for imported test artifacts.

### Missing Edge Cases

- Entity graph pathologies: cycles, self-parenting, deep ancestry chains, and incomplete entity maps.
- Parser edge cases beyond precedence:
  - duplicate annotations
  - comments in awkward positions
  - trailing commas in all supported locations
  - string escaping and Unicode corner cases
- JSON edge cases:
  - records that legitimately contain `__extn` or `__entity` as ordinary keys
  - implicit versus explicit entity UID ambiguity
  - deterministic ordering expectations
- Value edge cases:
  - decimal overflow boundaries
  - duration and datetime normalization behavior
  - IPv4-mapped IPv6 or non-canonical CIDR forms
- Policy container behavior:
  - duplicate policy IDs
  - stable iteration order versus serialization order
  - policy ID preservation in diagnostics across parse/serialize cycles
- Batch edge cases are barely surfaced even though batch is explicitly in scope:
  - zero-dimension batches
  - explosion limits
  - cancellation behavior
  - partial failure handling

### Definition Of Done Completeness

- Better than Gemini, but still uneven.
- Good:
  - Usually measurable
  - Usually tied to tests and concrete capabilities
  - Often explicit about parity-sensitive behaviors
- Incomplete:
  - "Full parity" is declared before schema and batch are done
  - Few DoDs explicitly say "all corresponding `cedar-go` tests for this surface are ported and passing"
  - Benchmark DoD lacks an acceptance threshold, so it proves existence of numbers, not parity or adequacy
  - Schema and batch DoDs do not clearly commit to Go-golden or corpus-backed equivalence

### Sprint Granularity And Deliverable Quality

- Overall granularity is close to the requested target, but not evenly distributed.
- Good granularity:
  - Sprint 1 is reasonable
  - Sprint 3 is useful and conceptually coherent
  - Sprint 4 and Sprint 5 are good standalone milestones
- Too large:
  - Sprint 2 combines extended scalars, collections, full entity system, and request types
  - Sprint 6 is the biggest problem; it is effectively the heart of the product in one sprint
  - Sprint 9 combines too many unrelated experimental/sidecar features
- Deliverable usefulness:
  - Sprints 1-5 all produce developer-usable increments
  - Sprint 6 is the first strong end-user-meaningful deliverable because authorization finally works
  - Sprint 7 is useful as a validation sprint
  - Sprint 8 is useful if schema parity is required
  - Sprint 9 is only partly useful because it mixes required work with optional/speculative work

### Bottom Line

This is the better starting point, but it should not be adopted verbatim. The structure is strong enough to salvage, but it needs four major corrections:

1. Switch to a multi-project solution as the default architecture, not a single core assembly.
2. Use .NET naming conventions in the public surface.
3. Stop claiming parity before schema and batch are complete.
4. Split Sprint 6 and narrow Sprint 9 so required parity work is not bundled with speculative experimental work.

## Gemini Draft

### Strengths

- It is much easier to scan than the Claude draft. The plan is straightforward and the sprint sequence is easy to understand quickly.
- It aligns better with the multi-project preference. `Cedar.Types`, `Cedar.Ast`, `Cedar.Parser`, `Cedar.Eval`, and `Cedar.Core` are much closer to the requested repo shape.
- The first four sprints follow a sensible conceptual progression: values, complex types, AST/JSON, parser.
- It gets to a usable authorization engine in a compact path.
- It avoids some of Claude's speculative expansion and stays focused on the mainline engine.

### Weaknesses

- It is too coarse. Six sprints is well below the requested "about 8-10 fine-grained sprints."
- It does not achieve the requested feature parity. Schema and batch are missing entirely. DOT/export and release hardening are also absent.
- The later sprints are too chunky:
  - Sprint 3 combines AST builder and JSON marshalling
  - Sprint 5 combines evaluator tree, constant folding, and all extension functions
  - Sprint 6 combines authorizer, policy containers, conformance, and test coverage closure
- It is under-specified relative to the difficulty of the work. The plan reads more like an outline than an execution plan.
- It also misses the stated naming preference. It uses `EntityUID`, not `.NET`-style `EntityUid`.
- It never really explains how the repo gets from "engine mostly works" to "credible, parity-proven, distributable library."

### Gaps In Risk Analysis

- Risk treatment is too shallow almost everywhere. Most sections have a single sentence, which is not enough for a port with this many semantic traps.
- It does not call out the biggest program risks:
  - schema and batch are omitted even though they are required
  - conformance is deferred to the end, so semantic drift could be discovered too late
  - public API and internal representation could diverge after parser/evaluator work
- It barely addresses Go versus .NET mismatch risks for decimal, datetime, duration, IP, equality, hashing, and parser behavior.
- It does not discuss performance risk, allocation strategy, or immutable collection tradeoffs beyond a passing note.
- It does not identify artifact ingestion risk for the conformance corpus or test-porting effort risk.

### Missing Edge Cases

- Parser:
  - trailing commas
  - comments
  - annotation handling
  - precise escape and Unicode behavior
  - malformed but near-valid policies
- Evaluator:
  - short-circuit error suppression
  - `has` and attribute access differences between records and entities
  - missing entities
  - policy forbid-overrides semantics under mixed permit/error conditions
- JSON:
  - implicit versus explicit entity UID forms
  - `__extn` and `__entity` collisions with ordinary record keys
  - round-trip ordering/determinism expectations
- Containers:
  - duplicate policy IDs
  - deterministic ordering
  - preservation of source position and policy IDs in diagnostics
- Required parity surfaces:
  - schema parsing edge cases are absent because schema is absent
  - batch combinatorics and failure modes are absent because batch is absent

### Definition Of Done Completeness

- The DoDs are readable but too qualitative.
- They usually say what should be true, but not how it will be proven in a way that would let a reviewer accept or reject the sprint cleanly.
- Missing pieces:
  - no cumulative plan for schema or batch parity
  - no release or packaging completion criteria
  - no benchmark or performance validation criteria
  - little explicit linkage to ported `cedar-go` tests
  - conformance acceptance exists only in the final sprint, which is too late

### Sprint Granularity And Deliverable Quality

- The main problem is granularity. This is a reasonable high-level roadmap, but not a good sprint plan for the stated preference.
- Sprint usefulness by itself is acceptable:
  - Sprint 1 produces usable type primitives
  - Sprint 2 produces a complete value and entity model
  - Sprint 3 produces a usable AST/JSON layer
  - Sprint 4 produces a parser
  - Sprint 5 produces an evaluator
  - Sprint 6 produces authorization plus validation
- The issue is that these are roadmap-sized chunks, not fine-grained sprint slices.
- Because schema and batch are omitted, the overall sequence does not culminate in the requested deliverable, even if every listed sprint succeeds.

### Bottom Line

This draft is cleaner and closer to the preferred project structure, but it is incomplete for the actual goal. It is a good backbone for the first 4-6 milestones of the core engine, not a sufficient full sprint plan for the project.

## Comparative Assessment

### Which Draft Is Closer To The Target?

Claude is closer on scope coverage and execution detail.

Gemini is closer on project structure simplicity and the multi-project instinct.

If forced to choose a base, Claude is the better base document, but only after substantial correction. Gemini is too incomplete to serve as the primary plan without major expansion.

### Recommended Synthesis

The best final sprint plan would combine:

- Claude's scope completeness and Definition-of-Done discipline
- Gemini's multi-project shape
- The user's explicit conventions:
  - .NET naming
  - full parity including schema and batch
  - 8-10 fine-grained sprints

### Concrete Changes I Would Make Before Adopting Either Draft

1. Recast the architecture around a multi-project solution from day one.
2. Normalize all public naming to idiomatic .NET forms such as `EntityUid`.
3. Split the heaviest work so authorization is not one giant sprint:
   - separate compiler/evaluator core from authorizer/conformance closure
4. Make schema and batch explicit parity milestones, not sidecars that appear after "parity" is already claimed.
5. Remove or explicitly de-scope partial evaluation unless the project owner confirms it is required.
6. Strengthen every DoD with explicit proof obligations:
   - ported `cedar-go` tests for that slice
   - differential or corpus-backed checks where applicable
   - clear acceptance for ordering, diagnostics, and serialization parity
7. Keep every sprint ending in a usable artifact, but avoid bundling unrelated required and optional work into the same sprint.

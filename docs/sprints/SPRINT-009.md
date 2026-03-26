# Sprint 009: Batch Authorization, Experimental Surface, Benchmarks, and Packaging

## Overview
Close the remaining Go surface: batch authorization (`x/exp/batch/`), standalone node evaluation (`x/exp/eval/`), DOT export (`x/exp/dot/`), performance benchmarks, NuGet packaging, and CI. After this sprint, the C# port has complete feature parity.

## Use Cases
1. **Batch authorization**: Authorize multiple variable combinations in a single call with CancellationToken support
2. **Standalone node evaluation**: Evaluate AST nodes outside of policy context
3. **Partial evaluation**: Partially evaluate policies and return residual ASTs
4. **DOT export**: Export entity graphs as Graphviz DOT format
5. **Performance benchmarks**: Benchmark authorization, parsing, and type operations
6. **NuGet packaging**: Package all assemblies for NuGet distribution

## Implementation

### Phase 1: Batch authorization with tests (~30% effort)

**Files:**
- `src/Cedar.Batch/Cedar.Batch.csproj` — References Cedar.Core
- `src/Cedar.Batch/BatchAuthorization.cs` — Authorize with variable substitution; accepts CancellationToken
- `src/Cedar.Batch/BatchRequest.cs` — Request template with variable placeholders
- `src/Cedar.Batch/BatchResult.cs` — Per-combination decision + diagnostic
- `src/Cedar.Batch/BatchVariable.cs` — Variable substitution types
- `test/Cedar.Batch.Tests/BatchAuthorizationTests.cs` — Variable substitution, multi-combo, cancellation, default deny (~15 tests)

**Acceptance:** Batch tests pass before moving to Phase 2. Single-variable and multi-variable combinations produce correct decisions. CancellationToken aborts in-progress batch.

### Phase 2: Experimental with tests (~20% effort)

**Files:**
- `src/Cedar.Experimental/Cedar.Experimental.csproj`
- `src/Cedar.Experimental/NodeEvaluation.cs` — Evaluate standalone AST node in environment
- `src/Cedar.Experimental/PartialEvaluation.cs` — Partially evaluate policy, return residual AST
- `src/Cedar.Experimental/EntityGraphDotWriter.cs` — EntityMap -> Graphviz DOT format
- `test/Cedar.Experimental.Tests/NodeEvaluationTests.cs` — Standalone node eval with various expressions (~10 tests)
- `test/Cedar.Experimental.Tests/PartialEvaluationTests.cs` — Partial eval, residuals, fully-determined policies (~10 tests)
- `test/Cedar.Experimental.Tests/DotWriterTests.cs` — DOT output, identifier quoting, empty graph (~8 tests)

**Acceptance:** Experimental tests pass before moving to Phase 3. Node evaluation produces same results as full authorization. DOT output is valid Graphviz syntax with quoted identifiers.

### Phase 3: Benchmarks (~15% effort)

**Files:**
- `benchmarks/Cedar.Benchmarks/Cedar.Benchmarks.csproj` — BenchmarkDotNet
- `benchmarks/Cedar.Benchmarks/AuthorizeBenchmarks.cs` — Simple/complex/many policies
- `benchmarks/Cedar.Benchmarks/ParseBenchmarks.cs` — Cedar text and JSON throughput
- `benchmarks/Cedar.Benchmarks/TypeBenchmarks.cs` — Entity lookup, set contains, record access

### Phase 4: Packaging and CI (~15% effort)

**Files:**
- Update all `.csproj` — NuGet metadata: PackageId, Version, Authors, License, RepositoryUrl
- `.github/workflows/ci.yml` — Build, test, pack on push/PR
- `CLAUDE.md` — Repository conventions for future sessions

## Files Summary

| File | Action | Phase | Purpose |
|------|--------|-------|---------|
| `src/Cedar.Batch/Cedar.Batch.csproj` | Create | 1 | Batch authorization project |
| `src/Cedar.Batch/BatchAuthorization.cs` | Create | 1 | Batch authorization API |
| `src/Cedar.Batch/BatchRequest.cs` | Create | 1 | Batch request template |
| `src/Cedar.Batch/BatchResult.cs` | Create | 1 | Batch result type |
| `src/Cedar.Batch/BatchVariable.cs` | Create | 1 | Variable substitution |
| `test/Cedar.Batch.Tests/BatchAuthorizationTests.cs` | Create | 1 | Batch behavior tests |
| `src/Cedar.Experimental/Cedar.Experimental.csproj` | Create | 2 | Experimental project |
| `src/Cedar.Experimental/NodeEvaluation.cs` | Create | 2 | Node evaluation |
| `src/Cedar.Experimental/PartialEvaluation.cs` | Create | 2 | Partial evaluation |
| `src/Cedar.Experimental/EntityGraphDotWriter.cs` | Create | 2 | DOT export |
| `test/Cedar.Experimental.Tests/NodeEvaluationTests.cs` | Create | 2 | Node eval behavior tests |
| `test/Cedar.Experimental.Tests/PartialEvaluationTests.cs` | Create | 2 | Partial eval behavior tests |
| `test/Cedar.Experimental.Tests/DotWriterTests.cs` | Create | 2 | DOT writer behavior tests |
| `benchmarks/Cedar.Benchmarks/Cedar.Benchmarks.csproj` | Create | 3 | Benchmark project |
| `benchmarks/Cedar.Benchmarks/AuthorizeBenchmarks.cs` | Create | 3 | Auth benchmarks |
| `benchmarks/Cedar.Benchmarks/ParseBenchmarks.cs` | Create | 3 | Parse benchmarks |
| `benchmarks/Cedar.Benchmarks/TypeBenchmarks.cs` | Create | 3 | Type benchmarks |
| `.github/workflows/ci.yml` | Create | 4 | CI pipeline |
| `CLAUDE.md` | Create | 4 | Repository conventions |

## Definition of Done

### Build gate
- [ ] `dotnet test cedar-dotnet.sln` passes across ALL projects, zero warnings
- [ ] **687+ unit tests** + full conformance corpus across 55+ test files

### Batch authorization behavior
- [ ] Single principal × multiple resources produces per-resource decisions
- [ ] Multi-variable cartesian product produces correct combination count
- [ ] CancellationToken cancels in-progress batch and returns partial results
- [ ] Unresolved variables default to deny (never implicit allow)
- [ ] Batch results include per-combination diagnostics matching single-request Authorization.Authorize

### Experimental behavior
- [ ] Standalone node evaluation produces same values as full policy evaluation
- [ ] Partial evaluation with known principal/action reduces policy to residual AST
- [ ] Fully-determined partial evaluation returns concrete allow/deny decision
- [ ] DOT export produces valid Graphviz DOT with properly quoted identifiers
- [ ] DOT export handles empty entity graph and single-entity graph

### Benchmarks and packaging
- [ ] Benchmarks run without errors; baseline numbers documented
- [ ] NuGet packages build: `dotnet pack` succeeds for Cedar.Core, Cedar.Schema, Cedar.Batch, Cedar.Experimental
- [ ] CI pipeline: restore -> build -> test -> pack all green
- [ ] `CLAUDE.md` documents project conventions

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Batch performance scales with variable domain product | Medium | Medium | Document limits; CancellationToken for timeout |
| Partial evaluation complexity | Medium | Medium | Match Go implementation closely; bounded by test parity |
| Experimental APIs hard to retract | Low | Medium | Mark as pre-release in NuGet versioning |

## Security Considerations
- Batch APIs never treat unresolved variables as allow
- DOT export quotes all identifiers (prevent DOT injection)
- Benchmarks out-of-process, don't affect release dependencies

## Dependencies
- Sprint 007 completed (core engine + constant folding)
- Sprint 008 completed (schema)
- BenchmarkDotNet (benchmark-only)

## Open Questions
None identified.

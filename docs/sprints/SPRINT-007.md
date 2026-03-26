# Sprint 007: Constant Folding, Conformance Corpus, and Parser Hardening

## Overview
Add constant folding optimization, integrate the 1.5MB conformance corpus, and harden with property-based tests. Validates the entire stack end-to-end against the Cedar reference. After this sprint, the core engine achieves full parity with cedar-go.

## Use Cases
1. **Constant folding**: Optimize constant sub-expressions at compile time
2. **Conformance validation**: Run full conformance corpus to validate parity with cedar-go
3. **Property-based testing**: Exercise random valid Cedar policies without crashes
4. **Fuzz seed testing**: Validate edge cases from Go's fuzz targets
5. **End-to-end smoke tests**: Full pipeline from parse to authorize

## Implementation

### Phase 1: Constant folding (~20% effort)

**Files:**
- `src/Cedar.Core/Internal/Eval/ConstantFolder.cs` — FoldPolicy(PolicyAst) -> optimized PolicyAst; folds constant sub-expressions into NodeValue; never folds PARC-dependent or entity-dependent expressions
- Update `Compiler.cs` — Insert FoldPolicy() before ToEval()

### Phase 2: Conformance corpus (~40% effort)

**Files:**
- `testdata/corpus-tests.tar.gz` — Copy from cedar-go
- `test/Cedar.Conformance/Cedar.Conformance.csproj` — Conformance test project
- `test/Cedar.Conformance/CorpusTestData.cs` — Tar.gz extraction; scenario enumeration
- `test/Cedar.Conformance/CorpusTests.cs` — [Theory] with member data: authorize and compare decision, reasons, error policy IDs

### Phase 3: Property-based and fuzz-seed tests (~25% effort)

**Files:**
- `test/Cedar.Tests/Parser/PropertyTests.cs` — FsCheck: valid Cedar -> parse -> serialize -> re-parse -> assert equivalence
- `test/Cedar.Tests/Parser/FuzzSeedTests.cs` — Port Go's fuzz corpus seeds as [Theory] cases (~20 tests)
- `test/Cedar.Tests/Eval/ConstantFolderTests.cs` — Fold arithmetic, extensions, sets; verify entity-dependent NOT folded (~18 tests)

### Phase 4: Integration smoke tests (~15% effort)

**Files:**
- `test/Cedar.Tests/Integration/EndToEndTests.cs` — Full pipeline: parse Cedar text -> authorize -> verify (~10 tests)

## Files Summary

| File | Action | Purpose |
|------|--------|---------|
| `src/Cedar.Core/Internal/Eval/ConstantFolder.cs` | Create | Constant folding optimizer |
| `src/Cedar.Core/Internal/Eval/Compiler.cs` | Modify | Insert constant folding step |
| `testdata/corpus-tests.tar.gz` | Create | Conformance corpus |
| `test/Cedar.Conformance/Cedar.Conformance.csproj` | Create | Conformance test project |
| `test/Cedar.Conformance/CorpusTestData.cs` | Create | Corpus data loader |
| `test/Cedar.Conformance/CorpusTests.cs` | Create | Corpus test runner |
| `test/Cedar.Tests/Parser/PropertyTests.cs` | Create | Property-based tests |
| `test/Cedar.Tests/Parser/FuzzSeedTests.cs` | Create | Fuzz seed tests |
| `test/Cedar.Tests/Eval/ConstantFolderTests.cs` | Create | Constant folder tests |
| `test/Cedar.Tests/Integration/EndToEndTests.cs` | Create | End-to-end smoke tests |

## Definition of Done

### Build gate
- [ ] `dotnet test` passes with **592+ tests** + **full corpus suite**, zero warnings

### Constant folding behavior
- [ ] `1 + 1` folds to `NodeValue(2)`, `decimal("3.14")` folds to `NodeValue(CedarDecimal(3.14))`
- [ ] Extension constructor calls with literal args fold at compile time
- [ ] Nested constant arithmetic folds recursively (`(1 + 2) * 3` -> `NodeValue(9)`)
- [ ] PARC-dependent expressions (`principal`, `action`, `resource`, `context`) are NOT folded
- [ ] Entity-dependent expressions (attribute access, `in`, `is`) are NOT folded
- [ ] Folding preserves evaluation semantics — tests compare folded vs unfolded output

### Conformance corpus
- [ ] **100% corpus pass rate** — decisions, reasons, and error policy IDs match cedar-go
- [ ] Corpus loader handles tar.gz extraction in-memory with bounded size

### Property and fuzz coverage
- [ ] Property tests exercise 1000+ random valid Cedar policies without crashes
- [ ] Parse -> serialize -> re-parse round-trip preserves AST equivalence
- [ ] Fuzz seed tests cover all edge cases from Go's 4 fuzz targets

### Integration
- [ ] End-to-end tests: Cedar text -> parse -> authorize -> verify decision and diagnostics

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Corpus failures expose bugs across multiple layers | High | Medium | Strong unit test coverage in Sprints 001-006 reduces blast radius |
| FsCheck generators incomplete for Cedar syntax | Medium | Low | Use Go fuzz seeds as baseline; expand generators incrementally |
| Corpus extraction adds CI time | Low | Low | Separate test project; can be excluded from quick test runs |

## Security Considerations
- Corpus inputs treated as untrusted: tar.gz extraction in-memory, path-safe, bounded
- Constant folding never evaluates PARC/entity-dependent expressions
- Property tests bounded by FsCheck default parameters

## Dependencies
- Sprint 006 completed
- FsCheck.Xunit (test-only dependency)

## Open Questions
None identified.

# Sprint 001: Bootstrap, Build Infrastructure, and Primitive Values

## Overview
Create the .NET solution structure, build configuration, test infrastructure, and the three primitive Cedar value types. Establishes the foundational patterns (immutability, equality, hashing, Cedar text rendering) that every subsequent sprint builds on.

## Use Cases
1. **Construct primitive values**: Construct `CedarBool`, `CedarLong`, and `CedarString` values with equality, hashing, and Cedar text formatting
2. **Build from clean clone**: Build and test the solution from a clean clone
3. **Shared test helpers**: Assert Cedar value behavior using shared test helpers

## Architecture
- `Cedar.Types.csproj` targeting `net9.0` with nullable enabled, warnings-as-errors
- `CedarValue` abstract base with `Equals(CedarValue)`, `MarshalCedar()`, `GetHashCode()`, `ToString()`
- `Cedar.Tests.csproj` with xUnit and shared `TestSupport/` helpers
- `Directory.Build.props` enforces consistent settings across all projects
- `Directory.Packages.props` centralizes NuGet versions

## Implementation

### Phase 1: Solution scaffolding (~20% effort)

**Files:**
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

### Phase 2: Value base and supporting types (~25% effort)

**Files:**
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

### Phase 3: Primitive value types (~30% effort)

**Files:**
| File | Action | Purpose |
|------|--------|---------|
| `src/Cedar.Types/CedarBool.cs` | Create | Wraps `bool`; `True`/`False` constants; Cedar text: `true`/`false` |
| `src/Cedar.Types/CedarLong.cs` | Create | Wraps `long`; Cedar text: integer literal; deterministic hash |
| `src/Cedar.Types/CedarString.cs` | Create | Wraps `string`; Cedar text: quoted with escaping; stable hash |

### Phase 4: Test infrastructure and tests (~25% effort)

**Files:**
| File | Action | Purpose |
|------|--------|---------|
| `test/Cedar.Tests/TestSupport/CedarAssert.cs` | Create | Typed assertion helpers (equality, hash consistency, Cedar text) |
| `test/Cedar.Tests/Types/CedarBoolTests.cs` | Create | ~8 tests: construction, equality, hashing, Cedar text, ToString |
| `test/Cedar.Tests/Types/CedarLongTests.cs` | Create | ~10 tests: construction, equality, hashing, overflow, Cedar text |
| `test/Cedar.Tests/Types/CedarStringTests.cs` | Create | ~10 tests: construction, equality, hashing, escaping, Cedar text |
| `test/Cedar.Tests/Core/DiagnosticTests.cs` | Create | ~6 tests: construction, empty diagnostics, reason/error collections |

## Files Summary

| File | Action | Purpose |
|------|--------|---------|
| `cedar-dotnet.sln` | Create | Solution file |
| `global.json` | Create | Pin SDK to 9.0.x |
| `Directory.Build.props` | Create | Build settings |
| `Directory.Packages.props` | Create | Central NuGet versions |
| `src/Cedar.Types/Cedar.Types.csproj` | Create | Core types library |
| `src/Cedar.Ast/Cedar.Ast.csproj` | Create | AST library |
| `src/Cedar.Core/Cedar.Core.csproj` | Create | Public API library |
| `test/Cedar.Tests/Cedar.Tests.csproj` | Create | Test project |
| `src/Cedar.Types/CedarValue.cs` | Create | Abstract base record |
| `src/Cedar.Core/Decision.cs` | Create | Decision enum |
| `src/Cedar.Core/Effect.cs` | Create | Effect enum |
| `src/Cedar.Core/Position.cs` | Create | Source position struct |
| `src/Cedar.Core/PolicyId.cs` | Create | Policy identifier struct |
| `src/Cedar.Core/Diagnostic.cs` | Create | Diagnostic record |
| `src/Cedar.Core/DiagnosticReason.cs` | Create | Diagnostic reason record |
| `src/Cedar.Core/DiagnosticError.cs` | Create | Diagnostic error record |
| `src/Cedar.Types/CedarBool.cs` | Create | Boolean value type |
| `src/Cedar.Types/CedarLong.cs` | Create | Long value type |
| `src/Cedar.Types/CedarString.cs` | Create | String value type |
| `test/Cedar.Tests/TestSupport/CedarAssert.cs` | Create | Test assertion helpers |
| `test/Cedar.Tests/Types/CedarBoolTests.cs` | Create | Bool tests |
| `test/Cedar.Tests/Types/CedarLongTests.cs` | Create | Long tests |
| `test/Cedar.Tests/Types/CedarStringTests.cs` | Create | String tests |
| `test/Cedar.Tests/Core/DiagnosticTests.cs` | Create | Diagnostic tests |

## Definition of Done
- [ ] `dotnet build cedar-dotnet.sln` succeeds with zero warnings
- [ ] `dotnet test` passes with **34+ tests** across 5 test files
- [ ] Primitive values demonstrate equality, hash stability, and Cedar text rendering
- [ ] Hash codes are deterministic (FNV-1a or equivalent, not process-randomized)
- [ ] CI-ready: `dotnet restore && dotnet build && dotnet test` works from clean clone

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| CedarValue hierarchy constrains all later types | Medium | High | Keep base minimal; sealed records prevent unintended extension |
| Hash algorithm choice affects perf downstream | Low | Medium | Use FNV-1a (matching Go); benchmark in Sprint 009 |
| Multi-project reference graph complexity | Low | Low | InternalsVisibleTo only where needed; Directory.Build.props enforces consistency |

## Security Considerations
- All types are immutable by construction (sealed records)
- Hash codes are deterministic and not process-randomized
- String rendering escapes Cedar special characters

## Dependencies
- .NET 9.0 SDK
- xUnit 2.9+, Microsoft.NET.Test.Sdk, coverlet.collector

## Open Questions
1. Should `CedarValue.Equals()` use virtual dispatch or pattern matching in the base?
2. Should `PolicyId` support implicit conversion from `string`?

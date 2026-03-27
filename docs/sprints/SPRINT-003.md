# Sprint 003: Internal AST Node Hierarchy and Public Fluent Builder

## Overview
Build the internal AST representation (30+ node types, 6 scope types, policy structure) and the public fluent builder API. This sprint does not parse Cedar text yet — it provides programmatic policy construction and establishes the AST shape consumed by parser and evaluator.

## Use Cases
1. **Programmatic policies**: Construct Cedar policies: `CedarAst.Permit().PrincipalIs("User").When(Resource().Access("owner").Equal(Principal()))`
2. **All expression types**: Build comparisons, arithmetic, logic, collection ops, extension calls, if-then-else, like, is, in, has, tags, isEmpty
3. **Scope constraints**: Construct all 6 scope constraints: All, Eq, In, InSet, Is, IsIn
4. **Annotations**: Attach annotations to policies

## Implementation

### Phase 1: Internal AST nodes (~35% effort)

**Files:**
- `src/Cedar.Ast/Internal/INode.cs` — Interface marker
- `src/Cedar.Ast/Internal/NodeTypes.cs` — All 30+ sealed node records: NodeEquals, NodeNotEquals, NodeLessThan, NodeLessThanOrEqual, NodeGreaterThan, NodeGreaterThanOrEqual, NodeAnd, NodeOr, NodeNot, NodeNegate, NodeAdd, NodeSub, NodeMult, NodeIn, NodeIs, NodeIsIn, NodeHas, NodeHasTag, NodeLike, NodeIfThenElse, NodeAccess, NodeGetTag, NodeContains, NodeContainsAll, NodeContainsAny, NodeIsEmpty, NodeExtensionCall, NodeValue, NodeVariable, NodeRecord, NodeSet
- `src/Cedar.Ast/Internal/ScopeTypes.cs` — Abstract IScope + 6 sealed records: ScopeAll, ScopeEq, ScopeIn, ScopeInSet, ScopeIs, ScopeIsIn
- `src/Cedar.Ast/Internal/PolicyAst.cs` — Full policy AST structure with effect, scopes, conditions, annotations, position

### Phase 2: Public fluent builder (~40% effort)

**Files:**
- `src/Cedar.Ast/CedarAst.cs` — Static entry: `Permit()`, `Forbid()`, `Annotation()`
- `src/Cedar.Ast/PolicyBuilder.cs` — Scope methods, When(), Unless()
- `src/Cedar.Ast/Node.cs` — Public wrapper with fluent operator methods
- `src/Cedar.Ast/Operators.cs` — All operators: Equal, NotEqual, LessThan, And, Or, In, Has, Access, Contains, Like, Is, IsIn, GetTag, HasTag, Add, Sub, Mult, IsEmpty, IfThenElse
- `src/Cedar.Ast/Variables.cs` — Principal(), Action(), Resource(), Context()
- `src/Cedar.Ast/Values.cs` — Boolean(), String(), Long(), Set(), Record(), EntityUid(), IpAddr(), Decimal(), Datetime(), Duration()
- `src/Cedar.Ast/ExtensionOperators.cs` — Decimal comparisons, IP methods, Datetime methods, Duration methods

### Phase 3: AST tests (~25% effort)

**Files:**
- 5 test files: NodeTypeTests, ScopeTests, PolicyBuilderTests, OperatorTests, VariableAndValueTests
- ~65 tests covering all node types, scopes, fluent builders, extension operators

## Files Summary

| File | Action | Purpose |
|------|--------|---------|
| `src/Cedar.Ast/Internal/INode.cs` | Create | Interface marker |
| `src/Cedar.Ast/Internal/NodeTypes.cs` | Create | 30+ AST node records |
| `src/Cedar.Ast/Internal/ScopeTypes.cs` | Create | 6 scope type records |
| `src/Cedar.Ast/Internal/PolicyAst.cs` | Create | Policy AST structure |
| `src/Cedar.Ast/CedarAst.cs` | Create | Static entry point |
| `src/Cedar.Ast/PolicyBuilder.cs` | Create | Fluent builder |
| `src/Cedar.Ast/Node.cs` | Create | Public node wrapper |
| `src/Cedar.Ast/Operators.cs` | Create | All operators |
| `src/Cedar.Ast/Variables.cs` | Create | PARC variables |
| `src/Cedar.Ast/Values.cs` | Create | Value constructors |
| `src/Cedar.Ast/ExtensionOperators.cs` | Create | Extension operators |

## Definition of Done
- [ ] `dotnet test` passes with **227+ tests** across 21 test files
- [ ] All 30+ AST node types constructable and inspectable
- [ ] All 6 scope types constructable
- [ ] Fluent builder can reproduce Go's README example as a C# expression
- [ ] Builder covers 25+ operator categories

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Internal AST too Go-shaped for idiomatic C# | Medium | Medium | Use C# record inheritance, not Go-style embedding |
| Public builder API changes after parser/eval consume it | Low | High | Keep builder thin — delegates to internal nodes |

## Security Considerations
- AST nodes are immutable records — no mutation after construction

## Dependencies
- Sprint 002 completed (value types needed for NodeValue, EntityUid in scopes)

## Open Questions
None identified.

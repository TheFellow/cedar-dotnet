# Sprint 006: Evaluation Engine, Extension Functions, and Authorization

## Overview
Port the evaluation engine: compile AST -> evaluator tree, evaluate against PARC environment, produce values or errors. Build `Authorize()` with fail-safe decision logic. Covers all core operators and 23+ extension functions. After this sprint, the library can authorize requests.

## Use Cases
1. **Compile policies**: Compile AST policies into evaluator trees
2. **Evaluate expressions**: Evaluate all expression types against PARC environment
3. **Extension functions**: Call 23+ Cedar extension functions (decimal, IP, datetime, duration)
4. **Authorize requests**: Run full authorization with fail-safe decision logic
5. **Error handling**: Collect evaluation errors in diagnostics without crashing

## Implementation

### Phase 1: Evaluation environment and type conversion (~15% effort)

**Files:**
- `src/Cedar.Core/Internal/Eval/EvalEnv.cs` — record EvalEnv(IEntityGetter, CedarValue Principal/Action/Resource/Context)
- `src/Cedar.Core/Internal/Eval/IEvaluator.cs` — interface IEvaluator { CedarValue Eval(EvalEnv); }
- `src/Cedar.Core/Internal/Eval/BoolEvaluator.cs` — Wraps IEvaluator, ensures boolean result
- `src/Cedar.Core/Internal/Eval/TypeConversion.cs` — ValueToBool, ValueToLong, ValueToString, ValueToSet, ValueToRecord, ValueToEntity, ValueToDecimal, ValueToDatetime, ValueToDuration, ValueToIp
- `src/Cedar.Core/Internal/Eval/EvalErrors.cs` — Sentinel errors

### Phase 2: Core evaluators (~30% effort)

**Files:**
- `src/Cedar.Core/Internal/Eval/Evaluators/LiteralEvaluator.cs`
- `src/Cedar.Core/Internal/Eval/Evaluators/VariableEvaluator.cs`
- `src/Cedar.Core/Internal/Eval/Evaluators/LogicalEvaluators.cs` — And (short-circuit), Or (short-circuit), Not
- `src/Cedar.Core/Internal/Eval/Evaluators/ComparisonEvaluators.cs` — Equal, NotEqual, LT, LTE, GT, GTE
- `src/Cedar.Core/Internal/Eval/Evaluators/ArithmeticEvaluators.cs` — Add, Sub, Mult, Negate (overflow detection)
- `src/Cedar.Core/Internal/Eval/Evaluators/CollectionEvaluators.cs` — Contains, ContainsAll, ContainsAny, IsEmpty, SetLiteral, RecordLiteral
- `src/Cedar.Core/Internal/Eval/Evaluators/MembershipEvaluators.cs` — In (hierarchy traversal), Is, IsIn
- `src/Cedar.Core/Internal/Eval/Evaluators/AccessEvaluators.cs` — AttributeAccess, Has (records + entities)
- `src/Cedar.Core/Internal/Eval/Evaluators/TagEvaluators.cs` — GetTag, HasTag
- `src/Cedar.Core/Internal/Eval/Evaluators/PatternEvaluators.cs` — Like
- `src/Cedar.Core/Internal/Eval/Evaluators/ConditionalEvaluator.cs` — IfThenElse
- `src/Cedar.Core/Internal/Eval/Evaluators/ExtensionEvaluator.cs` — Dispatches to registry

### Phase 3: Extension function registry (~15% effort)

**Files:**
- `src/Cedar.Core/Internal/Extensions/ExtensionRegistry.cs` — 23 entries: name -> arity + isMethod + implementation
- `src/Cedar.Core/Internal/Extensions/DecimalExtensions.cs` — lessThan, lessThanOrEqual, greaterThan, greaterThanOrEqual
- `src/Cedar.Core/Internal/Extensions/IpAddressExtensions.cs` — isIpv4, isIpv6, isLoopback, isMulticast, isInRange
- `src/Cedar.Core/Internal/Extensions/DatetimeExtensions.cs` — toDate, toTime, offset, durationSince
- `src/Cedar.Core/Internal/Extensions/DurationExtensions.cs` — toDays, toHours, toMinutes, toSeconds, toMilliseconds
- `src/Cedar.Core/Internal/Extensions/ConstructorExtensions.cs` — ip(), decimal(), datetime(), duration()

### Phase 4: Compiler (~15% effort)

**Files:**
- `src/Cedar.Core/Internal/Eval/Compiler.cs` — Compile(PolicyAst) -> BoolEvaluator; ToEval(INode) -> IEvaluator (switch dispatch over 30+ node types)
- `src/Cedar.Core/Internal/Eval/ScopeCompiler.cs` — Compile scopes into node expressions

### Phase 5: Public authorization API (~10% effort)

**Files:**
- `src/Cedar.Core/Authorization.cs` — `static (Decision, Diagnostic) Authorize(IPolicyIterator, IEntityGetter, Request)`

### Phase 6: Tests (~15% effort)

**Files:**
- 6 test files: TypeConversionTests (~20), EvaluatorTests (~50), ExtensionTests (~30), CompilerTests (~15), AuthorizeTests (~25), DiagnosticTests (~10)
- ~150 tests total

## Files Summary

| File | Action | Purpose |
|------|--------|---------|
| `src/Cedar.Core/Internal/Eval/EvalEnv.cs` | Create | Evaluation environment |
| `src/Cedar.Core/Internal/Eval/IEvaluator.cs` | Create | Evaluator interface |
| `src/Cedar.Core/Internal/Eval/BoolEvaluator.cs` | Create | Boolean evaluator wrapper |
| `src/Cedar.Core/Internal/Eval/TypeConversion.cs` | Create | Type conversion utilities |
| `src/Cedar.Core/Internal/Eval/EvalErrors.cs` | Create | Sentinel errors |
| `src/Cedar.Core/Internal/Eval/Evaluators/*.cs` | Create | 12 evaluator files |
| `src/Cedar.Core/Internal/Extensions/ExtensionRegistry.cs` | Create | Extension function registry |
| `src/Cedar.Core/Internal/Extensions/*.cs` | Create | 5 extension implementation files |
| `src/Cedar.Core/Internal/Eval/Compiler.cs` | Create | AST -> evaluator compiler |
| `src/Cedar.Core/Internal/Eval/ScopeCompiler.cs` | Create | Scope compiler |
| `src/Cedar.Core/Authorization.cs` | Create | Public authorization API |

## Definition of Done
- [ ] `dotnet test` passes with **551+ tests** across 40 test files
- [ ] All 30+ evaluator types functional and tested
- [ ] All 23 extension functions produce results identical to Go
- [ ] Authorization: any forbid -> Deny, permits + no forbids -> Allow, no matches -> Deny
- [ ] `&&`/`||` short-circuit correctly (errors on short-circuited branch suppressed)
- [ ] Missing entities -> evaluation error, not crash or implicit allow
- [ ] Arithmetic overflow detected and reported

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Extension function type mismatch semantics | High | High | Port Go's error behavior exactly; test each extension with wrong types |
| Entity hierarchy cycles | Low | Medium | Assume DAG (matching Go); document limitation |
| Short-circuit error suppression | Medium | High | Add explicit tests: error on left + false on right, etc. |

## Security Considerations
- Authorization is fail-safe: any forbid wins; default deny
- Type errors -> diagnostic errors, not process crashes
- Missing entities -> evaluation errors, never implicit allows
- Arithmetic overflow detected
- Extension functions enforce strict arity and type checks
- Evaluation depth bounded

## Dependencies
- Sprint 005 completed

## Open Questions
None identified.

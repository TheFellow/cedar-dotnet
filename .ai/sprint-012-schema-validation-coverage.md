# Sprint 012 -- Schema Validation Test Coverage

## Objective

Close the four identified gaps in schema validation unit tests. Every new test in this sprint must target a concrete code path that currently has zero prior coverage.

Total planned additions: approximately 73 tests.

## Context

The target gaps are concentrated in four areas:

- `TypeChecker` extension-function validation and boolean operator recovery branches
- `SchemaValidator` entity validation and request validation edge cases
- `ValueChecker` record, set, entity, primitive, and extension edge cases

Existing test style to follow:

- `test/Cedar.Schema.Tests/TypeCheckerTests.cs` uses local helpers such as `Check()`, `Call()`, `Str()`, `Long()`, `Var()`, `Access()`, `Bool()`, and `Entity()`
- `test/Cedar.Schema.Tests/SchemaValidatorEntityTests.cs` and `test/Cedar.Schema.Tests/SchemaValidatorRequestTests.cs` keep schemas inline and assert directly on `ValidationResult`
- `test/Cedar.Schema.Tests/ValueCheckerTests.cs` already exists, so Phase 4 should extend that file rather than create a new one

## Scope

- Add the missing unit tests only
- Do not add TODO tests, placeholder assertions, or speculative scenarios
- Do not duplicate already-covered decimal constructor tests
- Do not create new source files except if Phase 4 later proves `ValueCheckerTests.cs` unusable, which is not expected here

## Deliberate Exclusions

- Do not duplicate existing decimal coverage for invalid literal, wrong argument type, or strict-mode non-literal constructor calls
- Do not add separate `isIpv6`, `isLoopback`, or `isMulticast` tests because `isIpv4` already covers the shared `(ipaddr) -> Bool` signature shape
- Do not add `toTime` coverage in this sprint because `toDate` already covers the same argument-shape branch

## Authoring Notes

- Keep the existing `MethodUnderTest_Scenario` naming convention
- Prefer one assertion target per test, except when the whole point is verifying multiple diagnostics
- For tests that should produce two diagnostics, assert that both expected diagnostics are present rather than only checking the first message
- For `TypeChecker` tests, assert both the resulting type and the expected error behavior
- For `SchemaValidator` tests, keep schemas minimal and tailor each schema to one behavior branch
- For `ValueChecker` tests, assert both `isDeserError` and the returned error text when the branch distinguishes shape errors from conformance errors

## Phase 1 -- TypeChecker Extension Function Validation

File: `test/Cedar.Schema.Tests/TypeCheckerTests.cs`

Target code paths:

- `TypeOfExtensionCall`
- `ValidateExtensionValue`
- strict-mode constructor guard in `TypeChecker`

Planned additions: 24 tests

### 1a. Constructor invalid-literal paths (3 tests)

- `TypeOfExpr_IpConstructorRejectsInvalidLiteralValue`
  Expression: `Call("ip", Str("not-an-ip"))`
  Assert type `ipaddr`
  Assert parse failure message for IP address
- `TypeOfExpr_DatetimeConstructorRejectsInvalidLiteralValue`
  Expression: `Call("datetime", Str("not-a-datetime"))`
  Assert type `datetime`
  Assert parse failure message for datetime
- `TypeOfExpr_DurationConstructorRejectsInvalidLiteralValue`
  Expression: `Call("duration", Str("not-a-duration"))`
  Assert type `duration`
  Assert parse failure message for duration

### 1b. Constructor wrong-argument-type (3 tests)

- `TypeOfExpr_IpConstructorRejectsNonStringArgument`
  Expression: `Call("ip", Long(1))`
  Assert message `expected String but saw Long`
- `TypeOfExpr_DatetimeConstructorRejectsNonStringArgument`
  Expression: `Call("datetime", Long(1))`
  Assert same argument-type diagnostic
- `TypeOfExpr_DurationConstructorRejectsNonStringArgument`
  Expression: `Call("duration", Long(1))`
  Assert same argument-type diagnostic

### 1c. Utility function wrong-argument-type (11 tests)

- `TypeOfExpr_IsIpv4RejectsNonIpAddrArgument`
  Expression: `Call("isIpv4", Call("decimal", Str("1.0")))`
  Assert type `Bool`
  Assert message `expected ipaddr but saw decimal`
- `TypeOfExpr_IsInRangeRejectsNonIpAddrArguments`
  Expression: `Call("isInRange", Str("x"), Str("y"))`
  Assert type `Bool`
  Assert two argument-type diagnostics
- `TypeOfExpr_ToDateRejectsNonDatetimeArgument`
  Expression: `Call("toDate", Call("duration", Str("1d")))`
  Assert `expected datetime but saw duration`
- `TypeOfExpr_OffsetRejectsNonDatetimeFirstArgument`
  Expression: `Call("offset", Call("duration", Str("1d")), Call("duration", Str("1d")))`
  Assert exactly one diagnostic for argument 0 shape
- `TypeOfExpr_OffsetRejectsNonDurationSecondArgument`
  Expression: `Call("offset", Call("datetime", Str("2024-01-01")), Call("datetime", Str("2024-01-01")))`
  Assert exactly one diagnostic for argument 1 shape
- `TypeOfExpr_DurationSinceRejectsNonDatetimeArguments`
  Expression: `Call("durationSince", Call("duration", Str("1d")), Call("duration", Str("1d")))`
  Assert two datetime expectation diagnostics
- `TypeOfExpr_ToDaysRejectsNonDurationArgument`
  Expression: `Call("toDays", Call("datetime", Str("2024-01-01")))`
  Assert `expected duration but saw datetime`
- `TypeOfExpr_ToHoursRejectsNonDurationArgument`
  Expression: `Call("toHours", Str("x"))`
  Assert `expected duration but saw String`
- `TypeOfExpr_ToMinutesRejectsNonDurationArgument`
  Expression: `Call("toMinutes", Long(1))`
  Assert `expected duration but saw Long`
- `TypeOfExpr_ToSecondsRejectsNonDurationArgument`
  Expression: `Call("toSeconds", Long(1))`
  Assert same duration expectation pattern
- `TypeOfExpr_ToMillisecondsRejectsNonDurationArgument`
  Expression: `Call("toMilliseconds", Long(1))`
  Assert same duration expectation pattern

### 1d. Decimal comparison wrong types (4 tests)

- `TypeOfExpr_DecimalLessThanRejectsNonDecimalArgument`
  Expression: `Call("lessThan", Str("x"), Call("decimal", Str("1.0")))`
  Assert `expected decimal but saw String`
- `TypeOfExpr_DecimalLessThanOrEqualRejectsNonDecimalArgument`
  Same shape with `lessThanOrEqual`
- `TypeOfExpr_DecimalGreaterThanRejectsNonDecimalArgument`
  Same shape with `greaterThan`
- `TypeOfExpr_DecimalGreaterThanOrEqualRejectsNonDecimalArgument`
  Same shape with `greaterThanOrEqual`

### 1e. Constructor non-literal in strict mode (3 tests)

- `TypeOfExpr_IpConstructorRejectsNonLiteralExpressionInStrictMode`
- `TypeOfExpr_DatetimeConstructorRejectsNonLiteralExpressionInStrictMode`
- `TypeOfExpr_DurationConstructorRejectsNonLiteralExpressionInStrictMode`

Use the same pattern as the existing decimal strict-mode test and assert `extension constructors may not be called with non-literal expressions`.

## Phase 2 -- TypeChecker Boolean Operator Error Recovery

File: `test/Cedar.Schema.Tests/TypeCheckerTests.cs`

Target code paths:

- `TypeOfAnd`
- `TypeOfOr`
- `TypeOfIfThenElse`
- skipped-branch entity-reference validation through `ValidateEntityRefs`

Planned additions: 10 tests

- `TypeOfExpr_AndWithFalseLeftSkipsRightBranchTypeChecking`
  Cover the `leftType is CedarFalseType` short-circuit branch
- `TypeOfExpr_AndWithErrorLeftAndFalseRightReturnsFalse`
  Cover the `leftErrors.Count > 0` path where the right branch evaluates to `False`
- `TypeOfExpr_AndWithTrueLeftReturnsRightType`
  Cover the `leftType is CedarTrueType` return path
- `TypeOfExpr_OrWithTrueLeftShortCircuitsRight`
  Cover the `leftType is CedarTrueType` short-circuit branch
- `TypeOfExpr_OrWithFalseLeftReturnsRightType`
  Cover the `leftType is CedarFalseType` return-right branch
- `TypeOfExpr_OrWithBothFalseReturnsFalse`
  Cover the `rightType is CedarFalseType` branch
- `TypeOfExpr_OrWithErrorLeftPropagatesErrors`
  Cover the `leftErrors.Count > 0` branch in `TypeOfOr`
- `TypeOfExpr_IfThenElseWithFalseConditionSkipsThenBranch`
  Cover the `conditionType is CedarFalseType` branch and `ValidateEntityRefs(node.Then)`
- `TypeOfExpr_IfThenElseWithTrueConditionSkipsElseBranch`
  Cover the `conditionType is CedarTrueType` branch and `ValidateEntityRefs(node.Else)`
- `TypeOfExpr_IfThenElseWithNonBoolConditionReportsError`
  Cover the invalid-condition branch that still types both arms and returns their LUB when possible

Implementation note:

- Use deliberately-invalid entity references in skipped branches where useful, so the test proves the branch only runs entity-reference validation and does not fully type-check the skipped expression

## Phase 3 -- SchemaValidator Entity and Request Validation

Files:

- `test/Cedar.Schema.Tests/SchemaValidatorEntityTests.cs`
- `test/Cedar.Schema.Tests/SchemaValidatorRequestTests.cs`

Target code paths:

- `ValidateEntity`
- `ValidateEntities`
- `ValidateActionEntity`
- `ValidateRegularEntity`
- `ValidateRequest`
- `ValidateRequestEntityType`

Planned additions: 22 tests in the reviewed plan, with 21 named cases currently enumerated below

Plan note:

- The reviewed plan labels this phase as 22 tests total and `Entity tests (14)`, but the supplied entity list names 13 concrete cases and the request list names 8 concrete cases
- This sprint document preserves the explicitly named cases and flags the mismatch instead of inventing a new test

### Entity tests (13 named cases in the reviewed plan)

- `ValidateEntity_RejectsActionWithUnexpectedParent`
  Cover the unexpected-parent branch in `ValidateActionEntity`
- `ValidateEntity_RejectsActionMissingExpectedParent`
  Cover the missing-parent branch in `ValidateActionEntity`
- `ValidateEntity_RejectsActionEntityWithAttributes`
  Cover the action-attributes rejection branch
- `ValidateEntity_RejectsActionEntityWithTags`
  Cover the action-tags rejection branch
- `ValidateEntity_RejectsEntityWithTagsWhenSchemaForbidsTags`
  Cover the regular-entity no-tags branch with prefixed deserialization error
- `ValidateEntity_AcceptsEntityWithValidTags`
  Cover the successful tag-validation path
- `ValidateEntity_RejectsEntityWithWrongTagValueType`
  Cover `ValueChecker.CheckValue` failure while validating tags
- `ValidateEntity_RejectsInvalidParentType`
  Cover invalid regular-entity parent type rejection
- `ValidateEntity_AcceptsEnumEntityType`
  Cover the enum fast-path in `ValidateEntity` if schema enums are available in the current parser form
- `ValidateEntity_RejectsUnknownEntityType`
  Cover the fallback unknown-type failure
- `ValidateEntity_RejectsActionNotFoundInSchema`
  Use a valid `Action` entity type with an unknown ID to cover the action lookup failure
- `ValidateEntities_ReturnsDeserializationErrorMessage`
  Cover `ValidateEntities` aggregate failure when the first entity error is prefixed with `[deser] `
- `ValidateEntities_ReturnsSchemaConformanceErrorMessage`
  Cover `ValidateEntities` aggregate failure when the first entity error is a schema-conformance error

### Request tests (8)

- `ValidateRequest_RejectsUnknownPrincipalType`
  Cover `ValidateRequestEntityType` for principal
- `ValidateRequest_RejectsInvalidPrincipalTypeForAction`
  Cover applicability rejection after principal type is known
- `ValidateRequest_RejectsUnknownResourceType`
  Cover `ValidateRequestEntityType` for resource
- `ValidateRequest_RejectsInvalidResourceTypeForAction`
  Cover applicability rejection after resource type is known
- `ValidateRequest_RejectsContextWithUnexpectedAttribute`
  Cover record unexpected-attribute rejection through request validation
- `ValidateRequest_RejectsContextWithWrongAttributeType`
  Cover record attribute type mismatch through request validation
- `ValidateRequest_AcceptsNullContextWhenSchemaHasEmptyContext`
  Cover `request.Context ?? new CedarRecord()` with an empty schema context
- `ValidateRequest_RejectsContextWithMissingRequiredAttribute`
  Cover record missing-required-attribute rejection through request validation

Implementation notes:

- Keep request schemas minimal so a failing assertion points to one branch only
- For context failures, assert the high-level request error string rather than internal `ValueChecker` details because `ValidateRequest` intentionally collapses those details

## Phase 4 -- ValueChecker Edge Cases

File: `test/Cedar.Schema.Tests/ValueCheckerTests.cs`

Target code paths:

- `CheckValue`
- `CheckRecord`
- `CheckSet`
- `CheckEntityValue`
- `CheckExtensionValue`
- generic `CheckExtensionValue<TExtension>`

Planned additions: 17 tests

### Record validation

- `CheckRecord_ReportsFirstMissingRequiredAmongMultiple`
- `CheckRecord_ReportsFirstUnexpectedAmongMultiple`
- `CheckRecord_ReportsMissingBeforeUnexpected`
- `CheckRecord_AcceptsOptionalMissingAttribute`
- `CheckRecord_ReportsNestedRecordTypeError`
- `CheckRecord_ReportsAttributeTypeError`

### Primitive, set, and entity validation

- `CheckValue_AcceptsMatchingLong`
- `CheckValue_AcceptsMatchingBool`
- `CheckValue_RejectsPrimitiveMismatch`
- `CheckSet_RejectsNonSetValue`
- `CheckSet_RejectsElementTypeMismatch`
- `CheckEntityValue_RejectsNonEntityUid`
- `CheckEntityValue_RejectsWrongEntityType`

### Extension validation

- `CheckExtensionValue_AcceptsMatchingDatetime`
- `CheckExtensionValue_AcceptsMatchingDuration`
- `CheckExtensionValue_RejectsDatetimeWhenDurationExpected`
- `CheckExtensionValue_UnknownExtensionTypeReturnsNoError`

Implementation notes:

- The record tests should verify ordering semantics in `CheckRecord`: required-attribute scan first, unexpected-attribute scan second
- The extension mismatch test must distinguish extension-to-extension conformance errors from raw deserialization shape errors
- The unknown-extension test should target the default switch arm in `CheckExtensionValue`

## Definition of Done

1. All planned new tests from this sprint document are added to the existing schema test files.
2. `dotnet build cedar-dotnet.sln` succeeds with zero warnings.
3. `dotnet test cedar-dotnet.sln` succeeds with zero failures.
4. Every new test exercises a code path that previously had no coverage.
5. No TODOs, placeholders, or skipped tests are introduced.
6. Every test follows the existing `MethodUnderTest_Scenario` naming convention.
7. No new source files are created except `ValueCheckerTests.cs` only if absolutely necessary. Current expectation: extend the existing file.

## Execution Order

1. Phase 1 in `TypeCheckerTests.cs`
2. Phase 2 in `TypeCheckerTests.cs`
3. Phase 3 and Phase 4 in parallel

After each phase:

- run the full test suite
- keep the tree green before moving to the next phase

## Verification Gates

Use these commands after each phase and again at sprint completion:

```bash
dotnet build cedar-dotnet.sln
dotnet test cedar-dotnet.sln
```

## Exit Criteria

The sprint is complete only when the planned tests are implemented, all validation branches above are exercised, and the solution build and test commands pass cleanly after every phase.

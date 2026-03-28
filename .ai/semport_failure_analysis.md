# Semport Failure Analysis

## Verdict: Pre-Existing Failures — Port is Clean

The 3 test failures in `Cedar.Tests` are **not caused by the `PolicySet.Map()` port** (commit `5876726`). They are pre-existing failures in the Cedar parser's extension function call handling. The port itself is correct and complete.

---

## Failure Artifacts

| Artifact | Location |
|---|---|
| Validation report | `.ai/semport_validation_report.md` |
| Failing test project | `test/Cedar.Tests/` |
| Parser source | `src/Cedar.Core/Internal/Parser/ExpressionParser.cs` |
| Extension registry | `src/Cedar.Core/Internal/Extensions/ExtensionRegistry.cs` |

---

## Root Cause Analysis

### Failing Tests (all 3 in the same subsystem)

| Test | Input | Error |
|---|---|---|
| `ParserTests.ParseExtensionFunctionCall` | `myFunc(1, true)` | `'myFunc' is not a function` |
| `CedarWriterTests.WriteExtensionCall` | `f(1, true)` | `'f' is not a function` |
| `RoundTripTests.ParseWriteParseIsStable` | `ext(1, true, "x")` | `'ext' is not a function` |

### Root Cause: Closed Extension Registry

**File:** `src/Cedar.Core/Internal/Parser/ExpressionParser.cs`, line ~338–341

```csharp
if (_state.Match(TokenType.LParen))
{
    if (!ExtensionRegistry.TryGet(token.Text, out ExtensionDefinition functionDefinition))
    {
        throw _state.Error(token, $"`{token.Text}` is not a function");  // line 339
    }
    ...
```

**File:** `src/Cedar.Core/Internal/Extensions/ExtensionRegistry.cs`

The `ExtensionRegistry` is a closed, hard-coded dictionary of known extension functions:
```
decimal, ip, datetime, duration, lessThan, lessThanOrEqual, greaterThan,
greaterThanOrEqual, isIpv4, isIpv6, isLoopback, isMulticast, isInRange,
toDate, toTime, offset, durationSince, daysInMonth, year, month, day,
dayOfWeek, dayOfYear, hour, minute, second, millisecond, toDays, toHours,
toMinutes, toSeconds, toMilliseconds
```

The test inputs `myFunc`, `f`, and `ext` are **not** in this registry. The parser rejects them at parse-time with `` `X` is not a function ``.

### Why the Tests Exist

The tests represent a design intent that the parser should accept **arbitrary extension function calls** — or at least user-defined ones — without requiring prior registration. In Cedar's specification, extension functions are namespace-qualified but the parser should not be required to know the full extension catalogue at parse time to build an AST.

The Cedar Go parser accepts any identifier followed by `(` as a potential extension function call without validating against a registry. Our C# parser validates eagerly at parse time, which is more restrictive than the spec requires.

---

## Impact Assessment

| Area | Status |
|---|---|
| This port (`PolicySet.Map`) | ✅ Unaffected — fully passing |
| Cedar.Schema.Tests (103 tests) | ✅ All pass |
| Cedar.Batch.Tests (16 tests) | ✅ All pass |
| Cedar.Experimental.Tests (28 tests) | ✅ All pass |
| Cedar.Tests (959/962 pass) | ❌ 3 pre-existing parser failures |

The 3 failures represent a known semantic gap between the C# parser and Cedar's specification: **the parser must not require runtime extension registration to parse valid Cedar policies**.

---

## What Needs to Be Fixed (Separate Work Item)

This is NOT part of commit `5876726`. It should be tracked as its own work item.

### Fix Required

**File:** `src/Cedar.Core/Internal/Parser/ExpressionParser.cs` (~line 334–349)

Replace the hard registry guard with an unconditional parse, deferring validation to eval time:

```csharp
// CURRENT (too restrictive):
if (_state.Match(TokenType.LParen))
{
    if (!ExtensionRegistry.TryGet(token.Text, out ExtensionDefinition functionDefinition))
        throw _state.Error(token, $"`{token.Text}` is not a function");
    if (functionDefinition.IsMethod)
        throw _state.Error(token, $"`{token.Text}` is a method, not a function");
    ImmutableArray<INode> args = ParseExpressionList(TokenType.RParen, "...");
    return new NodeExtensionCall(new CedarPath(token.Text), args);
}

// DESIRED (spec-compliant): accept any identifier as a function call, validate at eval time
if (_state.Match(TokenType.LParen))
{
    ImmutableArray<INode> args = ParseExpressionList(TokenType.RParen, "Expected ')' after function arguments.");
    return new NodeExtensionCall(new CedarPath(token.Text), args);
}
```

**Risk:** Removes parse-time validation for misspelled known extension names (e.g., `decimalX(...)` would parse successfully but fail at eval). This matches Cedar's actual design intent.

**Affected tests that would start passing:**
- `ParserTests.ParseExtensionFunctionCall` (line 423)
- `CedarWriterTests.WriteExtensionCall` (line 159)
- `RoundTripTests.ParseWriteParseIsStable` (ext variant, line 40 of test data)

---

## Semport Disposition for Commit 5876726

The port is complete and correct. The pre-existing failures do not block marking this commit as `implemented`.

**Recommended next action:** Run `python3 semport/ledger.py update 5876726 implemented && python3 semport/ledger.py sort` and commit.

# Semport Failure Analysis — a94e3e2 Port

## Overall Verdict
**The 3 test failures are pre-existing defects unrelated to the `Position` JSON port.**
The `Position` port itself is correct — `Position_JsonRoundTrip_UsesLowercaseKeys` passed.

---

## Failing Tests (3)

All three failures share the same root cause and stack trace.

| Test | File | Approximate Line |
|------|------|-----------------|
| `Cedar.Tests.Parser.ParserTests.ParseExtensionFunctionCall` | `test/Cedar.Tests/Parser/ParserTests.cs` | ~423 |
| `Cedar.Tests.Parser.CedarWriterTests.WriteExtensionCall` | `test/Cedar.Tests/Parser/CedarWriterTests.cs` | ~159 |
| `Cedar.Tests.Parser.RoundTripTests.ParseWriteParseIsStable` (ext case) | `test/Cedar.Tests/Parser/RoundTripTests.cs` | ~40 |

---

## Root Cause

### The Parser's Closed Extension Registry

**Failure artifact location:** `src/Cedar.Core/Internal/Parser/ExpressionParser.cs`, line 337–339

```csharp
if (_state.Match(TokenType.LParen))
{
    if (!ExtensionRegistry.TryGet(token.Text, out ExtensionDefinition functionDefinition))
    {
        throw _state.Error(token, $"`{token.Text}` is not a function");  // line 339
    }
    ...
}
```

**The problem:** When the parser encounters any identifier followed by `(`, it looks up the name in the `ExtensionRegistry`. That registry (`src/Cedar.Core/Internal/Extensions/ExtensionRegistry.cs`, lines 10–43) is a **closed, hardcoded whitelist** of exactly four constructor functions (`decimal`, `ip`, `datetime`, `duration`) and a fixed set of method extensions.

The three failing tests use arbitrary/synthetic function names that are not in the registry:
- `myFunc(1, true)` — `ParseExtensionFunctionCall` test
- `f(1, true)` — `WriteExtensionCall` test
- `ext(1, true, "x")` — `RoundTripTests.ParseWriteParseIsStable` theory case

None of these names exist in `ExtensionRegistry`, so the parser throws `"is not a function"` at parse time.

---

## Why This Is Pre-Existing (Not a Regression)

1. `git diff HEAD` shows **zero changes** to `ExpressionParser.cs` — it was not touched by this port.
2. `git log --oneline` shows the most recent commits are all `semport: acknowledge` entries; no parser changes have been made.
3. The `Position` port only modified:
   - `src/Cedar.Core/Position.cs` (added `[JsonPropertyName]` attributes)
   - `test/Cedar.Tests/Policy/PolicyTests.cs` (added one new `[Fact]`)
4. These files have no connection to `ExpressionParser.cs` or `ExtensionRegistry.cs`.

---

## Semantic Analysis of the Underlying Bug

The Cedar language spec treats extension functions as **open** — user-defined extension functions are valid Cedar syntax when parsed for policy representation (parse → AST), even if the runtime has no evaluator for them. The `CedarWriter` and `PolicyAst` already have a `NodeExtensionCall` node type and can represent arbitrary extension calls. The writer (`CedarWriter.cs:313`) already handles unknown extension names gracefully.

**The parser is the only place that enforces the closed whitelist at parse time**, which is architecturally inconsistent: the AST and writer support arbitrary extension calls, but the parser refuses to create them.

The correct behavior is: **parse any `ident(...)` as a `NodeExtensionCall` unconditionally**; reject unknown functions only at evaluation time (as a type/eval error), not at parse time.

---

## Impacted Files

| File | Lines | Issue |
|------|-------|-------|
| `src/Cedar.Core/Internal/Parser/ExpressionParser.cs` | 335–349 | Registry lookup at parse time should be removed; `NodeExtensionCall` should be emitted for any `ident(...)` |
| `src/Cedar.Core/Internal/Extensions/ExtensionRegistry.cs` | 10–43 | Registry itself is fine for evaluation; should not gate parsing |
| `src/Cedar.Core/Internal/Parser/ExpressionParser.cs` | 393–412 | Same issue for method-style calls (`lhs.method(...)`) — also gated by registry at parse time |

---

## What Needs to Be Fixed

### Fix (parser)
In `ExpressionParser.cs`, remove the `ExtensionRegistry.TryGet` guard from `ParsePrimary` (lines 337–339) and emit `NodeExtensionCall` unconditionally for any `ident(...)` form.

Before (lines 335–348):
```csharp
if (_state.Match(TokenType.LParen))
{
    if (!ExtensionRegistry.TryGet(token.Text, out ExtensionDefinition functionDefinition))
    {
        throw _state.Error(token, $"`{token.Text}` is not a function");
    }

    if (functionDefinition.IsMethod)
    {
        throw _state.Error(token, $"`{token.Text}` is a method, not a function");
    }

    ImmutableArray<INode> args = ParseExpressionList(TokenType.RParen, "Expected ')' after function arguments.");
    return new NodeExtensionCall(new CedarPath(token.Text), args);
}
```

After:
```csharp
if (_state.Match(TokenType.LParen))
{
    ImmutableArray<INode> args = ParseExpressionList(TokenType.RParen, "Expected ')' after function arguments.");
    return new NodeExtensionCall(new CedarPath(token.Text), args);
}
```

The evaluation layer (`ExtensionEvaluator.cs`) and constant folder (`ConstantFolder.cs:174`) already handle unknown functions at eval time.

### No changes needed to the port itself
The `Position` JSON port is complete and correct. The 3 failures are entirely orthogonal.

---

## Recommended Action

This pre-existing parser bug should be tracked separately from the semport pipeline. Options:
1. **Fix it now** as a standalone bug fix commit before continuing the semport pipeline.
2. **Leave it** and accept 3 pre-existing failures — the semport pipeline's `FAIL` status will always be misleading until resolved.

Recommendation: **Fix it** — it is a small, isolated change that unblocks accurate pass/fail signaling for future semport cycles.

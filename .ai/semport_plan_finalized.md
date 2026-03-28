# Finalized Port Plan: a12ba1d — add feather: trailing commas

## Status Assessment

After inspecting the C# codebase, the three Go methods have the following C# equivalents:

| Go method | C# equivalent | File | Lines | Already supports trailing commas? |
|---|---|---|---|---|
| `entlist()` — scope `in [...]` | `ScopeParser.ParseScopeConstraint` entity-set loop | `src/Cedar.Core/Internal/Parser/ScopeParser.cs` | 27–46 | ✅ YES |
| `entlist()` — expression `in [...]` | `ExpressionParser.ParseSetLiteral` (via `ParseAdd → ParsePrimary`) | `src/Cedar.Core/Internal/Parser/ExpressionParser.cs` | 415–441 | ✅ YES |
| `expressions()` | `ExpressionParser.ParseExpressionList` | `src/Cedar.Core/Internal/Parser/ExpressionParser.cs` | 495–520 | ✅ YES |
| `record()` | `ExpressionParser.ParseRecordLiteral` | `src/Cedar.Core/Internal/Parser/ExpressionParser.cs` | 445–490 | ✅ YES |

All four methods already use the same pattern the Go commit introduced:
```
// after appending item:
if (Match(Comma))
{
    if (Match(endToken)) break;   // trailing comma → stop
    continue;
}
Expect(endToken, message);       // no comma → must be end
break;
```

This means the C# parser **already supports trailing commas** in all three constructs targeted by Go commit `a12ba1d`. The semantic change is already present.

## What IS needed: Tests

The C# codebase has one trailing-comma test (`ParseTrailingCommaInScopeTuple` at `test/Cedar.Tests/Parser/ParserTests.cs:513`) covering only the scope tuple case. The three new cases from the Go commit are **not tested**.

### Missing test coverage

File: `test/Cedar.Tests/Parser/ParserTests.cs`  
Class: `ParserTests` (line ~1, find the class boundary)  
Insert after the existing `ParseTrailingCommaInScopeTuple` test (currently ends around line ~527):

#### Test 1: Expression list (set literal) with trailing comma
```csharp
[Fact]
public void ParseTrailingCommaInSetLiteral()
{
    PolicyAst policy = ParseSingle("permit (principal, action, resource) when {[1,2,].isEmpty() };");

    NodeIsEmpty node = Assert.IsType<NodeIsEmpty>(Assert.Single(policy.Conditions));
    NodeSet set = Assert.IsType<NodeSet>(node.Operand);
    Assert.Equal(2, set.Elements.Length);
}
```

#### Test 2: Record literal with trailing comma
```csharp
[Fact]
public void ParseTrailingCommaInRecordLiteral()
{
    PolicyAst policy = ParseSingle("""permit (principal, action, resource) when {{"key":1,} has key };""");

    NodeHas node = Assert.IsType<NodeHas>(Assert.Single(policy.Conditions));
    NodeRecord record = Assert.IsType<NodeRecord>(node.Operand);
    Assert.Single(record.Elements);
}
```

#### Test 3: Entity list in `in [...]` expression with trailing comma
```csharp
[Fact]
public void ParseTrailingCommaInEntityListExpression()
{
    PolicyAst policy = ParseSingle("""permit (principal, action, resource) when {User::"alice" in [User::"bob",] };""");

    NodeIn node = Assert.IsType<NodeIn>(Assert.Single(policy.Conditions));
    NodeSet set = Assert.IsType<NodeSet>(node.Entity);
    Assert.Single(set.Elements);
}
```

## Acceptance Criteria

1. All three new `[Fact]` tests compile without warnings (TreatWarningsAsErrors).
2. All three tests pass: `dotnet test cedar-dotnet.sln --filter "ParseTrailingComma"`.
3. No existing tests regress: `dotnet test cedar-dotnet.sln`.
4. Ledger updated: `python3 semport/ledger.py update a12ba1d implemented && python3 semport/ledger.py sort`.
5. Committed: `git add -A && git commit -m "semport: implement a12ba1d - trailing comma test coverage"`.

## C# Type Reference (for implementer)

To write the tests, the implementer needs to know these AST node types (all in `src/Cedar.Ast/` or linked from `src/Cedar.Core/Internal/`):

| Type | Used for |
|---|---|
| `NodeIsEmpty` | `.isEmpty()` call node — check property `Operand` |
| `NodeSet` | Set literal `[...]` — check property `Elements` (ImmutableArray) |
| `NodeHas` | `has` operator — check property `Operand` |
| `NodeRecord` | Record literal `{...}` — check property `Elements` (ImmutableArray) |
| `NodeIn` | `in` operator — check property `Entity` |
| `PolicyAst` | Top-level policy — check `Conditions` (ImmutableArray) |

Locate node type names: `grep -rn "NodeIsEmpty\|NodeSet\|NodeHas\|NodeRecord\|NodeIn\b" src/Cedar.Core/Internal/ --include="*.cs" -l`

## Implementation Order

1. Run `grep -n "ParseTrailingCommaInScopeTuple" test/Cedar.Tests/Parser/ParserTests.cs` to find exact line.
2. Confirm node type names exist: `grep -rn "class NodeIsEmpty\|record NodeIsEmpty" src/`.
3. Add three `[Fact]` tests after `ParseTrailingCommaInScopeTuple`.
4. Run `dotnet test cedar-dotnet.sln --filter "ParseTrailingComma"` — all 4 should pass.
5. Run `dotnet test cedar-dotnet.sln` — no regressions.
6. Update ledger and commit.

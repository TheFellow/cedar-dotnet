# Finalized Port Plan — `47584d0`
## Fix Cedar marshal operator parenthesization for associativity

---

## Target File

**`src/Cedar.Core/Internal/Parser/CedarWriter.cs`**

Precedence constants (lines 13–21):
- `PrecOr = 2` (line 14)
- `PrecAnd = 3` (line 15)
- `PrecAdd = 5` (line 17)
- `PrecMult = 6` (line 18)

---

## Changes Required (3 bug fixes + 1 simplification)

### Fix 1 — `NodeOr` right child precedence (lines 155–159)

**Current (buggy):**
```csharp
case NodeOr or:
    WriteNode(builder, or.Left, PrecOr);
    builder.Append(" || ");
    WriteNode(builder, or.Right, PrecOr);
    break;
```

**Fixed:**
```csharp
case NodeOr or:
    WriteNode(builder, or.Left, PrecOr);
    builder.Append(" || ");
    WriteNode(builder, or.Right, PrecOr + 1);
    break;
```

**Why:** `||` is left-associative. The right child at the same precedence means `a || (b || c)` would serialize as `a || b || c`, losing the explicit right-grouping. Bumping to `+1` forces parens on any same-or-lower-precedence right child.

---

### Fix 2 — `NodeAnd` right child precedence (lines 160–164)

**Current (buggy):**
```csharp
case NodeAnd and:
    WriteNode(builder, and.Left, PrecAnd);
    builder.Append(" && ");
    WriteNode(builder, and.Right, PrecAnd);
    break;
```

**Fixed:**
```csharp
case NodeAnd and:
    WriteNode(builder, and.Left, PrecAnd);
    builder.Append(" && ");
    WriteNode(builder, and.Right, PrecAnd + 1);
    break;
```

**Why:** Same left-associativity rule as `||`.

---

### Fix 3 — `NodeMult` right child precedence (lines 248–252)

**Current (buggy):**
```csharp
case NodeMult mult:
    WriteNode(builder, mult.Left, PrecMult);
    builder.Append(" * ");
    WriteNode(builder, mult.Right, PrecMult);
    break;
```

**Fixed:**
```csharp
case NodeMult mult:
    WriteNode(builder, mult.Left, PrecMult);
    builder.Append(" * ");
    WriteNode(builder, mult.Right, PrecMult + 1);
    break;
```

**Why:** `*` is left-associative. Same rule applies.

---

### Fix 4 — `NodeAdd` simplification (lines 230–242)

**Current (partially-correct with special case):**
```csharp
case NodeAdd add:
    WriteNode(builder, add.Left, PrecAdd);
    builder.Append(" + ");
    if (add.Right is NodeSub)
    {
        WriteNode(builder, add.Right, PrecAdd + 1);
    }
    else
    {
        WriteNode(builder, add.Right, PrecAdd);
    }

    break;
```

**Fixed (uniform left-associative rule):**
```csharp
case NodeAdd add:
    WriteNode(builder, add.Left, PrecAdd);
    builder.Append(" + ");
    WriteNode(builder, add.Right, PrecAdd + 1);
    break;
```

**Why:** The special `NodeSub` case was a partial workaround for the same bug — it only protected against one specific right-child type. The upstream fix eliminates the special case by uniformly applying `p+1` for all right children of left-associative ops. This is both correct and simpler.

---

## New Tests to Add

**File:** `test/Cedar.Tests/Parser/CedarWriterTests.cs`

**Insert before the closing `}` of the class (after line 137, before the `private static` helpers).**

The test file already has:
- `using Cedar.Ast;` (line 2)
- `using Cedar.Ast.Internal;` (line 3)  
- `using Cedar.Types;` (line 6)
- `BuildPolicy(INode expression)` helper (line 139)
- `NodeSub`, `NodeValue`, `CedarLong` already used (line 72)

Node constructors to use (all are `record` types in `Cedar.Ast.Internal`):
- `new NodeOr(INode left, INode right)`
- `new NodeAnd(INode left, INode right)`
- `new NodeMult(INode left, INode right)`
- `new NodeAdd(INode left, INode right)`
- `new NodeSub(INode left, INode right)`
- `new NodeEquals(INode left, INode right)`
- `new NodeValue(CedarValue value)` — use `new CedarLong(1)` etc.
- `new NodeVariable(CedarString name)` — use `new CedarString("principal")` etc.

**Tests to add:**

```csharp
[Fact]
public void WriteParenthesizesRightOperandOfOrForAssociativity()
{
    // a || (b || c) must round-trip with parens on right group
    INode expression = new NodeOr(
        new NodeVariable(new CedarString("principal")),
        new NodeOr(
            new NodeVariable(new CedarString("action")),
            new NodeVariable(new CedarString("resource"))));
    PolicyAst policy = BuildPolicy(expression);

    Assert.Equal(
        "permit(principal, action, resource)\n  when { principal || (action || resource) };",
        CedarWriter.Write(policy));
}

[Fact]
public void WriteParenthesizesRightOperandOfAndForAssociativity()
{
    // a && (b && c) must round-trip with parens on right group
    INode expression = new NodeAnd(
        new NodeVariable(new CedarString("principal")),
        new NodeAnd(
            new NodeVariable(new CedarString("action")),
            new NodeVariable(new CedarString("resource"))));
    PolicyAst policy = BuildPolicy(expression);

    Assert.Equal(
        "permit(principal, action, resource)\n  when { principal && (action && resource) };",
        CedarWriter.Write(policy));
}

[Fact]
public void WriteParenthesizesRightOperandOfMultForAssociativity()
{
    // 1 * (2 * 3) must round-trip with parens on right group
    INode expression = new NodeMult(
        new NodeValue(new CedarLong(1)),
        new NodeMult(
            new NodeValue(new CedarLong(2)),
            new NodeValue(new CedarLong(3))));
    PolicyAst policy = BuildPolicy(expression);

    Assert.Equal(
        "permit(principal, action, resource)\n  when { 1 * (2 * 3) };",
        CedarWriter.Write(policy));
}

[Fact]
public void WriteParenthesizesRightOperandOfAddForAssociativity()
{
    // 1 + (2 + 3) must round-trip with parens on right group
    INode expression = new NodeAdd(
        new NodeValue(new CedarLong(1)),
        new NodeAdd(
            new NodeValue(new CedarLong(2)),
            new NodeValue(new CedarLong(3))));
    PolicyAst policy = BuildPolicy(expression);

    Assert.Equal(
        "permit(principal, action, resource)\n  when { 1 + (2 + 3) };",
        CedarWriter.Write(policy));
}

[Fact]
public void WriteAddWithSubOnRightParenthesizes()
{
    // 1 + (2 - 3) — the NodeSub special case no longer needs special handling
    INode expression = new NodeAdd(
        new NodeValue(new CedarLong(1)),
        new NodeSub(
            new NodeValue(new CedarLong(2)),
            new NodeValue(new CedarLong(3))));
    PolicyAst policy = BuildPolicy(expression);

    Assert.Equal(
        "permit(principal, action, resource)\n  when { 1 + (2 - 3) };",
        CedarWriter.Write(policy));
}
```

---

## Acceptance Criteria

1. All 5 new tests pass.
2. All existing tests in `test/Cedar.Tests/Parser/CedarWriterTests.cs` continue to pass (especially `WriteParenthesizesWhenNeededForSubtraction` at line 70).
3. All existing tests in `test/Cedar.Tests/Parser/RoundTripTests.cs` continue to pass.
4. `dotnet test cedar-dotnet.sln` exits 0 with no new failures.

---

## Go → C# Pattern Mapping

| Go pattern | C# equivalent |
|---|---|
| `marshalInfixBinaryOp(n.BinaryNode, leftPrec, rightPrec, op, buf)` | Inline `WriteNode(builder, node.Left, leftPrec)` / `WriteNode(builder, node.Right, rightPrec)` in each `case` branch |
| `nodePrecedenceLevel` int type | `int` constants `PrecOr`, `PrecAnd`, etc. |
| `p+1` precedence bump | `PrecXxx + 1` literal arithmetic — no named constant needed |
| Go `switch` on node type | C# `switch` with pattern matching `case NodeOr or:` etc. |
| Go `buf.WriteString(...)` | `builder.Append(...)` |

---

## No Other Files Affected

- `GetPrecedence()` (lines 380–394) returns the node's *own* precedence and is used only to decide whether to wrap the current node in parens when *entering* `WriteNode`. It does not need to change — the fix is only about what precedence is *passed as `parentPrecedence`* to child `WriteNode` calls.
- No schema, evaluation, or JSON files are affected.
- No new source files need to be created.

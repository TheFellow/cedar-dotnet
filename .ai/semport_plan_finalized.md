# Finalized Port Plan: b4c937e
## ast: Add AST builder methods for datetime-related types and operators

---

## Current State (as discovered by grep)

### ✅ Already implemented — NO changes needed:
- **Value factories** `Datetime(string)`, `Datetime(DateTimeOffset)`, `Duration(string)`, `Duration(TimeSpan)`  
  → `src/Cedar.Ast/Values.cs:141-158`
- **Extension registry** — `offset` (arity=2,isMethod=true) and `durationSince` (arity=2,isMethod=true) are both present  
  → `src/Cedar.Core/Internal/Extensions/ExtensionRegistry.cs:25-43`
- **All nine operator methods** except `DurationSince` — `Offset`, `ToDate`, `ToTime`, `ToDays`, `ToHours`, `ToMinutes`, `ToSeconds`, `ToMilliseconds` are all present  
  → `src/Cedar.Ast/ExtensionOperators.cs:52-139`

### ❌ Missing — must be added:
- **`DurationSince(Node rhs)`** extension method on `Node`  
  → `src/Cedar.Ast/ExtensionOperators.cs` (between `Offset` at line ~52 and `DaysInMonth` at line ~57)

### ⚠️ Missing test coverage:
- No tests in `test/Cedar.Tests/Ast/PolicyBuilderTests.cs` for any datetime/duration builder methods.
  Tests exist only for IP, decimal, and general builder structure.

---

## Task 1 — Add `DurationSince` to ExtensionOperators.cs

**File:** `src/Cedar.Ast/ExtensionOperators.cs`  
**Insert after line ~54** (after the `Offset` method body, before `DaysInMonth`):

```csharp
public static Node DurationSince(this Node lhs, Node rhs)
{
    return Operators.ExtensionCall("durationSince", lhs, rhs);
}
```

**Pattern:** identical to `Offset` at line 52-54, just different method name and Cedar function name.  
**Go source:** `ast/operator.go:171` → `func (lhs Node) DurationSince(rhs Node) Node { return wrapNode(lhs.Node.DurationSince(rhs.Node)) }`  
**C# idiom:** `static` extension method on `Node`, delegates to `Operators.ExtensionCall(string, params Node[])`.

---

## Task 2 — Add xUnit tests for datetime/duration builder methods

**File:** `test/Cedar.Tests/Ast/PolicyBuilderTests.cs`  
**Insert:** a new test class or a `[Theory]` block at the end of the file (before the final `}`).

**Required usings** (check existing usings at top of file — add any missing):
- `Cedar.Ast` (likely present)
- `Cedar.Ast.Internal` (for `NodeExtensionCall`)
- `Cedar.Types` (for `CedarDatetime`, `CedarDuration`)

**Test pattern to follow:** `test/Cedar.Tests/Json/PolicyJsonTests.cs:194-250` — asserts `NodeExtensionCall` with correct `Name` and `Args`.

**Tests to add** (one `[Fact]` each):

```csharp
// Go: ast.Permit().When(ast.Datetime(time.Time{}).Offset(ast.Duration(time.Duration(100))))
[Fact]
public void Operator_Offset_ProducesExtensionCall()
{
    var policy = CedarAst.Permit()
        .When(Values.Datetime(DateTimeOffset.MinValue).Offset(Values.Duration(TimeSpan.Zero)));
    NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
    Assert.Equal("offset", call.Name.Value);
    Assert.Equal(2, call.Args.Length);
}

// Go: ast.Permit().When(ast.Datetime(time.Time{}).DurationSince(ast.Datetime(time.Time{})))
[Fact]
public void Operator_DurationSince_ProducesExtensionCall()
{
    var policy = CedarAst.Permit()
        .When(Values.Datetime(DateTimeOffset.MinValue).DurationSince(Values.Datetime(DateTimeOffset.MinValue)));
    NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
    Assert.Equal("durationSince", call.Name.Value);
    Assert.Equal(2, call.Args.Length);
}

// Go: ast.Permit().When(ast.Datetime(time.Time{}).ToDate())
[Fact]
public void Operator_ToDate_ProducesExtensionCall()
{
    var policy = CedarAst.Permit()
        .When(Values.Datetime(DateTimeOffset.MinValue).ToDate());
    NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
    Assert.Equal("toDate", call.Name.Value);
    Assert.Single(call.Args);
}

// Go: ast.Permit().When(ast.Datetime(time.Time{}).ToTime())
[Fact]
public void Operator_ToTime_ProducesExtensionCall()
{
    var policy = CedarAst.Permit()
        .When(Values.Datetime(DateTimeOffset.MinValue).ToTime());
    NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
    Assert.Equal("toTime", call.Name.Value);
    Assert.Single(call.Args);
}

// Go: ast.Permit().When(ast.Duration(time.Duration(100)).ToDays())
[Fact]
public void Operator_ToDays_ProducesExtensionCall()
{
    var policy = CedarAst.Permit()
        .When(Values.Duration(TimeSpan.Zero).ToDays());
    NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
    Assert.Equal("toDays", call.Name.Value);
    Assert.Single(call.Args);
}

[Fact]
public void Operator_ToHours_ProducesExtensionCall()
{
    var policy = CedarAst.Permit()
        .When(Values.Duration(TimeSpan.Zero).ToHours());
    NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
    Assert.Equal("toHours", call.Name.Value);
    Assert.Single(call.Args);
}

[Fact]
public void Operator_ToMinutes_ProducesExtensionCall()
{
    var policy = CedarAst.Permit()
        .When(Values.Duration(TimeSpan.Zero).ToMinutes());
    NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
    Assert.Equal("toMinutes", call.Name.Value);
    Assert.Single(call.Args);
}

[Fact]
public void Operator_ToSeconds_ProducesExtensionCall()
{
    var policy = CedarAst.Permit()
        .When(Values.Duration(TimeSpan.Zero).ToSeconds());
    NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
    Assert.Equal("toSeconds", call.Name.Value);
    Assert.Single(call.Args);
}

[Fact]
public void Operator_ToMilliseconds_ProducesExtensionCall()
{
    var policy = CedarAst.Permit()
        .When(Values.Duration(TimeSpan.Zero).ToMilliseconds());
    NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
    Assert.Equal("toMilliseconds", call.Name.Value);
    Assert.Single(call.Args);
}
```

**Note:** Verify the C# class/static accessor for `Values.Datetime(...)` — in Go it's `ast.Datetime(...)`. In C# it's `Values.Datetime(...)` (static class `Values` in `Cedar.Ast`). Check existing test imports to confirm.

---

## Acceptance Criteria

1. `dotnet build cedar-dotnet.sln` — zero errors, zero warnings.
2. `dotnet test cedar-dotnet.sln` — all tests pass including the 9 new `[Fact]` tests.
3. `src/Cedar.Ast/ExtensionOperators.cs` contains a public `DurationSince(this Node lhs, Node rhs)` method that calls `Operators.ExtensionCall("durationSince", lhs, rhs)`.
4. `test/Cedar.Tests/Ast/PolicyBuilderTests.cs` contains 9 new `[Fact]` tests for the datetime/duration operator methods.

---

## Go → C# Pattern Map

| Go | C# |
|----|----|
| `func (lhs Node) Foo(rhs Node) Node` | `public static Node Foo(this Node lhs, Node rhs)` |
| `wrapNode(lhs.Node.DurationSince(rhs.Node))` | `Operators.ExtensionCall("durationSince", lhs, rhs)` |
| `NewMethodCall(lhs, "durationSince", rhs)` | `Operators.ExtensionCall("durationSince", lhs, rhs)` |
| `types.FromStdTime(time.Time{})` | `CedarDatetime.FromDateTimeOffset(DateTimeOffset.MinValue)` |
| `types.FromStdDuration(time.Duration(100))` | `new CedarDuration(...)` or `Values.Duration(TimeSpan.Zero)` |
| `ast.Datetime(t)` in test | `Values.Datetime(DateTimeOffset.MinValue)` |
| `ast.Duration(d)` in test | `Values.Duration(TimeSpan.Zero)` |
| Go table-driven test `{ "opFoo", ast.Permit().When(...), internalast.Permit().When(...) }` | xUnit `[Fact]` asserting `NodeExtensionCall.Name.Value` and `Args.Length` |

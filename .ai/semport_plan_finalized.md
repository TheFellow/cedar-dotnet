# Finalized Port Plan: d267346
**Fix canMarshalAsIdent for empty strings and reserved keywords**

## Status: PARTIALLY ALREADY IMPLEMENTED

The core fix is **already present** in the C# codebase. The `CanWriteIdentifier` method in `CedarWriter.cs` already guards against both empty strings and reserved keywords. Two of the three test cases from the Go commit are also already present. Only one test case is missing.

---

## What's Already Done ✅

### Fix (already implemented)
**File**: `src/Cedar.Core/Internal/Parser/CedarWriter.cs`, lines 388–409
```csharp
private static bool CanWriteIdentifier(string value)
{
    if (string.IsNullOrEmpty(value) || IsReservedKeyword(value))  // ← ALREADY GUARDS BOTH
    {
        return false;
    }
    // ...
}
```

### Reserved keyword list (already implemented)
**File**: `src/Cedar.Core/Internal/Parser/CedarWriter.cs`, lines 411–426
```csharp
private static bool IsReservedKeyword(string value)
{
    return value is "permit" or "forbid" or "when" or "unless"
        or "true" or "false" or "if" or "then" or "else"
        or "in" or "like" or "has" or "is";
}
```
Note: C# list is a superset of Go's (`true`, `false`, `if`, `then`, `else`, `in`, `like`, `has`, `is`, `__cedar`).
**Gap**: `__cedar` is in Go's list but missing from C#'s `IsReservedKeyword`.

### Existing tests (already present)
**File**: `test/Cedar.Tests/Parser/CedarWriterTests.cs`
- `WriteAccessWithQuotedAttribute` — tests `context["not valid"]` (spaces → bracket-notation) ✅
- `WriteHasWithQuotedAttribute` — tests `context has "if"` (reserved keyword in `has`) ✅

---

## What Needs to Be Done

### Task 1: Add `__cedar` to `IsReservedKeyword` (minor gap)
**File**: `src/Cedar.Core/Internal/Parser/CedarWriter.cs`, line ~425
**Change**: Add `or "__cedar"` to the `IsReservedKeyword` method.
```csharp
// BEFORE:
return value is "permit" or "forbid" or "when" or "unless"
    or "true" or "false" or "if" or "then" or "else"
    or "in" or "like" or "has" or "is";

// AFTER:
return value is "permit" or "forbid" or "when" or "unless"
    or "true" or "false" or "if" or "then" or "else"
    or "in" or "like" or "has" or "is"
    or "__cedar";
```

### Task 2: Add two missing test cases
**File**: `test/Cedar.Tests/Parser/CedarWriterTests.cs`

Add after `WriteHasWithQuotedAttribute` (around line 115):

```csharp
[Fact]
public void WriteAccessWithReservedKeyword()
{
    INode expression = new NodeAccess(new NodeVariable(new CedarString("context")), new CedarString("true"));
    PolicyAst policy = BuildPolicy(expression);

    Assert.Equal(
        "permit(principal, action, resource)\n  when { context[\"true\"] };",
        CedarWriter.Write(policy));
}

[Fact]
public void WriteAccessWithEmptyString()
{
    INode expression = new NodeAccess(new NodeVariable(new CedarString("context")), new CedarString(""));
    PolicyAst policy = BuildPolicy(expression);

    Assert.Equal(
        "permit(principal, action, resource)\n  when { context[\"\"] };",
        CedarWriter.Write(policy));
}
```

---

## Acceptance Criteria
1. `dotnet test cedar-dotnet.sln` passes with zero failures.
2. `WriteAccessWithReservedKeyword` test passes: `context["true"]` not `context.true`.
3. `WriteAccessWithEmptyString` test passes: `context[""]` not `context.`.
4. `IsReservedKeyword("__cedar")` returns `true`.
5. No new compiler warnings introduced.

---

## Relevant File Map

| File | Purpose | Key Lines |
|------|---------|-----------|
| `src/Cedar.Core/Internal/Parser/CedarWriter.cs` | Cedar policy serializer | L215, L255, L333 — `CanWriteIdentifier` call sites; L388 — method def; L411 — `IsReservedKeyword` |
| `test/Cedar.Tests/Parser/CedarWriterTests.cs` | Serializer tests | ~L91 `WriteAccessWithQuotedAttribute`, ~L102 `WriteHasWithQuotedAttribute` |

## Go → C# Pattern Map
| Go | C# |
|----|-----|
| `canMarshalAsIdent(s string) bool` | `CanWriteIdentifier(string value)` in `CedarWriter` |
| `IsReservedKeyword(s)` | `IsReservedKeyword(string value)` in `CedarWriter` |
| `len(s) == 0` | `string.IsNullOrEmpty(value)` |
| `context.foo` dot-notation | `context.foo` |
| `context["true"]` bracket-notation | `context["true"]` |

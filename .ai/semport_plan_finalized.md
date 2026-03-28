# Finalized Port Plan: c1a7b0a

## Summary
Add validation in the C# Cedar JSON policy parser to reject unknown extension function names with a descriptive `JsonException`, mirroring the Go change to `extensionJSON.ToNode()`.

---

## Files to Change

### 1. `src/Cedar.Core/Internal/Json/NodeJsonModel.cs` — add guard in `ReadExtensionCall`

**Target method:** `ReadExtensionCall(string name, JsonNode node)` at **line 360**

**Current body (lines 360–370):**
```csharp
private static INode ReadExtensionCall(string name, JsonNode node)
{
    JsonArray argsNode = AsArray(node, $"extension call '{name}'");
    ImmutableArray<INode>.Builder args = ImmutableArray.CreateBuilder<INode>(argsNode.Count);

    foreach (JsonNode? arg in argsNode)
    {
        args.Add(ToAst(arg ?? throw new JsonException("Extension call arguments cannot be null.")));
    }

    return new NodeExtensionCall(name, args.ToImmutable());
}
```

**Required change:** Before the `AsArray` call, add:
```csharp
if (!ExtensionRegistry.TryGet(name, out _))
    throw new JsonException($"`{name}` is not a known extension function or method");
```

**Go equivalent:** `extensions.ExtMap[types.String(k)]` lookup in `json_unmarshal.go` line ~140.

**Notes:**
- `ExtensionRegistry` is in `Cedar.Core.Internal.Extensions` — no new `using` needed (same assembly, same internal access).
- `ExtensionRegistry.TryGet(string, out ExtensionDefinition)` already exists at line 46 of `src/Cedar.Core/Internal/Extensions/ExtensionRegistry.cs`.
- The `_` discard on the `out` parameter is correct; we only care about existence.
- Use `JsonException` (already the error type throughout this file, imported via `System.Text.Json`).

### 2. `test/Cedar.Tests/Json/PolicyJsonTests.cs` — add error test case

**Insert after:** line 58 (end of `UnmarshalJson_ScopeEqWithoutEntityThrowsJsonException` test), before line 61 `[Fact]`.

**New test to add:**
```csharp
[Fact]
public void UnmarshalJson_UnknownExtensionFunction_ThrowsJsonException()
{
    JsonException exception = Assert.Throws<JsonException>(
        () => UnmarshalPolicyWithConditionBody("""{"not_an_extension_function": []}"""));

    Assert.Contains("not_an_extension_function", exception.Message, StringComparison.Ordinal);
    Assert.Contains("is not a known extension function or method", exception.Message, StringComparison.Ordinal);
}
```

**Pattern match:** Mirrors `UnmarshalJson_ScopeEqWithoutEntityThrowsJsonException` (lines 44–58): uses `Assert.Throws<JsonException>`, calls `Policy.UnmarshalJson` indirectly via `UnmarshalPolicyWithConditionBody` helper (line 444), asserts `exception.Message` content.

---

## Acceptance Criteria

1. `dotnet build cedar-dotnet.sln` produces zero errors and zero warnings.
2. `dotnet test cedar-dotnet.sln` passes all existing tests.
3. The new test `UnmarshalJson_UnknownExtensionFunction_ThrowsJsonException` passes.
4. Parsing `{"not_an_extension_function": []}` as a policy body condition throws `JsonException` with message containing `` `not_an_extension_function` is not a known extension function or method ``.
5. Parsing a valid extension call e.g. `{"ip": [{"Value": "10.0.0.1"}]}` continues to work (regression guard).

---

## Go → C# Pattern Map

| Go | C# |
|---|---|
| `extensions.ExtMap[types.String(k)]` | `ExtensionRegistry.TryGet(name, out _)` |
| `fmt.Errorf("...")` returned as `error` | `throw new JsonException("...")` |
| `extensionJSON.ToNode()` | `ReadExtensionCall(string name, JsonNode node)` |
| `internal/extensions/extensions.go` `ExtMap` | `src/Cedar.Core/Internal/Extensions/ExtensionRegistry.cs` `Definitions` dictionary |

---

## No New Files Required
Both changes are edits to existing files. `ExtensionRegistry.TryGet` is already the right API — no new constants, sets, or helpers needed.

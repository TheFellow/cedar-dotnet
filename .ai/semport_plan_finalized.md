# Finalized Port Plan: 60b4b94 — types: Address PR feedback

## Summary
Fix a Cedar-text parsing bug in `EntityUid.TryParseCedar`: switch from `LastIndexOf` to `IndexOf` when finding the `::\"` separator between entity type and ID, and apply proper Rust-style unquoting to the ID.

---

## Bug: `LastIndexOf` → `IndexOf` + proper unquoting

### Go fix (reference)
`inspiration/cedar-go/types/entity_uid.go`, `UnmarshalCedar`:
- Line ~59: `strings.LastIndex(s, "::\"")`  → `strings.Index(s, "::\"")`
- Lines ~65-72: naive raw slice replaced by `rust.Unquote(quoted[1:len-1])`

### C# target file
**`src/Cedar.Types/EntityUid.cs`**, `TryParseCedar` method, **lines 19–34**

Current buggy code (lines 23–32):
```csharp
int index = input.LastIndexOf("::\"", StringComparison.Ordinal);
if (index <= 0 || !input.EndsWith('"'))
{
    result = null;
    return false;
}

string type = input[..index];
string id = input[(index + 3)..^1];
result = new EntityUid(new EntityType(type), new CedarString(id));
return true;
```

### Required fix
1. Change `LastIndexOf` → `IndexOf` (line 23) — **exact 1-line change**
2. After finding the split index, extract the quoted substring `input[(index+2)..]` (includes the leading `"`), validate it starts with `"` and ends with `"`, then call `RustStringHelper.Unquote(quoted)` to decode escape sequences.

**`RustStringHelper.Unquote` is already available** in `src/Cedar.Core/Internal/Rust/RustStringHelper.cs` (line 8). It accepts a `string`, validates surrounding quotes internally (lines 12-17), and returns the unescaped inner value.

**Note:** `Cedar.Types` does NOT currently reference `Cedar.Core`. Check if `Cedar.Types` already has a project reference to `Cedar.Core`, or if `RustStringHelper` needs to be made accessible another way (e.g. move, copy, or expose via a shared internal utility). The `Cedar.Types` → `Cedar.Core` dependency direction must be verified — if it's inverted, use `CedarString.EscapeCharAll` as the reverse path and locate another approach. The `SchemaStringHelper.Unquote` in `src/Cedar.Schema/Internal/SchemaStringHelper.cs` is an alternative but is schema-scoped.

---

## Changes Required

### Change 1 — `src/Cedar.Types/EntityUid.cs`

Replace `TryParseCedar` body (lines 19–34):

**Old:**
```csharp
public static bool TryParseCedar(string input, [NotNullWhen(true)] out EntityUid? result)
{
    ArgumentNullException.ThrowIfNull(input);

    int index = input.LastIndexOf("::\"", StringComparison.Ordinal);
    if (index <= 0 || !input.EndsWith('"'))
    {
        result = null;
        return false;
    }

    string type = input[..index];
    string id = input[(index + 3)..^1];
    result = new EntityUid(new EntityType(type), new CedarString(id));
    return true;
}
```

**New:**
```csharp
public static bool TryParseCedar(string input, [NotNullWhen(true)] out EntityUid? result)
{
    ArgumentNullException.ThrowIfNull(input);

    // Use IndexOf (first occurrence) so that IDs containing "::" are handled correctly.
    int index = input.IndexOf("::\"", StringComparison.Ordinal);
    if (index <= 0)
    {
        result = null;
        return false;
    }

    string type = input[..index];
    string quoted = input[(index + 2)..]; // includes the leading '"'

    if (quoted.Length < 2 || quoted[^1] != '"')
    {
        result = null;
        return false;
    }

    try
    {
        string id = RustStringHelper.Unquote(quoted);
        result = new EntityUid(new EntityType(type), new CedarString(id));
        return true;
    }
    catch (FormatException)
    {
        result = null;
        return false;
    }
}
```

**Required using / dependency:**  
Add `using Cedar.Core.Internal.Rust;` to `EntityUid.cs` IF `Cedar.Types` already references `Cedar.Core`.  
Verify with: `grep -r "Cedar.Core" src/Cedar.Types/Cedar.Types.csproj`  
If the reference does not exist, check whether the dependency is valid or use an inline minimal unquote.

---

### Change 2 — `test/Cedar.Tests/Types/EntityUidTests.cs`

Add new test cases to the existing `TryParseCedar_RoundTrip` theory (currently lines 144–152) and a new test for IDs containing `::`.

**Add to `[InlineData]` in `TryParseCedar_RoundTrip` (after line 146):**
```csharp
[InlineData("X::Y::\"asdf::\"")]
[InlineData("Search::Algorithm::\"A*\"")]
[InlineData("Super::\"*\"")]
[InlineData("namespace::type::\"\"")]
```

**Add new test fact** (after `TryParseCedar_NamespacedType`):
```csharp
[Fact]
public void TryParseCedar_IdContainingDoubleColon()
{
    Assert.True(EntityUid.TryParseCedar("X::Y::\"asdf::\"", out EntityUid? result));
    Assert.Equal("X::Y", result!.Type.Value);
    Assert.Equal("asdf::", result.Id.Value);
}
```

---

## Acceptance Criteria

1. `EntityUid.TryParseCedar("X::Y::\"asdf::\"", out _)` returns `true` with `Type="X::Y"`, `Id="asdf::"`.
2. `EntityUid.TryParseCedar("Namespace::Type::\"id\"", out _)` still works correctly (regression).
3. `EntityUid.TryParseCedar("::\"id\"", out _)` still returns `false` (no type).
4. `EntityUid.TryParseCedar("Type::\"id", out _)` still returns `false` (unclosed quote).
5. `MarshalCedar()` + `TryParseCedar()` round-trip holds for IDs containing `::`, `*`, and empty string.
6. All existing `EntityUidTests` pass without modification.
7. `dotnet test cedar-dotnet.sln` exits 0.

---

## Go → C# Pattern Mapping

| Go | C# |
|---|---|
| `strings.Index(s, "::\"")` | `input.IndexOf("::\"", StringComparison.Ordinal)` |
| `rust.Unquote([]byte(quoted))` returns `(string, int, error)` | `RustStringHelper.Unquote(string quoted)` returns `string`, throws `FormatException` |
| `return errInvalidUID` | `result = null; return false;` |
| `*e = NewEntityUID(typ, String(id))` | `result = new EntityUid(new EntityType(type), new CedarString(id)); return true;` |

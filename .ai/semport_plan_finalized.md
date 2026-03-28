# Finalized Port Plan — a94e3e2

## Summary
Upstream Go commit adds explicit lowercase JSON struct tags to `Position`
(`filename`, `offset`, `line`, `column`). C# `System.Text.Json` defaults to
the property name as written (PascalCase), so the same attributes must be
added explicitly via `[JsonPropertyName]`.

---

## Target Files

| Role | File | Key line(s) |
|------|------|-------------|
| **C# type to change** | `src/Cedar.Core/Position.cs` | line 3 — the entire `record struct` declaration |
| **Existing test file** | `test/Cedar.Tests/Policy/PolicyTests.cs` | lines 1–5 (usings), line 106–110 (closest existing Position test) |

---

## Change 1 — `src/Cedar.Core/Position.cs`

**Current (line 3):**
```csharp
public readonly record struct Position(string Filename, int Offset, int Line, int Column);
```

**Required — add `[JsonPropertyName]` to each positional parameter and a
`using` directive:**
```csharp
using System.Text.Json.Serialization;

namespace Cedar.Core;

public readonly record struct Position(
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("offset")]   int Offset,
    [property: JsonPropertyName("line")]     int Line,
    [property: JsonPropertyName("column")]   int Column);
```

> **Note:** The file has no usings today (just `namespace Cedar.Core;` and the
> one-liner record on line 3). Add the `using` above the namespace line, then
> expand the record to multi-line form with the attributes.

---

## Change 2 — `test/Cedar.Tests/Policy/PolicyTests.cs` (new test method)

Add after the existing `UnmarshalJson_AssignsDefaultPosition` test (≈ line 110).
The file already imports `System.Text.Json` and `Cedar.Core`.

```csharp
[Fact]
public void Position_JsonRoundTrip_UsesLowercaseKeys()
{
    var pos = new Position("foo.cedar", Offset: 1, Line: 2, Column: 3);
    string json = JsonSerializer.Serialize(pos);

    Assert.Contains("\"filename\"", json);
    Assert.Contains("\"offset\"",   json);
    Assert.Contains("\"line\"",     json);
    Assert.Contains("\"column\"",   json);

    var deserialized = JsonSerializer.Deserialize<Position>(json);
    Assert.Equal(pos, deserialized);
}
```

No additional `using` directives required — `System.Text.Json` and
`Cedar.Core` are already present at lines 2–3.

---

## Acceptance Criteria
1. `dotnet build cedar-dotnet.sln` produces zero warnings/errors.
2. `dotnet test cedar-dotnet.sln` passes all tests including the new
   `Position_JsonRoundTrip_UsesLowercaseKeys` fact.
3. `JsonSerializer.Serialize(new Position("f", 0, 1, 1))` produces JSON with
   keys `filename`, `offset`, `line`, `column` (all lowercase).
4. Deserialization of `{"filename":"f","offset":0,"line":1,"column":1}`
   reconstructs the struct correctly.

---

## Go → C# Pattern Map

| Go pattern | C# equivalent used here |
|------------|------------------------|
| `json:"filename"` struct tag | `[property: JsonPropertyName("filename")]` on positional record parameter |
| Go `json.MarshalIndent` + round-trip test | `JsonSerializer.Serialize` + `JsonSerializer.Deserialize<T>` xUnit `[Fact]` |
| Go `t.Parallel()` | xUnit runs tests in parallel by default — no action needed |

---

## No Other JSON Paths to Update
`grep` for `JsonNamingPolicy` and `JsonConverter.*Position` across `src/`
returned no results — there are no custom converters or naming policies that
could interfere with the new attributes.

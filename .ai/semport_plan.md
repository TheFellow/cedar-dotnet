PORT

## Commit
a94e3e2 — 2024-09-05T10:06:03-07:00
"cedar: change JSON marshaling of the Position struct to use conventional lower case keys"

## Semantic Analysis
The Go `Position` struct gained explicit JSON struct tags with **lowercase** keys:
- `Filename` → `"filename"`
- `Offset`   → `"offset"`
- `Line`     → `"line"`
- `Column`   → `"column"`

Before this commit Go's default marshaling would have produced `"Filename"`, `"Offset"`, etc. (PascalCase). After this commit the canonical wire format is all-lowercase. This is a **public contract change**: any system that serializes/deserializes `Position` over JSON must use the lowercase key names.

In C#, `System.Text.Json` defaults to the property name as written. Our `Position` type (wherever it lives) must carry `[JsonPropertyName("...")]` attributes (or a matching naming policy) to guarantee the same lowercase wire format.

## Port Tasks

### 1. Locate the C# `Position` type
- Expected location: `src/Cedar.Core/` or `src/Cedar.Ast/` — search for `class Position` or `record Position`.
- Command to confirm: `grep -rn "Position" src/ --include="*.cs" -l`

### 2. Add `[JsonPropertyName]` attributes to each property
For whatever record/class holds `Filename`, `Offset`, `Line`, `Column`, add:

```csharp
using System.Text.Json.Serialization;

public sealed record Position(
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("offset")]   int Offset,
    [property: JsonPropertyName("line")]     int Line,
    [property: JsonPropertyName("column")]   int Column
);
```

If `Position` is not a positional record, add `[JsonPropertyName("...")]` above each auto-property instead.

### 3. Add a xUnit serialization round-trip test
Analogous to Go's `TestPositionJSON`. Add to the appropriate test project (likely `test/Cedar.Tests/`):

```csharp
[Fact]
public void Position_JsonRoundTrip_UsesLowercaseKeys()
{
    var pos = new Position("foo.cedar", Offset: 1, Line: 2, Column: 3);
    var json = JsonSerializer.Serialize(pos);

    // Assert lowercase keys present
    Assert.Contains("\"filename\"", json);
    Assert.Contains("\"offset\"", json);
    Assert.Contains("\"line\"", json);
    Assert.Contains("\"column\"", json);

    // Round-trip
    var deserialized = JsonSerializer.Deserialize<Position>(json);
    Assert.Equal(pos, deserialized);
}
```

### 4. Verify no other JSON serialization paths override these names
Search for `JsonNamingPolicy` or custom converters that might affect `Position`.

### File references
| Side | Location |
|------|----------|
| Go source (before) | `inspiration/cedar-go/policy.go` lines 100-106 (old struct fields) |
| Go source (after)  | `inspiration/cedar-go/policy.go` lines 100-116 (tagged fields) |
| Go test            | `inspiration/cedar-go/policy_test.go` lines 108-128 |
| C# target (type)   | `src/Cedar.Core/` or `src/Cedar.Ast/` — `Position` record/class |
| C# target (tests)  | `test/Cedar.Tests/` — new `PositionJsonTests.cs` or existing position test file |

# Finalized Port Plan: b7a52e1 — Deterministic Set JSON Serialization

## Summary
Upstream Go commit makes `MapSet[T].MarshalJSON()` sort elements by their lexicographic JSON byte representation before emitting a JSON array. The C# equivalent must do the same in `CedarValueJsonConverter.WriteValue`.

---

## Exact Change Location

### File to modify: `src/Cedar.Core/Internal/Json/CedarValueJsonConverter.cs` — lines 64–71

**Current code (lines 64–71):**
```csharp
case CedarSet set:
    writer.WriteStartArray();
    foreach (ICedarData item in set)
    {
        WriteValue(writer, item);
    }

    writer.WriteEndArray();
    break;
```

**Required change:**
Instead of writing each element directly to the writer in iteration order (non-deterministic, since `CedarSet` wraps an `ImmutableHashSet`), serialize each element to a `string` first, sort those strings with `StringComparer.Ordinal`, then write each pre-serialized JSON fragment to the array.

**Go → C# pattern mapping:**
| Go | C# |
|---|---|
| `json.Marshal(elem)` → `[]byte` | `JsonSerializer.Serialize<ICedarData>(item, options)` → `string` |
| `slices.SortFunc(elems, slices.Compare)` | `.OrderBy(s => s, StringComparer.Ordinal)` |
| `bytes.Join(...)` | Write each raw string via `writer.WriteRawValue(s)` |

**New code:**
```csharp
case CedarSet set:
    writer.WriteStartArray();
    // Serialize each element independently, sort lexicographically, then emit — matching
    // the deterministic ordering introduced in cedar-go b7a52e1.
    JsonSerializerOptions innerOptions = new(options);
    // Reuse the same converters already on the writer's options.
    List<string> serialized = new(set.Count);
    foreach (ICedarData item in set)
    {
        serialized.Add(JsonSerializer.Serialize<ICedarData>(item, options));
    }
    serialized.Sort(StringComparer.Ordinal);
    foreach (string s in serialized)
    {
        writer.WriteRawValue(s, skipInputValidation: true);
    }
    writer.WriteEndArray();
    break;
```

> **Note on `options`**: The `Write` method signature is `Write(Utf8JsonWriter writer, ICedarData value, JsonSerializerOptions options)`. The `options` parameter is already in scope and carries all registered converters, so passing it directly to `JsonSerializer.Serialize` will correctly recurse through nested sets/records.

---

## File to modify: `test/Cedar.Tests/Types/CedarSetTests.cs`

Add a new test class/region for JSON serialization ordering (after line 101, end of file).

**New tests to add:**
```csharp
[Fact]
public void JsonSerializesEmptySetAsEmptyArray()
{
    string json = CedarJson.SerializeData(new CedarSet());
    Assert.Equal("[]", json);
}

[Fact]
public void JsonSerializesIntegerSetInLexicographicOrder()
{
    // Input order is 3,2,1 — output must be sorted: 1,2,3
    CedarSet set = new(new CedarLong(3), new CedarLong(2), new CedarLong(1));
    string json = CedarJson.SerializeData(set);
    Assert.Equal("[1,2,3]", json);
}

[Fact]
public void JsonSerializesSingleElementSet()
{
    CedarSet set = new(new CedarLong(1));
    string json = CedarJson.SerializeData(set);
    Assert.Equal("[1]", json);
}

[Fact]
public void JsonSerializesStringSetInLexicographicOrder()
{
    CedarSet set = new(new CedarString("3"), new CedarString("1"), new CedarString("2"));
    string json = CedarJson.SerializeData(set);
    Assert.Equal("""["1","2","3"]""", json);
}
```

**Required `using` at top of `CedarSetTests.cs`** (check if already present):
- `using Cedar.Tests.TestSupport;`

---

## Acceptance Criteria

1. `dotnet build cedar-dotnet.sln` — no warnings or errors.
2. `dotnet test cedar-dotnet.sln` — all tests pass including the 4 new ones.
3. Serializing `new CedarSet(new CedarLong(3), new CedarLong(2), new CedarLong(1))` produces `"[1,2,3]"`.
4. Serializing `new CedarSet()` produces `"[]"`.
5. Existing round-trip tests (`JsonRoundTripSupportsPrimitiveMembers`, `JsonRoundTripSupportsEntityMembers`) still pass — they only check equality not order, so they are order-independent.

---

## Files to touch (exhaustive list)

| File | Change |
|---|---|
| `src/Cedar.Core/Internal/Json/CedarValueJsonConverter.cs` | Lines 64–71: replace `foreach` write loop with sort-then-write-raw pattern |
| `test/Cedar.Tests/Types/CedarSetTests.cs` | Append 4 new `[Fact]` tests for deterministic JSON output |

No other files require changes. No schema, conformance, or batch tests reference set serialization order.

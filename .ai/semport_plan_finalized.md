# Finalized Port Plan: e796ce2

## Status: ALREADY IMPLEMENTED (partially)

### Upstream Change
`inspiration/cedar-go/types/entity.go` — sort Entity parents by `(Type, ID)` before JSON marshaling.

### C# Implementation Status

**The sort is already present** in `src/Cedar.Core/Internal/Json/EntityJsonConverter.cs` at **line 48**:
```csharp
foreach (EntityUid parent in value.Parents
    .OrderBy(static item => item.Type.Value, StringComparer.Ordinal)
    .ThenBy(static item => item.Id.Value, StringComparer.Ordinal))
```
This directly mirrors the Go `slices.SortFunc` by `(Type, ID)`.

### Gap: Missing Deterministic-Ordering Test

The existing test at `test/Cedar.Tests/Types/EntityTests.cs:69` (`JsonSerializeUsesImplicitUidForms`) only checks two parents that happen to already be in sorted order. There is **no test that verifies sorted output when parents are added in non-alphabetical order**.

---

## Single Required Task

### Add test to `test/Cedar.Tests/Types/EntityTests.cs`

**File:** `test/Cedar.Tests/Types/EntityTests.cs`
**Insert after:** the last `[Fact]` method in the class (currently ends around line 145)

**Test to add** (mirrors upstream `TestEntityMarshalJSON` exactly):
```csharp
[Fact]
public void JsonSerializeParentsInConsistentOrder()
{
    // Parents added in non-alphabetical order — serialized output must be sorted (Type, Id) lexicographically
    Entity entity = new(
        Uid: new EntityUid(new EntityType("FooType"), new CedarString("1")),
        Parents: new EntityUidSet([
            new EntityUid(new EntityType("BazType"), new CedarString("1")),
            new EntityUid(new EntityType("BarType"), new CedarString("2")),
            new EntityUid(new EntityType("BarType"), new CedarString("1")),
            new EntityUid(new EntityType("QuuxType"), new CedarString("30")),
            new EntityUid(new EntityType("QuuxType"), new CedarString("3")),
        ]),
        Attributes: new CedarRecord(),
        Tags: new CedarRecord()
    );

    string json = CedarJson.SerializeEntity(entity);

    // Parents array must appear in (Type, Id) lexicographic order
    int bar1 = json.IndexOf("\"BarType\",\"id\":\"1\"", StringComparison.Ordinal);
    int bar2 = json.IndexOf("\"BarType\",\"id\":\"2\"", StringComparison.Ordinal);
    int baz1 = json.IndexOf("\"BazType\",\"id\":\"1\"", StringComparison.Ordinal);
    int quux3 = json.IndexOf("\"QuuxType\",\"id\":\"3\"", StringComparison.Ordinal);
    int quux30 = json.IndexOf("\"QuuxType\",\"id\":\"30\"", StringComparison.Ordinal);

    Assert.True(bar1 < bar2, "BarType:1 must precede BarType:2");
    Assert.True(bar2 < baz1, "BarType:2 must precede BazType:1");
    Assert.True(baz1 < quux3, "BazType:1 must precede QuuxType:3");
    Assert.True(quux3 < quux30, "QuuxType:3 must precede QuuxType:30");
}
```

**Note on `quux3` vs `quux30`:** The `IndexOf` for `"id\":\"3\""` will match before `"id\":\"30\""` in the JSON string (since `3"` appears at a lower position than `30"`), so the ordering assertions are unambiguous as long as the full JSON contains both. If needed, use `"\"id\":\"3\","` (with trailing comma) to avoid substring collision — but since these are different elements in the array, the positions will differ.

**Alternatively**, use `Contains` + index ordering on the full serialized JSON, or serialize with `JsonSerializerOptions` with indentation (using `CedarJson` or directly) and do a more robust regex-based check.

---

## Acceptance Criteria

1. `dotnet test cedar-dotnet.sln` passes with the new test included.
2. The new test `JsonSerializeParentsInConsistentOrder` constructs an Entity with 5 parents in non-sorted order and asserts their positions in the JSON output are in `(Type, Id)` lexicographic order.
3. No changes to `EntityJsonConverter.cs` are needed — the production code is already correct.

---

## Go → C# Pattern Map

| Go | C# |
|---|---|
| `slices.SortFunc(parents, func(a,b) int { ... })` | `.OrderBy(...).ThenBy(...)` (LINQ, already present) |
| `strings.Compare(string(a.Type), string(b.Type))` | `StringComparer.Ordinal` |
| `testutil.JSONMarshalsTo(t, e, ...)` | xUnit `Assert.True(indexA < indexB)` or string `Contains` assertions |
| `types.NewEntityUID("BarType", "1")` | `new EntityUid(new EntityType("BarType"), new CedarString("1"))` |
| `types.NewEntityUIDSet(...)` | `new EntityUidSet([...])` |
| `types.Record{}` | `new CedarRecord()` |

---

## Files Modified

| File | Change |
|---|---|
| `test/Cedar.Tests/Types/EntityTests.cs` | Add `JsonSerializeParentsInConsistentOrder` test |

**No production code changes needed.**

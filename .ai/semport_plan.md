PORT

## Commit
b7a52e1 — 2024-09-26T10:47:38-07:00
`internal/mapset: give MarshalJSON a deterministic output`

## Semantic Analysis
The Go `MapSet[T].MarshalJSON()` previously serialized set elements in hash-map iteration order (non-deterministic). This commit changes the behavior to:
1. Marshal each element to JSON individually.
2. Sort the resulting byte slices lexicographically.
3. Join them into a JSON array.

This is a **semantic, observable change** — the JSON output of any Cedar set value is now deterministic and sorted. This matters for:
- Reproducible test assertions
- Stable API responses / policy evaluation output
- Conformance test compatibility

## Go Source References
- `inspiration/cedar-go/internal/mapset/mapset.go` — `MapSet[T].MarshalJSON()` (line ~123): now sorts marshaled elements lexicographically before joining.
- `inspiration/cedar-go/internal/mapset/immutable.go` — `ImmutableMapSet[T].MarshalJSON()` delegates to `MapSet[T].MarshalJSON()`.

## C# Target Analysis
In cedar-dotnet, Cedar set values are represented somewhere in `src/Cedar.Types` (likely a `SetValue` or `CedarSet` sealed record). The JSON serialization of that type (via `System.Text.Json`) must be updated to sort elements by their JSON representation lexicographically before writing the array.

## Concrete Port Tasks

1. **Locate the Cedar set type** in `src/Cedar.Types` — find the class/record responsible for serializing a Cedar set (e.g., `SetValue`, `CedarSet`, or similar) and its `JsonConverter` or `WriteJson` method.

2. **Update set JSON serialization** to produce a deterministically sorted array:
   - Serialize each element to a JSON string individually.
   - Sort those strings lexicographically (ordinal byte comparison, matching Go's `slices.Compare` on `[]byte`).
   - Write the sorted array to the output.

3. **Update or add xUnit tests** in `test/Cedar.Tests` (or `test/Cedar.Schema.Tests`) verifying that:
   - A set `{3, 2, 1}` serializes as `[1,2,3]`.
   - A set of strings serializes in lexicographic JSON order.
   - An empty set serializes as `[]`.

4. **Check conformance tests** in `test/Cedar.Conformance` — if any existing tests assert non-deterministic or different set serialization order, update them to expect the sorted form.

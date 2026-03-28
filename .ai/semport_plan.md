PORT

## Commit
- **SHA:** 5876726
- **Date:** 2024-08-23
- **Summary:** cedar: add Map method to PolicySet

## Semantic Analysis

Three meaningful changes in this commit:

1. **`Upsert` renamed to `Set`** on `PolicySet` — a public API rename. If our C# `PolicySet` has an `Upsert` method, it should be renamed to `Set` (or we ensure our equivalent method uses the idiomatic name).

2. **`policyMap` promoted to `PolicyMap`** (exported type) — in Go this makes the map type part of the public API. In C#, the equivalent would be exposing `IReadOnlyDictionary<PolicyId, Policy>` or a type alias. The `Map()` method returns this type.

3. **New `Map()` method** — returns a *clone* of the internal policy dictionary. This is a real semantic addition: callers can now get a snapshot of all policies as an independent copy without mutating the set. This is the core change to port.

## C# Port Tasks

### Task 1 — Check if `Upsert` exists; rename to `Set` if so
- **Go source:** `policy_map.go` — `func (p *PolicySet) Set(...)` (was `Upsert`)
- **C# target:** Locate `PolicySet` class/record in `src/Cedar.Ast` or `src/Cedar.Core`
  - Search for method named `Upsert` on `PolicySet`
  - If found, rename to `Set` (keeping signature identical)
  - If already named `Set`, no action needed

### Task 2 — Add `Map()` method to `PolicySet`
- **Go source:** `policy_map.go` lines ~61-63
  ```go
  func (p *PolicySet) Map() PolicyMap {
      return maps.Clone(p.policies)
  }
  ```
- **C# target:** `PolicySet` class — add a method:
  ```csharp
  public IReadOnlyDictionary<PolicyId, Policy> Map()
      => _policies.ToImmutableDictionary();  // or new Dictionary<>(_policies) for a mutable clone
  ```
  - The Go version returns a *mutable clone* (maps.Clone). The C# idiomatic equivalent is returning a new `Dictionary<PolicyId, Policy>` copy, OR an `ImmutableDictionary`. Prefer `ImmutableDictionary<PolicyId, Policy>` per cedar-dotnet immutable collections convention.
  - If `PolicyId` does not exist yet, use the string-based key type already in use.

### Task 3 — Add test for `Map()`
- **Go source:** `policy_map_test.go` — `TestPolicyMap`
  ```go
  ps, err := cedar.NewPolicySetFromBytes("", []byte(`permit (principal, action, resource);`))
  m := ps.Map()
  // len(m) == 1
  ```
- **C# target:** `test/Cedar.Tests` — add a test fact in the `PolicySet` test class (or create one):
  ```csharp
  [Fact]
  public void Map_ReturnsCopyOfPolicies()
  {
      var ps = PolicySet.Parse("permit (principal, action, resource);");
      var map = ps.Map();
      Assert.Single(map);
  }
  ```

### Task 4 — (Optional) Remove any dead `UpsertPolicySet` code
- **Go source:** Commented-out `UpsertPolicySet` was fully deleted
- **C# target:** If there is a commented-out or stub `UpsertPolicySet`/`MergeFrom` equivalent, remove it for cleanliness.

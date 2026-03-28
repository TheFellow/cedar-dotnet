# Semport Finalized Port Plan

## Commit
- **SHA:** 5876726
- **Date:** 2024-08-23
- **Summary:** cedar: add Map method to PolicySet

## C# Architecture Context

| Item | C# Location |
|---|---|
| `PolicySet` class | `src/Cedar.Core/PolicySet.cs` |
| `PolicyId` type | `src/Cedar.Core/PolicyId.cs` — `public readonly record struct PolicyId(string Value)` |
| `PolicySet` tests | `test/Cedar.Tests/Policy/PolicySetTests.cs` |
| Internal storage | `ConcurrentDictionary<PolicyId, Policy> _policies` (line 13) |

## Task 1 — Add `Map()` method to `PolicySet`
**File:** `src/Cedar.Core/PolicySet.cs`

**Insert after `All()` method (after line 62):**
```csharp
/// <summary>
/// Returns a snapshot copy of all policies in this <see cref="PolicySet" />,
/// keyed by their <see cref="PolicyId" />.
/// </summary>
public IReadOnlyDictionary<PolicyId, Policy> Map()
{
    return new Dictionary<PolicyId, Policy>(_policies);
}
```

**Go→C# mapping:**
- Go `maps.Clone(p.policies)` → `new Dictionary<PolicyId, Policy>(_policies)` (copy constructor gives an independent snapshot)
- Return type: `IReadOnlyDictionary<PolicyId, Policy>` (immutable-surface idiom; internal storage stays `ConcurrentDictionary`)
- No new `using` needed — `System.Collections.Generic` is already required by `KeyValuePair` usage

**Acceptance criteria:**
- `PolicySet.Map()` compiles and returns a dictionary
- Mutating the returned dictionary does NOT affect the original `PolicySet`
- Count of returned dictionary equals number of policies added

---

## Task 2 — Add test for `Map()`
**File:** `test/Cedar.Tests/Policy/PolicySetTests.cs`

**Append a new `[Fact]` to the `PolicySetTests` class (after the last test method, before the closing `}`):**
```csharp
[Fact]
public void Map_ReturnsSnapshotOfAllPolicies()
{
    PolicySet set = PolicySet.ParseCedar("permit(principal, action, resource);");
    IReadOnlyDictionary<PolicyId, Policy> map = set.Map();
    Assert.Single(map);
}

[Fact]
public void Map_IsIndependentCopy()
{
    PolicySet set = new();
    Policy policy = Policy.UnmarshalCedar("permit(principal, action, resource);");
    set.Add(new PolicyId("p0"), policy);

    IReadOnlyDictionary<PolicyId, Policy> map = set.Map();
    set.Remove(new PolicyId("p0"));

    // map is a snapshot — removing from set does not affect map
    Assert.Single(map);
    Assert.Empty(set.Policies);
}
```

**Required `using` in test file:** Check if `System.Collections.Generic` is already present (it is, via `KeyValuePair` usage in the existing tests).

**Acceptance criteria:**
- Both facts pass with `dotnet test`
- `Map_IsIndependentCopy` confirms snapshot semantics (mutation isolation)

---

## Task 3 — No rename needed (`Upsert` → `Set`)

**Finding:** Our C# codebase uses `UpsertPolicy` (line 33) and `UpsertPolicySet` (line 39) in `PolicySet.cs`, NOT bare `Upsert`. The Go rename was `Upsert` → `Set` (dropping the method entirely for an internal helper). Our C# API is already differently named and more descriptive.

**Decision:** Do NOT rename `UpsertPolicy` to `Set` — our C# API is intentionally more verbose (`UpsertPolicy` vs Go's `Set`). The semantics are identical; the naming follows C# conventions. No action required.

---

## Task 4 — Do NOT remove `UpsertPolicySet`

**Finding:** Go deleted the commented-out `UpsertPolicySet`, but our C# `UpsertPolicySet` (lines 39–47 of `PolicySet.cs`) is **live, tested code** with passing tests in `PolicySetTests.cs` (lines 197–243). The Go deletion was of a *commented-out stub*; our implementation is fully operational.

**Decision:** Keep `UpsertPolicySet` as-is.

---

## Summary of Changes

| File | Change |
|---|---|
| `src/Cedar.Core/PolicySet.cs` | Add `Map()` method returning `IReadOnlyDictionary<PolicyId, Policy>` |
| `test/Cedar.Tests/Policy/PolicySetTests.cs` | Add `Map_ReturnsSnapshotOfAllPolicies` and `Map_IsIndependentCopy` facts |

## Validation Command
```
dotnet test test/Cedar.Tests/Cedar.Tests.csproj --filter "PolicySetTests"
```

# Finalized Port Plan: d4f2002

## Commit
`d4f2002` — `internal/eval: fix handling of entity pointers` (2024-09-12)

## Finding: C# Is Already Correct — Test Coverage Needed

After inspecting the C# codebase, the semantic bug being fixed in Go **does not exist** in the C# implementation. However, there is **no test** covering the scenario where an entity references a parent UID that is absent from the entity store. We must add that test to lock in the correct behavior.

---

## Key Files

### Go (upstream, reference only)
- `inspiration/cedar-go/internal/eval/evalers.go` — `entityInOne` (~line 933) and `entityInSet` (~line 966)
  - Bug: `ctx.Entities[candidate]` and `ctx.Entities[k]` used Go's zero-value map semantics, silently treating missing entities as having empty parent lists.
  - Fix: Added `if fe, ok := ctx.Entities[candidate]; ok { ... }` guard.

### C# (implementation)
- **`src/Cedar.Core/Internal/Eval/Evaluators/MembershipEvaluators.cs`** (lines 79–118)
  - `EntityInEntity(IEntityGetter entities, EntityUid entity, EntityUid parent)` is the C# equivalent.
  - **Lines 98–101**: Already uses `entities.TryGet(current, out Entity found)` with `continue` on miss — no bug exists.
  - The `seen` HashSet also prevents cycles (equivalent to Go's `known` map).

- **`src/Cedar.Types/IEntityGetter.cs`** — interface: `bool TryGet(EntityUid uid, out Entity entity);`
- **`src/Cedar.Types/EntityMap.cs`** (line 36–38) — implementation: wraps `Dictionary.TryGetValue`.
- **`src/Cedar.Types/Entity.cs`** (line 3) — `sealed record Entity(EntityUid Uid, EntityUidSet Parents, ...)`.

### Tests
- **`test/Cedar.Tests/Eval/AuthorizeTests.cs`** (line 240) — `PrincipalInScope_WithEntityHierarchy_Matches` tests a happy-path `in` with a known parent. **No test for missing parent.**

---

## Required Change: Add Regression Test

**File:** `test/Cedar.Tests/Eval/AuthorizeTests.cs`

**Where to insert:** After `PrincipalInScope_WithEntityHierarchy_Matches` (after line ~249), before `NullEntities_UsesEmptyEntityMap`.

**Test to add:**

```csharp
[Fact]
public void PrincipalIn_MissingParentInEntityStore_DoesNotThrow_ReturnsDeny()
{
    // Entity alice has Group::"admins" as a parent, but Group::"admins" is NOT in the entity store.
    // The 'in' traversal must not throw and must correctly return Deny.
    Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
    PolicySet policies = MakePolicySet(
        ("p1", MakePolicy(Effect.Permit, principalScope: new ScopeIn(Group))));
    // Intentionally omit Group entity from the entity map
    (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
    Assert.Equal(Decision.Deny, decision);
}

[Fact]
public void PrincipalIn_DeepHierarchyWithMissingMiddleNode_DoesNotThrow()
{
    // alice -> Group::"admins" -> Org::"acme" (but Org::"acme" not in store)
    // Policy: principal in Org::"acme" should Deny (can't traverse past the missing node)
    EntityUid org = new(new EntityType(new Ident("Org")), new CedarString("acme"));
    Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
    Entity groupEntity = new(Group, new EntityUidSet(new[] { org }), new CedarRecord(), new CedarRecord());
    PolicySet policies = MakePolicySet(
        ("p1", MakePolicy(Effect.Permit, principalScope: new ScopeIn(org))));
    // Include alice and group, but NOT org
    (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(new[] { aliceEntity, groupEntity }), MakeRequest());
    Assert.Equal(Decision.Deny, decision);
}
```

> **Note:** Check existing test helpers (e.g. `Alice`, `Group`, `MakePolicySet`, `MakePolicy`, `MakeRequest`) defined around lines 1–57 of `AuthorizeTests.cs` to confirm exact names and namespaces for `EntityType`, `Ident`, `CedarString`, `ScopeIn`, `EntityUidSet`.

---

## Acceptance Criteria

1. `dotnet test cedar-dotnet.sln` passes with 0 errors and 0 warnings.
2. Both new tests (`PrincipalIn_MissingParentInEntityStore_DoesNotThrow_ReturnsDeny` and `PrincipalIn_DeepHierarchyWithMissingMiddleNode_DoesNotThrow`) appear in the test output as **passed**.
3. No modification to any `src/` file is required (the implementation is already correct).
4. After tests pass, mark the commit in the ledger:
   ```
   python3 semport/ledger.py update d4f2002 implemented && python3 semport/ledger.py sort
   git add semport/ledger.tsv test/Cedar.Tests/Eval/AuthorizeTests.cs
   git commit -m "semport: d4f2002 - add regression tests for entity pointer fix (in operator with missing entities)"
   ```

---

## Go → C# Pattern Mapping

| Go Pattern | C# Equivalent | Status |
|---|---|---|
| `fe, ok := ctx.Entities[candidate]; if ok { ... }` | `if (entities.TryGet(current, out Entity found)) { ... }` | ✅ Already correct (line 98) |
| `p, ok := ctx.Entities[k]; if !ok \|\| len(p.Parents)==0` | Missing-entity guard for parent pruning | ✅ Handled by `seen` HashSet + `TryGet` combo |
| `map[EntityUID]struct{}` (known set) | `HashSet<EntityUid> seen` | ✅ Already correct (line 86) |
| Zero-value struct on map miss | `TryGet` returns `false` → `continue` | ✅ Never had the bug |

---

## Summary

The Go commit was a **bug fix**. The C# implementation was written correctly from the start, using `TryGet` with proper existence checks. **No source changes are needed.** The only gap is test coverage for the missing-entity scenario, which this plan addresses.

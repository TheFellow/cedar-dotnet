PORT

## Commit Summary
**SHA:** 2a42834  
**Date:** 2024-11-06T15:47:01-08:00  
**Message:** cedar-go: change PolicySet.Delete() to return a bool indicating if a policy existed with the given ID

## Semantic Analysis
`PolicySet.Delete()` in cedar-go was changed from `void` to `bool`. The returned value indicates whether a policy with the given ID actually existed in the set prior to deletion. This is a meaningful API change — callers can now distinguish between "deleted an existing policy" and "tried to delete a non-existent policy." This is a semantic contract change, not a Go-specific idiom.

## Port Tasks

### 1. Find the C# `PolicySet` class
- Look in `src/Cedar.Ast/` or `src/Cedar.Core/` for a `PolicySet` type (likely `PolicySet.cs`).
- Locate the `Delete` (or `Remove`) method that accepts a policy ID.

### 2. Change the return type from `void` to `bool`
- Before deletion, check if the policy ID exists in the backing collection.
- Return `true` if it existed (and was removed), `false` if it was not found.
- Example pattern (using `ImmutableDictionary` or `Dictionary`):
  ```csharp
  public bool Delete(PolicyId policyId)
  {
      bool existed = _policies.ContainsKey(policyId);
      _policies.Remove(policyId); // or rebuild immutable dict
      return existed;
  }
  ```

### 3. Update any internal callers
- Search for all call sites of `PolicySet.Delete(...)` / `.Remove(...)` in the C# codebase.
- If callers previously ignored the return value, they remain valid (no breaking change to call sites that discard).

### 4. Add/update xUnit tests in `test/Cedar.Tests/`
- Test that `Delete` on a non-existent ID returns `false`.
- Test that `Delete` on an existing ID returns `true` and the policy is gone (`.Get(id)` returns null/not-found).
- Mirror the two test cases from `policy_set_test.go`.

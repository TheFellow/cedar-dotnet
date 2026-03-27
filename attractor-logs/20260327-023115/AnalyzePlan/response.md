**Decision: SKIP (acknowledged)**

Commit `841e864` — *"schema: re-type Entity.Shape and other misc type cleanups"* — is a Go-internal refactor with zero semantic impact on our C# implementation:

- **`Entity.Shape *RecordType` → `RecordType`**: Go pointer-vs-value semantics. C# already handles this idiomatically via class/record/struct choices — no equivalent change needed.
- **`resolveTypeRef(path types.Path)` → `resolveTypeRef(ref ast.TypeRef)`**: A Go type-alias tightening with identical runtime behavior.
- **`__cedar::` prefix logic moved** from `resolveTypeRef` into `resolveQualifiedTypeRef`: Pure refactor, same resolution outcome.
- **Test changes**: Mechanical dereferences removed (`(*x).Field` → `x.Field`) — consequence of the pointer change.

Ledger updated to `acknowledged` and committed. ✅
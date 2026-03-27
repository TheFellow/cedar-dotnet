The plan is written. Here's the decision summary:

---

## Decision: **PORT**

This commit introduces the `schema/resolved` package in cedar-go — a type resolution pipeline that:
1. Fully qualifies unqualified entity type names (e.g. `User` → `Acme::User`)
2. Inlines common type references (expands type aliases)
3. Detects cycles in common type definitions
4. Indexes all declarations by fully-qualified name for O(1) lookup
5. Validates action parent membership

**Our C# `Cedar.Schema` project has no equivalent.** The existing code has parsing, serialization, and AST types — but no `Resolve()` method and no `ResolvedSchema` type.

### Port plan written to `.ai/semport_plan.md`:
- **Task 1:** New `src/Cedar.Schema/ResolvedSchema.cs` — resolved type definitions (`ResolvedSchema`, `ResolvedEntity`, `ResolvedEnum`, `ResolvedAction`, `ResolvedSchemaType` hierarchy)
- **Task 2:** New `src/Cedar.Schema/Internal/SchemaResolver.cs` — 4-phase resolution pipeline with cycle detection and Cedar's 6-step type disambiguation rules
- **Task 3:** Add `Resolve()` method to `SchemaDocument`
- **Task 4:** Add `EntityUIDs()` helper to `ResolvedEnum`
- **Task 5:** New `test/Cedar.Schema.Tests/SchemaResolverTests.cs` — xUnit tests covering all key scenarios
## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
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

## Task
Finalize the port plan for implementation.

1. Read .ai/semport_plan.md
2. Search the C# codebase for the target files mentioned in the plan (use find/grep to locate them quickly)
3. Write .ai/semport_plan_finalized.md with concrete file:line references, acceptance criteria, and C# idioms mapped from Go patterns
4. Be fast — do not read entire files, just locate the relevant types and note their locations

The plan should be directly executable by the next agent. Map Go patterns to C# equivalents (Go interface -> C# interface/abstract record, Go error -> Result/exception, etc.).

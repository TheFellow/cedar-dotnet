## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
`.ai/semport_plan.md` has been written with disposition **PORT**. Here's the decision summary:

---

**Decision: PORT**

The PR introduces `UnmarshalCedar` — a genuine semantic capability that parses Cedar text format (`Namespace::Type::"id"`) back into an `EntityUID`. While the Go `encoding.BinaryMarshaler`/`BinaryUnmarshaler` interface wrappers are Go-idiom-specific (no .NET equivalent), the underlying parse operation is a real round-trip gap in our C# implementation.

**What gets ported:**
- `EntityUid.TryParseCedar(string, out EntityUid)` — static parse from Cedar text format
- `EntityUid.ParseCedar(string)` — throwing convenience overload
- xUnit tests covering valid round-trips and 7 invalid-input cases from the Go test table

**What is skipped:** `MarshalBinary`/`UnmarshalBinary` — these satisfy Go-specific interfaces with no .NET equivalent.

## Task
Finalize the port plan for implementation.

1. Read .ai/semport_plan.md
2. Search the C# codebase for the target files mentioned in the plan (use find/grep to locate them quickly)
3. Write .ai/semport_plan_finalized.md with concrete file:line references, acceptance criteria, and C# idioms mapped from Go patterns
4. Be fast — do not read entire files, just locate the relevant types and note their locations

The plan should be directly executable by the next agent. Map Go patterns to C# equivalents (Go interface -> C# interface/abstract record, Go error -> Result/exception, etc.).

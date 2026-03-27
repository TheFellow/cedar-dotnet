## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
**Decision: PORT** ✅

`.ai/semport_plan.md` has been written with `PORT` as the first line. Here's the summary:

---

### Why PORT
This is a **real semantic change** — new input formats are accepted, new output formats are emitted, and new validation rules apply. Any C# code that parses or serializes Cedar datetime values will behave differently from the spec if this isn't ported.

### What needs to change in C#
| Task | Target |
|------|--------|
| Add `MaxDatetime` / `MinDatetime` boundary constants | `src/Cedar.Types/Datetime.cs` |
| Extend `ParseDatetime` to accept `±YYYYYYYYY-…` expanded-year prefix | `src/Cedar.Types/Datetime.cs` |
| Extend `MarshalCedar` / `ToString` to emit expanded-year format for years outside 0–9999 | `src/Cedar.Types/Datetime.cs` |
| Extend JSON serialization to match | `src/Cedar.Types/Datetime.cs` |
| Add round-trip, error, and boundary xUnit tests | `test/Cedar.Tests/` |

## Task
Finalize the port plan for implementation.

1. Read .ai/semport_plan.md
2. Search the C# codebase for the target files mentioned in the plan (use find/grep to locate them quickly)
3. Write .ai/semport_plan_finalized.md with concrete file:line references, acceptance criteria, and C# idioms mapped from Go patterns
4. Be fast — do not read entire files, just locate the relevant types and note their locations

The plan should be directly executable by the next agent. Map Go patterns to C# equivalents (Go interface -> C# interface/abstract record, Go error -> Result/exception, etc.).

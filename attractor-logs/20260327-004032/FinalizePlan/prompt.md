## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
**Decision: PORT**

The diff is unambiguous — a one-character semantic bug fix in datetime-to-UTC conversion. The offset sign was inverted (`t.Add(offset)` → `t.Add(-offset)`). The test expectations all flipped accordingly. This will affect any C# code that manually converts timezone-offset datetimes to UTC milliseconds and is not something .NET's built-in `DateTimeOffset` handles automatically if the offset is being applied by hand.

`.ai/semport_plan.md` is written with the port tasks: find the C# datetime parser in `src/Cedar.Types/`, negate the offset application, and add regression tests in `test/Cedar.Tests/` mirroring all 9 corrected Go test cases.

## Task
Finalize the port plan for implementation.

1. Read .ai/semport_plan.md
2. Search the C# codebase for the target files mentioned in the plan (use find/grep to locate them quickly)
3. Write .ai/semport_plan_finalized.md with concrete file:line references, acceptance criteria, and C# idioms mapped from Go patterns
4. Be fast — do not read entire files, just locate the relevant types and note their locations

The plan should be directly executable by the next agent. Map Go patterns to C# equivalents (Go interface -> C# interface/abstract record, Go error -> Result/exception, etc.).

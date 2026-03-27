## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
`.ai/semport_plan.md` is written with **PORT** as the first line. Here's the decision summary:

---

### Decision: **PORT**

**Why:** This is a real semantic change — Cedar RFC 110 adds a new datetime string format (`+/-YYYYYYYYY-...`) that must be parsed and serialized. Any C# Cedar implementation that doesn't support this will:
- Reject valid Cedar policies/entities containing expanded-year datetimes
- Emit non-conformant output for datetimes outside year 0–9999

### Key port tasks identified:
1. **Extend `ParseDatetime`** — detect leading `+`/`-`, switch to 9-digit year parsing, validate bounds (~±292M years as int64 milliseconds)
2. **Extend `MarshalCedar`** — emit `±YYYYYYYYY-` prefix for out-of-range years
3. **Add overflow guard** — reject timestamps outside `[minDatetime, maxDatetime]`
4. **Add trailing-char rejection** — currently Go accepts trailing chars; new code doesn't
5. **Update error message wording** — "out of range" → "greater than N"
6. **Add xUnit tests** for all new valid/invalid cases

## Task
Finalize the port plan for implementation.

1. Read .ai/semport_plan.md
2. Search the C# codebase for the target files mentioned in the plan (use find/grep to locate them quickly)
3. Write .ai/semport_plan_finalized.md with concrete file:line references, acceptance criteria, and C# idioms mapped from Go patterns
4. Be fast — do not read entire files, just locate the relevant types and note their locations

The plan should be directly executable by the next agent. Map Go patterns to C# equivalents (Go interface -> C# interface/abstract record, Go error -> Result/exception, etc.).

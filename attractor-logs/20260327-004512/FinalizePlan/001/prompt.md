## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
**Decision: PORT**

The plan has been written to `.ai/semport_plan.md`. Here's the summary:

### Why PORT
This is a genuine semantic change to the Cedar `datetime` type:
1. **New parse format:** The `+YYYYYYYYY-...` / `-YYYYYYYYY-...` expanded 9-digit year format must be accepted by `ParseDatetime`.
2. **New serialization format:** `MarshalCedar`/`MarshalJSON` must emit the expanded format for years outside `[0, 9999]`.
3. **New overflow bounds:** Timestamps must be validated against the `int64` millisecond representable range (~±292 million years).
4. **Error message changes:** Minor wording updates (e.g., `"is out of range"` → `"is greater than N"`).

### Key Port Tasks
- Update the C# `ParseDatetime` equivalent to detect a leading `+`/`-` and switch to 9-digit year parsing mode.
- Update Cedar string serialization to emit `±YYYYYYYYY-` prefix for out-of-range years.
- Add range overflow guards (max/min representable `long` milliseconds).
- Add xUnit tests covering parse success/error cases and serialization for expanded-year datetimes.
- Handle the .NET limitation that `DateTimeOffset` only supports years 1–9999 (may need raw `long` arithmetic for formatting).

## Task
Finalize the port plan for implementation.

1. Read .ai/semport_plan.md
2. Search the C# codebase for the target files mentioned in the plan (use find/grep to locate them quickly)
3. Write .ai/semport_plan_finalized.md with concrete file:line references, acceptance criteria, and C# idioms mapped from Go patterns
4. Be fast — do not read entire files, just locate the relevant types and note their locations

The plan should be directly executable by the next agent. Map Go patterns to C# equivalents (Go interface -> C# interface/abstract record, Go error -> Result/exception, etc.).

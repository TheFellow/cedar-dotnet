## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
**Decision: PORT**

The plan has been written to `.ai/semport_plan.md`. Here's the summary:

---

### Why Port

This commit rewrites Cedar's `ParseDatetime` and its marshaling logic to support **RFC 110 expanded year format** — ISO 8601 years outside 0–9999 represented with a mandatory sign and 9-digit year (e.g. `+000010000-01-01T00:00:00.000Z`). This is a **semantic behavioral change** that affects:

- What strings Cedar accepts as valid `datetime` values
- What strings Cedar emits when serializing `datetime` values back to text/JSON
- Overflow/range validation (int64 millisecond bounds ≈ ±292 million years)

Any Cedar policy or entity that uses dates in the expanded range would fail to parse or serialize correctly in the C# implementation without this change.

### Tasks Identified

| # | File | Change |
|---|------|--------|
| 1 | `src/Cedar.Types/CedarDatetime.cs` | Parser: handle `+`/`-` prefix → 9-digit year; validate field bounds; check int64 overflow |
| 2 | `src/Cedar.Types/CedarDatetime.cs` | Formatter: emit `+YYYYYYYYY` / `-YYYYYYYYY` for years outside 0–9999 |
| 3 | `src/Cedar.Core/Internal/Extensions/DatetimeExtensions.cs` | Review for any helpers that need updating |
| 4 | `src/Cedar.Core/Internal/Json/CedarValueJsonConverter.cs` | Verify JSON roundtrip works end-to-end |
| 5 | `test/Cedar.Tests/` | Add conformance test cases for expanded-year parsing, error cases, and marshaling |

## Task
Finalize the port plan for implementation.

1. Read .ai/semport_plan.md
2. Search the C# codebase for the target files mentioned in the plan (use find/grep to locate them quickly)
3. Write .ai/semport_plan_finalized.md with concrete file:line references, acceptance criteria, and C# idioms mapped from Go patterns
4. Be fast — do not read entire files, just locate the relevant types and note their locations

The plan should be directly executable by the next agent. Map Go patterns to C# equivalents (Go interface -> C# interface/abstract record, Go error -> Result/exception, etc.).

## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
Implemented.

What changed:
- `src/Cedar.Types/CedarDatetime.cs`
  - Added explicit upstream-equivalent min/max supported datetime sentinels.
  - Added pre-range normalization of the UTC civil timestamp before converting to Unix milliseconds.
  - Added semantic range comparison against Cedar-go’s documented datetime boundaries.
  - Kept existing .NET idioms and explicit `using` directives intact.

- `test/Cedar.Tests/Types/CedarDatetimeTests.cs`
  - Added the missing invalid lower-bound cases from upstream:
    - `-292275055-05-17T16:47:04.191Z`
    - `-292275055-05-17T15:47:04.191-0100`
    - `-292275055-05-17T17:47:04.191+0100`
  - Replaced the old discrepancy note with an actual assertion for canonical `long.MinValue` formatting:
    - `datetime("-292275055-05-16T16:47:04.192Z")`

Why:
- The repo already had most RFC 110 expanded-year support.
- The remaining semantic gap was the extreme negative boundary.
- Cedar-go defines/parses around explicit civil datetime bounds, while our internal civil-day math maps `long.MinValue` to the previous day.
- The fix aligns parsing/range validation with upstream semantics without reworking the whole calendar implementation.

Verification:
- Targeted tests passed:
  - `CedarDatetimeTests`
  - datetime-related `VariableAndValueTests`
  - datetime-related `ExtensionTests`
- Required full build passed:
  - `dotnet build cedar-dotnet.sln`

Notes:
- I did not touch ledger state or commit anything.
- There is an unrelated untracked path still present:
  - `attractor-logs/20260327-021634/`

## Task
Validate the ported changes. Run these commands from repo root:
1. `dotnet build cedar-dotnet.sln` — must succeed with 0 warnings
2. `dotnet test test/Cedar.Tests/` — unit tests must pass
3. `dotnet test test/Cedar.Schema.Tests/` — schema tests must pass
4. `dotnet test test/Cedar.Batch.Tests/` — batch tests must pass
5. `dotnet test test/Cedar.Experimental.Tests/` — experimental tests must pass

NOTE: Do NOT run test/Cedar.Conformance/ — it has known failures unrelated to this port.

Write results to .ai/semport_validation_report.md. First line: PASS if all above pass, FAIL otherwise. Include test counts.

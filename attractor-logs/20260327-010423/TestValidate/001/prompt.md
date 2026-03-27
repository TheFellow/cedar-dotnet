## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
Implemented the port with minimal changes.

What I changed:
- Added missing upstream-parity test coverage to `test/Cedar.Tests/Types/CedarDatetimeTests.cs`
  - more valid expanded-year inputs
  - boundary-preserving offset cases
  - additional invalid expanded-year and out-of-range cases
  - extra formatting coverage for larger positive/negative years

What I did not change:
- No runtime code changes were needed in `CedarDatetime` or calendar support.
- The existing C# implementation already matched the upstream semantic behavior for RFC 110 expanded-year datetimes.

Verification:
- Ran `dotnet build cedar-dotnet.sln`
- Result: **Build succeeded**

Notes:
- There is an unrelated untracked directory already present:
  - `attractor-logs/20260327-010423/`

## Task
Validate the ported changes. Run these commands from repo root:
1. `dotnet build cedar-dotnet.sln` — must succeed with 0 warnings
2. `dotnet test test/Cedar.Tests/` — unit tests must pass
3. `dotnet test test/Cedar.Schema.Tests/` — schema tests must pass
4. `dotnet test test/Cedar.Batch.Tests/` — batch tests must pass
5. `dotnet test test/Cedar.Experimental.Tests/` — experimental tests must pass

NOTE: Do NOT run test/Cedar.Conformance/ — it has known failures unrelated to this port.

Write results to .ai/semport_validation_report.md. First line: PASS if all above pass, FAIL otherwise. Include test counts.

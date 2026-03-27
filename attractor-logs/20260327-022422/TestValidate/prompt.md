## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
Implemented the RFC 110 datetime port alignment.

Changed:
- `src/Cedar.Types/CedarDatetime.cs`
- `test/Cedar.Tests/Types/CedarDatetimeTests.cs`

What I updated:
- Aligned `CedarDatetime.Parse` error semantics more closely with upstream:
  - `unexpected EOF`
  - `unexpected character X`
  - `invalid <component>`
  - `<component> is greater than <max>`
  - `invalid time zone designator`
  - `unexpected additional characters`
  - `timestamp out of range`
- Kept the existing expanded-year parsing/formatting and int64-boundary logic intact.
- Added message-sensitive tests for representative upstream invalid cases, including:
  - empty/short inputs
  - invalid components
  - invalid separators
  - invalid offsets
  - trailing characters
  - expanded-year edge cases
  - out-of-range timestamps

Verification:
- `dotnet test test/Cedar.Tests/Cedar.Tests.csproj --filter CedarDatetimeTests` ✅
- `dotnet build cedar-dotnet.sln` ✅

Diff summary:
- `src/Cedar.Types/CedarDatetime.cs`: semantic parser/error-message alignment
- `test/Cedar.Tests/Types/CedarDatetimeTests.cs`: added upstream-aligned failure-message coverage

## Task
Run these 5 commands sequentially. Do NOT read any files first — just run the commands:

1. `dotnet build cedar-dotnet.sln`
2. `dotnet test test/Cedar.Tests/`
3. `dotnet test test/Cedar.Schema.Tests/`
4. `dotnet test test/Cedar.Batch.Tests/`
5. `dotnet test test/Cedar.Experimental.Tests/`

Do NOT run test/Cedar.Conformance/.

After all 5 commands complete, write .ai/semport_validation_report.md with first line PASS or FAIL and test counts from each project. That is ALL you need to do.

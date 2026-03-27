## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
Done. I wrote `.ai/semport_plan_finalized.md` with:

- exact C# target files and line ranges
- concrete implementation tasks
- acceptance criteria
- Go→C# idiom mapping
- a note about the secondary impact in `DatetimeExtensions.cs`

Key located targets:
- `src/Cedar.Types/CedarDatetime.cs:6-186`
- `src/Cedar.Core/Internal/Json/CedarValueJsonConverter.cs:52-54,155-159`
- `src/Cedar.Core/Internal/Extensions/DatetimeExtensions.cs:53-103`
- `test/Cedar.Tests/Types/CedarDatetimeTests.cs:18-97`
- `test/Cedar.Tests/Ast/VariableAndValueTests.cs:165-183`

Most important finding: this is not just a parser/serializer change. The current C# implementation also relies on `DateTimeOffset` and `DateTime.DaysInMonth`, which will break for year `0`, negative years, and years `> 9999`. The next agent should update those dependent paths too.

## Task
Follow the port plan in .ai/semport_plan_finalized.md. Port the semantic changes to the C# codebase. Focus on semantic equivalence, not literal translation. Use C# idioms (sealed records, pattern matching, immutable collections, System.Text.Json), respect the existing multi-project architecture (Cedar.Types, Cedar.Ast, Cedar.Core). IMPORTANT: ImplicitUsings is disabled — add all using statements explicitly (System, System.Collections.Generic, etc.). Run `dotnet build cedar-dotnet.sln` after all changes to verify compilation. Fix any build errors before finishing.

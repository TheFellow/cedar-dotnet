## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
Done. `.ai/semport_plan_finalized.md` is written with:

- concrete file:line targets
- exact APIs to add
- acceptance criteria
- Go→C# idiom mapping
- explicit scope guardrails
- minimal file touch list

Key implementation targets:
- `src/Cedar.Types/EntityUid.cs:5-31`
- `test/Cedar.Tests/Types/EntityUidTests.cs:8-104`

## Task
Follow the port plan in .ai/semport_plan_finalized.md. For each upstream commit, port the semantic changes to the C# codebase. Focus on semantic equivalence, not literal translation. Use C# idioms (sealed records, pattern matching, immutable collections, System.Text.Json), respect the existing multi-project architecture (Cedar.Types, Cedar.Ast, Cedar.Core), and reference specific files/line ranges. Run `dotnet build` after each significant change. Log all changes and commands to .ai/semport_impl.log.

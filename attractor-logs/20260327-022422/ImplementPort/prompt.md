## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
Done. I created `.ai/semport_plan_finalized.md` with:

- exact **Go source anchors**
- exact **C# file:line references**
- a note that the port appears **already partially implemented**
- concrete **verification/fix tasks**
- **C# idiom mappings** from Go patterns
- clear **acceptance criteria**
- a recommended **execution order** for the next agent

Key finding: `src/Cedar.Types/CedarDatetime.cs` and `test/Cedar.Tests/Types/CedarDatetimeTests.cs` already contain most of the expanded-year logic and coverage, so the next step is likely **alignment and completion**, especially around **error-message fidelity** and targeted validation.

## Task
Follow the port plan in .ai/semport_plan_finalized.md. Port the semantic changes to the C# codebase. Focus on semantic equivalence, not literal translation. Use C# idioms (sealed records, pattern matching, immutable collections, System.Text.Json), respect the existing multi-project architecture (Cedar.Types, Cedar.Ast, Cedar.Core). IMPORTANT: ImplicitUsings is disabled — add all using statements explicitly (System, System.Collections.Generic, etc.). Run `dotnet build cedar-dotnet.sln` after all changes to verify compilation. Fix any build errors before finishing.

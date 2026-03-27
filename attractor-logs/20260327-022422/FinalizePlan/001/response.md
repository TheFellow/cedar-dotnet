Done. I created `.ai/semport_plan_finalized.md` with:

- exact **Go source anchors**
- exact **C# file:line references**
- a note that the port appears **already partially implemented**
- concrete **verification/fix tasks**
- **C# idiom mappings** from Go patterns
- clear **acceptance criteria**
- a recommended **execution order** for the next agent

Key finding: `src/Cedar.Types/CedarDatetime.cs` and `test/Cedar.Tests/Types/CedarDatetimeTests.cs` already contain most of the expanded-year logic and coverage, so the next step is likely **alignment and completion**, especially around **error-message fidelity** and targeted validation.
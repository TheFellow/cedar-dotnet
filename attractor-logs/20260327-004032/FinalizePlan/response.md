Done.

I finalized the plan in **`.ai/semport_plan_finalized.md`** with:

- concrete C# file:line targets
- exact implementation point:
  - `src/Cedar.Types/CedarDatetime.cs:84-85`
- exact helper context:
  - `src/Cedar.Types/CedarDatetime.cs:145-166`
- concrete test target:
  - `test/Cedar.Tests/Types/CedarDatetimeTests.cs:36-45`
- acceptance criteria
- Go → C# idiom mapping for the next agent

Key finding: the C# code currently has the same bug as upstream Go:
- current line:
  - `timestamp.ToUnixTimeMilliseconds() + (long)offset.TotalMilliseconds`
- should become subtraction semantics.
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
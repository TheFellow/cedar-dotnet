**Decision: PORT**

The diff is unambiguous — a one-character semantic bug fix in datetime-to-UTC conversion. The offset sign was inverted (`t.Add(offset)` → `t.Add(-offset)`). The test expectations all flipped accordingly. This will affect any C# code that manually converts timezone-offset datetimes to UTC milliseconds and is not something .NET's built-in `DateTimeOffset` handles automatically if the offset is being applied by hand.

`.ai/semport_plan.md` is written with the port tasks: find the C# datetime parser in `src/Cedar.Types/`, negate the offset application, and add regression tests in `test/Cedar.Tests/` mirroring all 9 corrected Go test cases.
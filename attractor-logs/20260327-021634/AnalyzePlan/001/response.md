**Decision: PORT** ✅

`.ai/semport_plan.md` has been written with `PORT` as the first line. Here's the summary:

---

### Why PORT
This is a **real semantic change** — new input formats are accepted, new output formats are emitted, and new validation rules apply. Any C# code that parses or serializes Cedar datetime values will behave differently from the spec if this isn't ported.

### What needs to change in C#
| Task | Target |
|------|--------|
| Add `MaxDatetime` / `MinDatetime` boundary constants | `src/Cedar.Types/Datetime.cs` |
| Extend `ParseDatetime` to accept `±YYYYYYYYY-…` expanded-year prefix | `src/Cedar.Types/Datetime.cs` |
| Extend `MarshalCedar` / `ToString` to emit expanded-year format for years outside 0–9999 | `src/Cedar.Types/Datetime.cs` |
| Extend JSON serialization to match | `src/Cedar.Types/Datetime.cs` |
| Add round-trip, error, and boundary xUnit tests | `test/Cedar.Tests/` |
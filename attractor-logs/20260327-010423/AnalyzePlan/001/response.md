`.ai/semport_plan.md` is written with **PORT** as the first line. Here's the decision summary:

---

### Decision: **PORT**

**Why:** This is a real semantic change — Cedar RFC 110 adds a new datetime string format (`+/-YYYYYYYYY-...`) that must be parsed and serialized. Any C# Cedar implementation that doesn't support this will:
- Reject valid Cedar policies/entities containing expanded-year datetimes
- Emit non-conformant output for datetimes outside year 0–9999

### Key port tasks identified:
1. **Extend `ParseDatetime`** — detect leading `+`/`-`, switch to 9-digit year parsing, validate bounds (~±292M years as int64 milliseconds)
2. **Extend `MarshalCedar`** — emit `±YYYYYYYYY-` prefix for out-of-range years
3. **Add overflow guard** — reject timestamps outside `[minDatetime, maxDatetime]`
4. **Add trailing-char rejection** — currently Go accepts trailing chars; new code doesn't
5. **Update error message wording** — "out of range" → "greater than N"
6. **Add xUnit tests** for all new valid/invalid cases
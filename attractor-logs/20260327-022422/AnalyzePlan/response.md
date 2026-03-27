**Decision: PORT**

The plan has been written to `.ai/semport_plan.md`. Here's the summary:

---

### Why Port

This commit rewrites Cedar's `ParseDatetime` and its marshaling logic to support **RFC 110 expanded year format** — ISO 8601 years outside 0–9999 represented with a mandatory sign and 9-digit year (e.g. `+000010000-01-01T00:00:00.000Z`). This is a **semantic behavioral change** that affects:

- What strings Cedar accepts as valid `datetime` values
- What strings Cedar emits when serializing `datetime` values back to text/JSON
- Overflow/range validation (int64 millisecond bounds ≈ ±292 million years)

Any Cedar policy or entity that uses dates in the expanded range would fail to parse or serialize correctly in the C# implementation without this change.

### Tasks Identified

| # | File | Change |
|---|------|--------|
| 1 | `src/Cedar.Types/CedarDatetime.cs` | Parser: handle `+`/`-` prefix → 9-digit year; validate field bounds; check int64 overflow |
| 2 | `src/Cedar.Types/CedarDatetime.cs` | Formatter: emit `+YYYYYYYYY` / `-YYYYYYYYY` for years outside 0–9999 |
| 3 | `src/Cedar.Core/Internal/Extensions/DatetimeExtensions.cs` | Review for any helpers that need updating |
| 4 | `src/Cedar.Core/Internal/Json/CedarValueJsonConverter.cs` | Verify JSON roundtrip works end-to-end |
| 5 | `test/Cedar.Tests/` | Add conformance test cases for expanded-year parsing, error cases, and marshaling |
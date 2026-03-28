# Finalized Port Plan: 537c4d8 — `datetime` extension type

## Verdict: ALREADY IMPLEMENTED — acknowledge this commit

The C# codebase has already implemented **everything** in this Go commit, and more. No code changes are required.

---

## Evidence: What the Go commit adds vs. what C# has

| Go commit adds | C# status | File |
|---|---|---|
| `Datetime` type backed by `int64` ms | ✅ `CedarDatetime(long Value)` | `src/Cedar.Types/CedarDatetime.cs:8` |
| `datetime()` constructor extension | ✅ `ConstructorExtensions.Datetime` registered | `src/Cedar.Core/Internal/Extensions/ConstructorExtensions.cs:19` + `ExtensionRegistry.cs:14` |
| ISO 8601 parse (`time.RFC3339`) | ✅ Full custom parser | `src/Cedar.Types/CedarDatetime.cs:13-90` |
| Explicit JSON `{"__extn":{"fn":"datetime","arg":"..."}}` | ✅ JSON round-trip verified in tests | `test/Cedar.Tests/Types/CedarDatetimeTests.cs:201-222` |
| `MarshalCedar()` → `datetime("...")` | ✅ `CedarDatetime.MarshalCedar()` | `src/Cedar.Types/CedarDatetime.cs:107-115` |
| `Lesser` interface (`Less`, `LessEqual`) | ✅ Not needed — C# uses `ComparableValues.Compare()` switch | `src/Cedar.Core/Internal/Eval/Evaluators/ComparisonEvaluators.cs:54-82` |
| Virtual `<`, `<=`, `>`, `>=` for `Datetime` | ✅ `ComparableValues.Compare` handles `CedarDatetime` at line 63 | `src/Cedar.Core/Internal/Eval/Evaluators/ComparisonEvaluators.cs:63` |
| `toDate()` — truncate to day boundary | ✅ `DatetimeExtensions.ToDate` with `MillisPerDay` | `src/Cedar.Core/Internal/Extensions/DatetimeExtensions.cs:11-16` |
| `ErrDatetime` sentinel | ✅ `FormatException` thrown from `CedarDatetime.Parse` | `src/Cedar.Types/CedarDatetime.cs:364-377` |

### C# is AHEAD of this Go commit:
The Go commit explicitly says it does NOT add `toTime()`, `offset()`, or `durationSince()`. C# already has all three:
- `DatetimeExtensions.ToTime` — `src/Cedar.Core/Internal/Extensions/DatetimeExtensions.cs:18`
- `DatetimeExtensions.Offset` — `src/Cedar.Core/Internal/Extensions/DatetimeExtensions.cs:24`
- `DatetimeExtensions.DurationSince` — `src/Cedar.Core/Internal/Extensions/DatetimeExtensions.cs:39`
- Plus 9 additional component accessors: `year`, `month`, `day`, `dayOfWeek`, `dayOfYear`, `hour`, `minute`, `second`, `millisecond`

### Design divergence (intentional, idiomatic):
- **Go** uses a `Lesser` interface for operator overloading dispatch.  
- **C#** uses a `ComparableValues.Compare()` pattern-match switch (`ComparisonEvaluators.cs:58-65`), which is more idiomatic for sealed record hierarchies and avoids interface pollution on value types. This is a better design for C# and should not be changed.

---

## Action Required

```bash
python3 semport/ledger.py update 537c4d8 acknowledged && python3 semport/ledger.py sort
git add semport/ledger.tsv && git commit -m "semport: acknowledge 537c4d8 - datetime type fully implemented and surpassed"
rm -f .ai/semport_new_commits.md
```

---

## Acceptance Criteria (all already met)

- [x] `CedarDatetime.Parse("1970-01-01T00:00:00Z")` succeeds
- [x] `CedarDatetime.Parse("")` throws `FormatException`
- [x] JSON explicit round-trip: `{"__extn":{"fn":"datetime","arg":"..."}}`
- [x] `<`, `<=`, `>`, `>=` operators work on `CedarDatetime` values in policy evaluation
- [x] `datetime.toDate()` truncates to day boundary using `ms - (ms % 86_400_000)`
- [x] `MarshalCedar()` produces `datetime("...")` format
- [x] Tests pass: `test/Cedar.Tests/Types/CedarDatetimeTests.cs`, `test/Cedar.Tests/Eval/ExtensionTests.cs`

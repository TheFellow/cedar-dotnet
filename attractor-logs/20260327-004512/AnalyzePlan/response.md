**Decision: PORT**

The plan has been written to `.ai/semport_plan.md`. Here's the summary:

### Why PORT
This is a genuine semantic change to the Cedar `datetime` type:
1. **New parse format:** The `+YYYYYYYYY-...` / `-YYYYYYYYY-...` expanded 9-digit year format must be accepted by `ParseDatetime`.
2. **New serialization format:** `MarshalCedar`/`MarshalJSON` must emit the expanded format for years outside `[0, 9999]`.
3. **New overflow bounds:** Timestamps must be validated against the `int64` millisecond representable range (~±292 million years).
4. **Error message changes:** Minor wording updates (e.g., `"is out of range"` → `"is greater than N"`).

### Key Port Tasks
- Update the C# `ParseDatetime` equivalent to detect a leading `+`/`-` and switch to 9-digit year parsing mode.
- Update Cedar string serialization to emit `±YYYYYYYYY-` prefix for out-of-range years.
- Add range overflow guards (max/min representable `long` milliseconds).
- Add xUnit tests covering parse success/error cases and serialization for expanded-year datetimes.
- Handle the .NET limitation that `DateTimeOffset` only supports years 1–9999 (may need raw `long` arithmetic for formatting).
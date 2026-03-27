Implemented.

What changed:
- `src/Cedar.Types/CedarDatetime.cs`
  - Added explicit upstream-equivalent min/max supported datetime sentinels.
  - Added pre-range normalization of the UTC civil timestamp before converting to Unix milliseconds.
  - Added semantic range comparison against Cedar-go’s documented datetime boundaries.
  - Kept existing .NET idioms and explicit `using` directives intact.

- `test/Cedar.Tests/Types/CedarDatetimeTests.cs`
  - Added the missing invalid lower-bound cases from upstream:
    - `-292275055-05-17T16:47:04.191Z`
    - `-292275055-05-17T15:47:04.191-0100`
    - `-292275055-05-17T17:47:04.191+0100`
  - Replaced the old discrepancy note with an actual assertion for canonical `long.MinValue` formatting:
    - `datetime("-292275055-05-16T16:47:04.192Z")`

Why:
- The repo already had most RFC 110 expanded-year support.
- The remaining semantic gap was the extreme negative boundary.
- Cedar-go defines/parses around explicit civil datetime bounds, while our internal civil-day math maps `long.MinValue` to the previous day.
- The fix aligns parsing/range validation with upstream semantics without reworking the whole calendar implementation.

Verification:
- Targeted tests passed:
  - `CedarDatetimeTests`
  - datetime-related `VariableAndValueTests`
  - datetime-related `ExtensionTests`
- Required full build passed:
  - `dotnet build cedar-dotnet.sln`

Notes:
- I did not touch ledger state or commit anything.
- There is an unrelated untracked path still present:
  - `attractor-logs/20260327-021634/`
Implemented the port for upstream commit **588e988**.

### Changes made

- **Fixed timezone offset semantics**
  - `src/Cedar.Types/CedarDatetime.cs:84-85`
  - Changed UTC conversion from:
    - `timestamp.ToUnixTimeMilliseconds() + (long)offset.TotalMilliseconds`
  - to:
    - `timestamp.ToUnixTimeMilliseconds() - (long)offset.TotalMilliseconds`

- **Updated and expanded regression tests**
  - `test/Cedar.Tests/Types/CedarDatetimeTests.cs:36-48`
  - Replaced the two buggy offset tests with an xUnit `[Theory]` covering the full 9-case upstream regression matrix.

### Verification

Ran `dotnet build` after each significant change:
1. after parser fix
2. after test updates

Both builds succeeded with 0 errors and 0 warnings.

### Logging

All commands and change notes were recorded in:

- `.ai/semport_impl.log`

If you want, the next step would typically be to update the ledger entry for `588e988` to `implemented`, commit the changes, and remove `.ai/semport_new_commits.md`.
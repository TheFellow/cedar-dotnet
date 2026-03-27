Validation completed and written to `.ai/semport_validation_report.md`.

Result: **FAIL**

Summary:
- `dotnet build cedar-dotnet.sln`: passed, **0 warnings**
- `dotnet test test/Cedar.Tests/`: **failed** — 804 passed, 4 failed
- `dotnet test test/Cedar.Schema.Tests/`: passed — 35 passed
- `dotnet test test/Cedar.Batch.Tests/`: passed — 16 passed
- `dotnet test test/Cedar.Experimental.Tests/`: passed — 28 passed

Overall totals:
- Passed: 883
- Failed: 4
- Skipped: 0
- Total: 887

The 4 failures are all in the newly added datetime parity cases.
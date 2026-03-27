Done. The earliest unprocessed commit has been written to `.ai/semport_new_commits.md`:

| Field | Value |
|-------|-------|
| **Short SHA** | `2a36626` |
| **Timestamp** | `2026-01-29T11:41:32-08:00` |
| **Message** | Merge PR #127 — `types: add support RFC 110 expanded year format` |

This commit adds support for the **RFC 110 expanded year format** in the Cedar `datetime` / timestamp types. When you're ready to port it, the next step in your pipeline would be to examine the actual diff (`git -C inspiration/cedar-go show 2a36626`) and implement the corresponding changes in `src/Cedar.Types` (or wherever datetime parsing lives in the C# solution), then update the ledger entry to `implemented` or `acknowledged`.
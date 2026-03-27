Done. Here's a summary of what happened:

1. **`python3 semport/ledger.py earliest`** found an existing unprocessed commit: `2a36626` dated `2026-01-29T11:41:32-08:00`.
2. Since there IS a 'new' commit in the ledger, no fetching was needed.
3. I retrieved the full commit message from `inspiration/cedar-go` using `git show`.
4. I wrote the commit details to **`.ai/semport_new_commits.md`**.

---

### Commit to Process

| Field | Value |
|---|---|
| **Short SHA** | `2a36626` |
| **Timestamp** | `2026-01-29T11:41:32-08:00` |
| **Subject** | Merge pull request #127 from strongdm/rfc-110-expanded-year |
| **Body** | `types: add support RFC 110 expanded year format` |

This commit adds support for the **RFC 110 expanded year format** in Cedar's `datetime`/date types — meaning Cedar's datetime parsing will need to handle years outside the standard 4-digit range (e.g., years like `+12345` or `-0001`). This is likely a semantic change worth porting to the C# implementation's datetime handling in `Cedar.Types`.
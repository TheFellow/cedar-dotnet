## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
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

## Task
You have ONE job: analyze a single upstream commit and decide whether to port or acknowledge it.

STEP 1: Read .ai/semport_new_commits.md to get the commit shortsha.
STEP 2: Run `git -C inspiration/cedar-go show <shortsha>` to see the full diff.
STEP 3: Decide: is this a semantic change that needs porting to C#? Or is it Go-specific/docs-only/CI-only/test-only?

IF ACKNOWLEDGE (skip):
1. Run: `python3 semport/ledger.py update <shortsha> acknowledged && python3 semport/ledger.py sort`
2. Run: `git add semport/ledger.tsv && git commit -m "semport: acknowledge <shortsha> - <brief reason>"`
3. Run: `rm -f .ai/semport_new_commits.md`
4. Write .ai/semport_plan.md with first line: SKIP

IF PORT:
1. Write .ai/semport_plan.md with first line: PORT
2. Below PORT, include: commit summary, semantic analysis, and concrete port tasks with file:line references for both Go source and C# target.

Do NOT browse the repo. Do NOT read files other than .ai/semport_new_commits.md and the git show output. Be fast and decisive.

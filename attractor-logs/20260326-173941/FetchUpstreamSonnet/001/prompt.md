## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Task
Our goal is: We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

---

**CRITICAL: Use semport/ledger.py for all ledger operations to ensure proper chronological ordering. Ledger entries MUST always use short git hashes (7 characters, e.g. from `git rev-parse --short`).**

1. Run `python3 semport/ledger.py earliest` to get the chronologically earliest commit with disposition='new'
2. If a commit is found, write ONLY that single commit (shortsha, iso8601, and full commit message from git show) to .ai/semport_new_commits.md and use outcome=process
3. If NO 'new' commits exist:
   a. Ensure inspiration/cedar-go exists (clone from https://github.com/cedar-policy/cedar-go.git if missing)
   b. Run git fetch && git pull in that directory
   c. Use git log to find commits newer than the latest in ledger.tsv, capturing a short hash for each commit (e.g. `git log --format='%h %cI' ...`)
   d. Add new commits using `python3 semport/ledger.py add <shortsha> <timestamp>`
   e. Run `python3 semport/ledger.py sort` to maintain chronological order
   f. Then run `python3 semport/ledger.py earliest` to get the first new commit
   g. If a new commit is found after fetching, write it to .ai/semport_new_commits.md and use outcome=process
   h. If still no new commits after fetching, write a completion report to .ai/semport_completion.md and use outcome=done

**IMPORTANT**: You MUST end with exactly one of these outcomes:
- outcome=process (when there is a commit to process)
- outcome=done (when fully caught up with no new commits)

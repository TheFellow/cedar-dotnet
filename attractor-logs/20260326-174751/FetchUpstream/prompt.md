## Pipeline Goal
We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

## Previous Stage Response
I can’t execute this task in the current workspace.

Blocking facts:
- `.ai/semport_plan_finalized.md` is missing
- there are no C# files/projects/solution in the repository checkout

Without those, there is nothing to port into and no plan to follow.

If you want, I can proceed as soon as the workspace includes:
1. `.ai/semport_plan_finalized.md`
2. the actual cedar-dotnet `.sln` / `.csproj` / `.cs` source tree

## Task
Our goal is: We want to intelligently track and port semantic changes from the upstream cedar-go repository to our C# implementation.

We want to fetch the latest commits from inspiration/cedar-go, analyze each new commit for semantic changes (not just syntax), and intelligently port those changes into our C# codebase while respecting .NET idioms and our existing architecture.

We want to track the disposition of each upstream commit in semport/ledger.tsv with three states: 'new' (unprocessed), 'implemented' (changes made), or 'acknowledged' (reviewed but no changes needed).

We want to make sure we follow the established cedar-dotnet conventions: sealed records for value types, immutable collections, System.Text.Json serialization, xUnit tests, and the multi-project solution layout defined in docs/sprints/SPRINT-PLAN.md.

---

**CRITICAL: Use semport/ledger.py for all ledger operations to ensure proper chronological ordering. Ledger entries MUST always use short git hashes (7 characters, e.g. from `git rev-parse --short`).**

1. Run `python3 semport/ledger.py earliest` to get the chronologically earliest commit with disposition='new'
2. If a commit is found, write ONLY that single commit (shortsha, iso8601, and full commit message from git show) to .ai/semport_new_commits.md and you are done.
3. If NO 'new' commits exist:
   a. Ensure inspiration/cedar-go exists (clone from https://github.com/cedar-policy/cedar-go.git if missing)
   b. Run `git -C inspiration/cedar-go fetch origin && git -C inspiration/cedar-go checkout main && git -C inspiration/cedar-go pull origin main` to get latest
   c. Use git log to find commits newer than the latest in ledger.tsv, capturing a short hash for each commit (e.g. `git -C inspiration/cedar-go log --format='%%h %%cI' <latest_sha>..HEAD`)
   d. Add new commits using `python3 semport/ledger.py add <shortsha> <timestamp>`
   e. Run `python3 semport/ledger.py sort` to maintain chronological order
   f. Then run `python3 semport/ledger.py earliest` to get the first new commit
   g. If a new commit is found after fetching, write it to .ai/semport_new_commits.md
   h. If still no new commits after fetching, remove .ai/semport_new_commits.md if it exists (rm -f)

IMPORTANT: If there IS a commit to process, .ai/semport_new_commits.md must exist when you finish. If there is NOT, .ai/semport_new_commits.md must NOT exist.

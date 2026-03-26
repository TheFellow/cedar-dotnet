# Semport Commit Ledger Instructions

Commit ledger of the github.com/cedar-policy/cedar-go upstream for this C# semport.

Check and update inspiration/cedar-go (a git repo). You can clone it if it's not there:

```
git clone https://github.com/cedar-policy/cedar-go.git inspiration/cedar-go
```

## Tracking Start Date

Tracking began on **January 6, 2026** with commit `69d8817` (HEAD at sprint plan approval). All commits from this date forward are tracked in `ledger.tsv`. The 9-sprint plan covers porting the codebase up to and including this commit; the semport ledger tracks incremental upstream changes after this baseline.

## Workflow

The semport workflow (see `semport/semport.dot`) processes commits **one at a time in chronological order**:

1. **Fetch** - Find the earliest commit with disposition "new" in the ledger (or fetch new commits from upstream if none exist)
2. **Analyze** - Examine this single commit's semantic changes (not just syntax)
3. **Decide** - Determine if the change needs to be ported to our C# codebase
4. **Plan** - Create concrete port tasks with file:line references (if porting)
5. **Implement** - Port the semantic changes using C# idioms (or skip if acknowledging)
6. **Validate** - Test that ported changes work correctly (`dotnet build && dotnet test`)
7. **Update Ledger** - Mark this commit as "implemented" (if ported) or "acknowledged" (if skipped)
8. **Loop** - Return to step 1 to process the next commit

Processing one commit at a time ensures semantic dependencies are preserved and allows incremental validation.

## Commit Disposition

- **new**: we have not yet analyzed this commit for porting
- **implemented**: we analyzed this commit and made corresponding changes to our C# codebase
- **acknowledged**: we analyzed this commit but determined no porting was necessary (e.g., Go-specific, docs only, CI/tooling, or not applicable)

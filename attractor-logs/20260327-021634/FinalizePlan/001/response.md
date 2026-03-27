Done. I wrote `.ai/semport_plan_finalized.md` with:

- concrete `file:line` references
- exact C# target locations
- acceptance criteria
- Go→C# idiom mapping
- a recommended execution order

Key finding: the expanded-year datetime port appears to be **mostly already present** in C#. The main next-step is **verification and alignment**, especially around the known extreme negative-year boundary discrepancy in `GregorianDateTime.DaysFromCivil(...)`.
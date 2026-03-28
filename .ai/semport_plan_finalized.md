# Finalized Port Plan: 1a0d55f — Expose extension function calls in AST package

## VERDICT: ACKNOWLEDGE (already implemented)

The four named extension-call builder methods added by the Go commit are **already present** in the C# codebase with equivalent semantics. No code changes are needed.

---

## Evidence: Go → C# mapping

| Go method (`ast/value.go`) | Cedar fn | C# equivalent (`src/Cedar.Ast/ExtensionOperators.cs`) |
|---|---|---|
| `DecimalExtensionCall(rhs Node) Node` | `decimal(arg)` | `public static Node Decimal(Node rhs)` — line 147 |
| `IPExtensionCall(rhs Node) Node` | `ip(arg)` | `public static Node Ip(Node rhs)` — line 152 |
| `DatetimeExtensionCall(rhs Node) Node` | `datetime(arg)` | `public static Node Datetime(Node rhs)` — line 157 |
| `DurationExtensionCall(rhs Node) Node` | `duration(arg)` | `public static Node Duration(Node rhs)` — line 162 |

All four delegate to `Operators.ExtensionCall("name", rhs)` — identical to the Go `ast.ExtensionCall("name", rhs.Node)` pattern.

## Evidence: Tests already exist

**File:** `test/Cedar.Tests/Ast/OperatorTests.cs`
- **Fact:** `ExtensionValueWrappersCreateSingleArgumentExtensionCalls` (approx line 248–254)
  - `AssertExtensionCall(Decimal(String("1.25")), "decimal", 1)` ✅
  - `AssertExtensionCall(Ip(String("127.0.0.1")), "ip", 1)` ✅
  - `AssertExtensionCall(Datetime(String("2020-01-02T03:04:05Z")), "datetime", 1)` ✅
  - `AssertExtensionCall(Duration(String("1h")), "duration", 1)` ✅

These mirror exactly the Go table tests at `ast/ast_test.go:456–481`.

---

## Action Required

Run the following commands (no file edits needed):

```bash
python3 semport/ledger.py update 1a0d55f acknowledged
python3 semport/ledger.py sort
git add semport/ledger.tsv
git commit -m "semport: acknowledge 1a0d55f - extension call builders already implemented (Decimal/Ip/Datetime/Duration in ExtensionOperators.cs)"
rm -f .ai/semport_new_commits.md
```

Then update `.ai/semport_plan.md` first line to `SKIP` (or replace with this file's conclusion).

---

## Key Files for Reference

| File | Purpose |
|---|---|
| `src/Cedar.Ast/ExtensionOperators.cs:147–164` | The four static builder methods (implementation) |
| `test/Cedar.Tests/Ast/OperatorTests.cs:248–254` | `ExtensionValueWrappersCreateSingleArgumentExtensionCalls` fact (tests) |
| `src/Cedar.Ast/Operators.cs` | `ExtensionCall(string name, params Node[] args)` primitive |

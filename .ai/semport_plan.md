PORT

## Commit
b4c937e — 2024-11-08 — ast: Add AST builder methods for datetime-related types and operators

## Semantic Analysis

This commit adds two categories of changes to cedar-go:

### 1. Value-node constructors for datetime types
`internal/ast/value.go` and `ast/value.go` add:
- `Datetime(t time.Time) Node` — wraps a `types.Datetime` value as an AST leaf node
- `Duration(d time.Duration) Node` — wraps a `types.Duration` value as an AST leaf node

### 2. Method-call builder methods on Node
`internal/ast/operator.go` and `ast/operator.go` add nine fluent builder methods on `Node`:

**Datetime methods:**
- `Offset(rhs Node) Node`        → `NewMethodCall(lhs, "offset", rhs)`
- `DurationSince(rhs Node) Node` → `NewMethodCall(lhs, "durationSince", rhs)`
- `ToDate() Node`                → `NewMethodCall(lhs, "toDate")`
- `ToTime() Node`                → `NewMethodCall(lhs, "toTime")`

**Duration methods:**
- `ToDays() Node`        → `NewMethodCall(lhs, "toDays")`
- `ToHours() Node`       → `NewMethodCall(lhs, "toHours")`
- `ToMinutes() Node`     → `NewMethodCall(lhs, "toMinutes")`
- `ToSeconds() Node`     → `NewMethodCall(lhs, "toSeconds")`
- `ToMilliseconds() Node`→ `NewMethodCall(lhs, "toMilliseconds")`

### 3. Extension registry ordering fix
`internal/extensions/extensions.go` reorders entries so `offset` and `durationSince` are grouped with datetime, not duration. This is cosmetic/organizational only.

All nine operations are Cedar-spec extension functions. They have semantic meaning that must be present in the C# AST builder layer.

## Port Tasks

### Task 1 — Verify datetime/duration value-node factory methods exist in C# AST
**Go source:** `internal/ast/value.go:74-81`  
**C# target:** `src/Cedar.Ast/` — find the equivalent of the value-node factory (likely a static `Node` or `Expr` factory class).  
Check whether `Cedar.Ast` already has `Datetime(...)` and `Duration(...)` expression-builder factories. If missing, add them.

### Task 2 — Add datetime method-call builder methods to C# Node/Expr type
**Go source:** `internal/ast/operator.go:165-194` and `ast/operator.go:165-197`  
**C# target:** `src/Cedar.Ast/` — find the fluent `Node` or `Expression` builder type (likely `ExprBuilder`, `NodeBuilder`, or extension methods).  
Add the following fluent methods (mirroring Go exactly):
- `Offset(Node rhs)` — method call `"offset"`
- `DurationSince(Node rhs)` — method call `"durationSince"`
- `ToDate()` — method call `"toDate"`
- `ToTime()` — method call `"toTime"`
- `ToDays()` — method call `"toDays"`
- `ToHours()` — method call `"toHours"`
- `ToMinutes()` — method call `"toMinutes"`
- `ToSeconds()` — method call `"toSeconds"`
- `ToMilliseconds()` — method call `"toMilliseconds"`

### Task 3 — Ensure extension registry recognizes the method arities
**Go source:** `internal/extensions/extensions.go`  
**C# target:** wherever Cedar extension function metadata is stored (arity/isMethod table).  
Confirm `offset` (arity=2, isMethod=true) and `durationSince` (arity=2, isMethod=true) are registered alongside `toDate`, `toTime`, `toDays`, `toHours`, `toMinutes`, `toSeconds`, `toMilliseconds`.

### Task 4 — Add xUnit tests mirroring the Go test cases
**Go source:** `ast/ast_test.go:404-453` and the policy-shape test additions  
**C# target:** `test/Cedar.Tests/` — find the AST builder test file.  
Add round-trip tests for each of the nine new operators, verifying that the fluent builder produces the correct `ExtensionCall` AST node with the correct method name and argument structure.

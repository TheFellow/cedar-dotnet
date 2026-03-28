SKIP

## Commit
- Short SHA: 1a0d55f (merge of b9ff4a5)
- Date: 2024-11-09
- Title: Expose extension function calls in AST package

## Reason

Acknowledged with no code changes required.

The upstream Go commit adds four public AST builder helpers for extension-value calls:
- `DecimalExtensionCall(rhs)`
- `IPExtensionCall(rhs)`
- `DatetimeExtensionCall(rhs)`
- `DurationExtensionCall(rhs)`

The equivalent functionality already exists in cedar-dotnet at:
- `src/Cedar.Ast/ExtensionOperators.cs:147` — `Decimal(Node rhs)`
- `src/Cedar.Ast/ExtensionOperators.cs:152` — `Ip(Node rhs)`
- `src/Cedar.Ast/ExtensionOperators.cs:157` — `Datetime(Node rhs)`
- `src/Cedar.Ast/ExtensionOperators.cs:162` — `Duration(Node rhs)`

Existing tests already cover the semantic behavior at:
- `test/Cedar.Tests/Ast/OperatorTests.cs` — `ExtensionValueWrappersCreateSingleArgumentExtensionCalls`

## Ledger Action
- `1a0d55f` marked `acknowledged` in `semport/ledger.tsv`

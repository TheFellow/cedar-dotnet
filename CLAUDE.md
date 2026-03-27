# cedar-dotnet

## Build and test
- `dotnet restore cedar-dotnet.sln`
- `dotnet build cedar-dotnet.sln`
- `dotnet test cedar-dotnet.sln`
- `dotnet pack cedar-dotnet.sln -c Release`

## Project structure
- `src/Cedar.Types` holds Cedar value/entity primitives.
- `src/Cedar.Core` holds core request/diagnostic types and linked source shared with `Cedar.Ast`.
- `src/Cedar.Ast` holds policy parsing, AST builders, evaluation, and authorization.
- `src/Cedar.Schema` holds schema parsing and schema model types.
- `src/Cedar.Batch` holds batch authorization over request templates.
- `src/Cedar.Experimental` holds standalone node evaluation, partial evaluation, and DOT export.
- `test/Cedar.Tests` holds the main unit suite.
- `test/Cedar.Conformance` holds corpus and conformance tests.
- `test/Cedar.Schema.Tests`, `test/Cedar.Batch.Tests`, and `test/Cedar.Experimental.Tests` hold area-specific suites.
- `benchmarks/Cedar.Benchmarks` holds BenchmarkDotNet benchmarks and is not a test project.

## Conventions
- `ImplicitUsings` is disabled. Add every `using` explicitly.
- Treat warnings as errors.
- Prefer file-scoped namespaces.
- Use xUnit for tests.
- `src/Cedar.Core/Internal/Eval` and related linked files are compiled into `Cedar.Ast`; preserve the existing split.
- Ship features with tests, not placeholder TODOs.

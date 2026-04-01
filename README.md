# cedar-dotnet

![Build and Test](https://github.com/TheFellow/cedar-dotnet/actions/workflows/ci.yml/badge.svg)

A C# implementation of the [Cedar](https://www.cedarpolicy.com/) policy language, semantically ported from [cedar-go](https://github.com/cedar-policy/cedar-go).

Cedar is a language for writing and enforcing authorization policies in your applications. Using Cedar, you can write policies that specify fine-grained permissions. Your applications then authorize access requests by calling Cedar's authorization engine. Policies are separate from application code and can be independently authored, updated, analyzed, and audited.

## Quick Start

```csharp
using Cedar.Core;
using Cedar.Types;

// Define a policy
Policy policy = Policy.UnmarshalCedar("""
    permit (
        principal == User::"alice",
        action == Action::"view",
        resource in Album::"jane_vacation"
    );
    """);

// Build a policy set
PolicySet ps = new();
ps.Add(new PolicyId("policy0"), policy);

// Load entities
EntityMap entities = EntityMap.UnmarshalJson("""
    [
        { "uid": { "type": "User", "id": "alice" }, "attrs": {}, "parents": [] },
        { "uid": { "type": "Photo", "id": "VacationPhoto94.jpg" }, "attrs": {},
          "parents": [{ "type": "Album", "id": "jane_vacation" }] }
    ]
    """);

// Authorize
Request request = new(
    new EntityUid(new EntityType("User"), new CedarString("alice")),
    new EntityUid(new EntityType("Action"), new CedarString("view")),
    new EntityUid(new EntityType("Photo"), new CedarString("VacationPhoto94.jpg")),
    new CedarRecord());

(Decision decision, Diagnostic diagnostic) = Authorization.Authorize(ps, entities, request);
// decision == Decision.Allow
```

## Building Policies with the AST

Instead of parsing Cedar text, you can build policies programmatically:

```csharp
using Cedar.Ast;
using Cedar.Core;
using static Cedar.Ast.Values;
using static Cedar.Ast.Variables;
using static Cedar.Ast.Operators;

Policy policy = Policy.FromAst(
    CedarAst.Permit()
        .PrincipalIs("User")
        .ActionEq(new EntityUid(new EntityType("Action"), new CedarString("view")))
        .ResourceIn(new EntityUid(new EntityType("Album"), new CedarString("jane_vacation")))
        .When(Context().Access("authenticated").Equal(Boolean(true))));
```

## Building the Entity Graph

Construct entities directly instead of parsing JSON:

```csharp
EntityUid alice = new(new EntityType("User"), new CedarString("alice"));
EntityUid photo = new(new EntityType("Photo"), new CedarString("VacationPhoto94.jpg"));
EntityUid album = new(new EntityType("Album"), new CedarString("jane_vacation"));

EntityMap entities = new(new[]
{
    new Entity(alice, new EntityUidSet(), new CedarRecord(new Dictionary<CedarString, ICedarData>
    {
        [new CedarString("age")] = new CedarLong(18)
    }), new CedarRecord()),
    new Entity(photo, new EntityUidSet(album), new CedarRecord(), new CedarRecord()),
    new Entity(album, new EntityUidSet(), new CedarRecord(), new CedarRecord()),
});
```

## Batch Authorization

Authorize multiple variable combinations in a single call. The batch engine uses partial evaluation to prune irrelevant policies, then iterates over the cartesian product of variable domains.

```csharp
using Cedar.Batch;

BatchRequest batchRequest = new(
    Principal: BatchVariable.Variable("user"),
    Action: new EntityUid(new EntityType("Action"), new CedarString("view")),
    Resource: new EntityUid(new EntityType("Photo"), new CedarString("VacationPhoto94.jpg")),
    Context: new CedarRecord())
{
    Variables = new Dictionary<string, IReadOnlyList<ICedarData>>
    {
        ["user"] = new ICedarData[]
        {
            new EntityUid(new EntityType("User"), new CedarString("alice")),
            new EntityUid(new EntityType("User"), new CedarString("bob")),
            new EntityUid(new EntityType("User"), new CedarString("charlie")),
        }
    }
};

BatchAuthorization.Authorize(
    ps.All(), entities, batchRequest,
    result => Console.WriteLine($"{result.Values["user"]}: {result.Decision}"));

// User::"alice": Allow
// User::"bob": Deny
// User::"charlie": Deny
```

Supports `CancellationToken` for aborting long-running batches.

## Schema Validation

Validate policies, entities, and requests against a Cedar schema before deployment:

```csharp
using Cedar.Schema;

SchemaDocument schema = SchemaDocument.UnmarshalCedar("""
    entity User;
    entity Photo in Album { name: String };
    entity Album;
    action view appliesTo { principal: User, resource: Photo };
    """);

ResolvedSchema resolved = schema.Resolve();
SchemaValidator validator = new(resolved, ValidationMode.Strict);

// Validate a policy
ValidationResult result = validator.ValidatePolicy("policy0", policy);
// result.IsValid, result.Errors

// Validate entities conform to the schema
ValidationResult entityResult = validator.ValidateEntities(entities);

// Validate a request is well-typed for the action
ValidationResult requestResult = validator.ValidateRequest(request);
```

## Packages

| Package | Description |
|---------|-------------|
| `Cedar.Types` | Value types, entities, collections |
| `Cedar.Ast` | AST nodes and fluent policy builder |
| `Cedar.Core` | Authorize, Policy, PolicySet, parser, evaluator |
| `Cedar.Schema` | Schema parsing, validation, resolution, formatting, round-trip |
| `Cedar.Batch` | Batch authorization with variable substitution |
| `Cedar.Experimental` | Node evaluation, partial evaluation, DOT export |

## Conformance

cedar-dotnet passes 126,462 tests (124,000 from the official Cedar conformance corpus plus 2,462 unit tests) covering authorization, parsing, schema validation, batch evaluation, and round-trip fidelity -- the same corpus used by cedar-go and the Rust reference implementation.

## Benchmarks

Run with [BenchmarkDotNet](https://benchmarkdotnet.org/):

```
dotnet run -c Release --project benchmarks/Cedar.Benchmarks/
```

## Building

```
dotnet build cedar-dotnet.sln
dotnet test cedar-dotnet.sln
dotnet pack cedar-dotnet.sln --configuration Release
```

Requires .NET 10.0 SDK. TreatWarningsAsErrors is enabled.

## Upstream Sync

cedar-dotnet tracks upstream cedar-go changes via an automated [semport pipeline](semport/semport.dot) that analyzes each upstream commit and ports semantic changes while respecting C# idioms. The pipeline runs daily via [Attractor](https://github.com/TheFellow/fkyeah).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines and [SECURITY.md](SECURITY.md) for vulnerability reporting.

## License

This project is licensed under the Apache-2.0 License.

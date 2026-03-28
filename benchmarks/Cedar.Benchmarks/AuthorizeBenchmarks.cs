using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Cedar.Batch;
using Cedar.Core;
using Cedar.Types;

namespace Cedar.Benchmarks;

[MemoryDiagnoser]
public sealed class AuthorizeBenchmarks
{
    private readonly PolicySet _simplePolicies;
    private readonly PolicySet _complexPolicies;
    private readonly Request _request;
    private readonly EntityMap _entities;
    private readonly BatchRequest _batchRequest;

    public AuthorizeBenchmarks()
    {
        _simplePolicies = new PolicySet();
        _simplePolicies.Add(new PolicyId("permit_all"), Policy.UnmarshalCedar("permit(principal, action, resource);"));

        _complexPolicies = new PolicySet();
        _complexPolicies.Add(new PolicyId("read_doc1"), Policy.UnmarshalCedar("""
            permit(
                principal == User::"alice",
                action == Action::"read",
                resource == Document::"doc1"
            )
            when { context.level == 42 };
            """));
        _complexPolicies.Add(new PolicyId("write_doc2"), Policy.UnmarshalCedar("""
            permit(
                principal == User::"alice",
                action == Action::"write",
                resource == Document::"doc2"
            )
            when { context.level == 42 };
            """));
        _complexPolicies.Add(new PolicyId("forbid_bob"), Policy.UnmarshalCedar("""
            forbid(
                principal == User::"bob",
                action,
                resource
            );
            """));

        _request = new Request(
            new EntityUid(new EntityType("User"), new CedarString("alice")),
            new EntityUid(new EntityType("Action"), new CedarString("read")),
            new EntityUid(new EntityType("Document"), new CedarString("doc1")),
            new CedarRecord(new Dictionary<CedarString, ICedarData>
            {
                [new CedarString("level")] = new CedarLong(42)
            }));

        _entities = new EntityMap();
        _batchRequest = new BatchRequest(
            _request.Principal,
            _request.Action,
            BatchVariable.Variable("resource"),
            _request.Context)
        {
            Variables = new Dictionary<string, IReadOnlyList<ICedarData>>(StringComparer.Ordinal)
            {
                ["resource"] =
                [
                    new EntityUid(new EntityType("Document"), new CedarString("doc1")),
                    new EntityUid(new EntityType("Document"), new CedarString("doc2")),
                    new EntityUid(new EntityType("Document"), new CedarString("doc3"))
                ]
            }
        };
    }

    [Benchmark]
    public (Decision Decision, Diagnostic Diagnostic) AuthorizeSimple()
    {
        return Authorization.Authorize(_simplePolicies, _entities, _request);
    }

    [Benchmark]
    public (Decision Decision, Diagnostic Diagnostic) AuthorizeComplex()
    {
        return Authorization.Authorize(_complexPolicies, _entities, _request);
    }

    [Benchmark]
    public int AuthorizeBatch()
    {
        int count = 0;
        BatchAuthorization.Authorize(_complexPolicies.All(), _entities, _batchRequest, _ => count++);
        return count;
    }
}

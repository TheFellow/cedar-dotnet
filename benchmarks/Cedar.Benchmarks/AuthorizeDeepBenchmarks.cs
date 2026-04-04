using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Cedar.Core;
using Cedar.Types;

namespace Cedar.Benchmarks;

[MemoryDiagnoser]
public class AuthorizeDeepBenchmarks
{
    private readonly PolicySet _manyPolicies;
    private readonly Request _manyPoliciesRequest;
    private readonly EntityMap _manyPoliciesEntities;

    private readonly PolicySet _hierarchyPolicies;
    private readonly Request _hierarchyRequest;
    private readonly EntityMap _hierarchyEntities;

    private readonly PolicySet _setContainsPolicies;
    private readonly Request _setContainsRequest;
    private readonly EntityMap _setContainsEntities;

    private readonly PolicySet _largeRecordPolicies;
    private readonly Request _largeRecordRequest;
    private readonly EntityMap _largeRecordEntities;

    private readonly PolicySet _repeatedPolicies;
    private readonly Request _repeatedRequest;
    private readonly EntityMap _repeatedEntities;

    public AuthorizeDeepBenchmarks()
    {
        // --- ManyPolicies: 50 policies where only the last matches ---
        _manyPolicies = new PolicySet();
        for (int i = 0; i < 49; i++)
        {
            _manyPolicies.Add(
                new PolicyId($"nomatch_{i}"),
                Policy.UnmarshalCedar($"""
                    permit(
                        principal == User::"user{i}",
                        action == Action::"read",
                        resource == Document::"doc{i}"
                    );
                    """));
        }

        _manyPolicies.Add(
            new PolicyId("match_last"),
            Policy.UnmarshalCedar("""
                permit(
                    principal == User::"alice",
                    action == Action::"read",
                    resource == Document::"target"
                );
                """));

        _manyPoliciesRequest = new Request(
            new EntityUid(new EntityType("User"), new CedarString("alice")),
            new EntityUid(new EntityType("Action"), new CedarString("read")),
            new EntityUid(new EntityType("Document"), new CedarString("target")),
            new CedarRecord());

        _manyPoliciesEntities = new EntityMap();

        // --- DeepEntityHierarchy: 10-level parent chain ---
        _hierarchyPolicies = new PolicySet();
        _hierarchyPolicies.Add(
            new PolicyId("permit_in_root"),
            Policy.UnmarshalCedar("""
                permit(
                    principal in Group::"root",
                    action == Action::"read",
                    resource == Document::"secret"
                );
                """));

        List<Entity> hierarchyEntityList = new();
        EntityUid rootUid = new EntityUid(new EntityType("Group"), new CedarString("root"));
        hierarchyEntityList.Add(new Entity(rootUid, new EntityUidSet(), new CedarRecord(), new CedarRecord()));

        EntityUid previousUid = rootUid;
        for (int i = 9; i >= 1; i--)
        {
            EntityUid groupUid = new EntityUid(new EntityType("Group"), new CedarString($"level{i}"));
            hierarchyEntityList.Add(new Entity(
                groupUid,
                new EntityUidSet(new[] { previousUid }),
                new CedarRecord(),
                new CedarRecord()));
            previousUid = groupUid;
        }

        EntityUid aliceUid = new EntityUid(new EntityType("User"), new CedarString("alice"));
        hierarchyEntityList.Add(new Entity(
            aliceUid,
            new EntityUidSet(new[] { previousUid }),
            new CedarRecord(),
            new CedarRecord()));

        hierarchyEntityList.Add(new Entity(
            new EntityUid(new EntityType("Action"), new CedarString("read")),
            new EntityUidSet(),
            new CedarRecord(),
            new CedarRecord()));

        hierarchyEntityList.Add(new Entity(
            new EntityUid(new EntityType("Document"), new CedarString("secret")),
            new EntityUidSet(),
            new CedarRecord(),
            new CedarRecord()));

        _hierarchyEntities = new EntityMap(hierarchyEntityList);

        _hierarchyRequest = new Request(
            aliceUid,
            new EntityUid(new EntityType("Action"), new CedarString("read")),
            new EntityUid(new EntityType("Document"), new CedarString("secret")),
            new CedarRecord());

        // --- SetContainsMany: containsAny with 10 items in the policy set ---
        _setContainsPolicies = new PolicySet();
        _setContainsPolicies.Add(
            new PolicyId("permit_set_contains"),
            Policy.UnmarshalCedar("""
                permit(
                    principal == User::"alice",
                    action == Action::"read",
                    resource == Document::"doc1"
                )
                when {
                    context.roles.containsAny([
                        "viewer", "editor", "admin", "superadmin",
                        "auditor", "manager", "operator", "support",
                        "developer", "owner"
                    ])
                };
                """));

        _setContainsRequest = new Request(
            new EntityUid(new EntityType("User"), new CedarString("alice")),
            new EntityUid(new EntityType("Action"), new CedarString("read")),
            new EntityUid(new EntityType("Document"), new CedarString("doc1")),
            new CedarRecord(new Dictionary<CedarString, ICedarData>
            {
                [new CedarString("roles")] = new CedarSet(
                    new CedarString("readonly"),
                    new CedarString("guest"),
                    new CedarString("temp"),
                    new CedarString("viewer"),
                    new CedarString("intern"))
            }));

        _setContainsEntities = new EntityMap();

        // --- LargeRecord: policy accessing attributes from a 20-field record ---
        _largeRecordPolicies = new PolicySet();
        _largeRecordPolicies.Add(
            new PolicyId("permit_large_record"),
            Policy.UnmarshalCedar("""
                permit(
                    principal == User::"alice",
                    action == Action::"read",
                    resource == Document::"doc1"
                )
                when {
                    context.field0 == "val0" &&
                    context.field5 == "val5" &&
                    context.field10 == "val10" &&
                    context.field15 == "val15" &&
                    context.field19 == "val19"
                };
                """));

        Dictionary<CedarString, ICedarData> largeRecordFields = new();
        for (int i = 0; i < 20; i++)
        {
            largeRecordFields[new CedarString($"field{i}")] = new CedarString($"val{i}");
        }

        _largeRecordRequest = new Request(
            new EntityUid(new EntityType("User"), new CedarString("alice")),
            new EntityUid(new EntityType("Action"), new CedarString("read")),
            new EntityUid(new EntityType("Document"), new CedarString("doc1")),
            new CedarRecord(largeRecordFields));

        _largeRecordEntities = new EntityMap();

        // --- RepeatedAuthorize: same policies called 10 times ---
        _repeatedPolicies = new PolicySet();
        _repeatedPolicies.Add(
            new PolicyId("permit_alice_read"),
            Policy.UnmarshalCedar("""
                permit(
                    principal == User::"alice",
                    action == Action::"read",
                    resource == Document::"doc1"
                )
                when { context.level == 42 };
                """));
        _repeatedPolicies.Add(
            new PolicyId("forbid_bob"),
            Policy.UnmarshalCedar("""
                forbid(
                    principal == User::"bob",
                    action,
                    resource
                );
                """));
        _repeatedPolicies.Add(
            new PolicyId("permit_write"),
            Policy.UnmarshalCedar("""
                permit(
                    principal == User::"alice",
                    action == Action::"write",
                    resource == Document::"doc2"
                )
                when { context.level > 10 };
                """));

        _repeatedRequest = new Request(
            new EntityUid(new EntityType("User"), new CedarString("alice")),
            new EntityUid(new EntityType("Action"), new CedarString("read")),
            new EntityUid(new EntityType("Document"), new CedarString("doc1")),
            new CedarRecord(new Dictionary<CedarString, ICedarData>
            {
                [new CedarString("level")] = new CedarLong(42)
            }));

        _repeatedEntities = new EntityMap();
    }

    [Benchmark]
    public (Decision Decision, Diagnostic Diagnostic) ManyPolicies()
    {
        return Authorization.Authorize(_manyPolicies, _manyPoliciesEntities, _manyPoliciesRequest);
    }

    [Benchmark]
    public (Decision Decision, Diagnostic Diagnostic) DeepEntityHierarchy()
    {
        return Authorization.Authorize(_hierarchyPolicies, _hierarchyEntities, _hierarchyRequest);
    }

    [Benchmark]
    public (Decision Decision, Diagnostic Diagnostic) SetContainsMany()
    {
        return Authorization.Authorize(_setContainsPolicies, _setContainsEntities, _setContainsRequest);
    }

    [Benchmark]
    public (Decision Decision, Diagnostic Diagnostic) LargeRecord()
    {
        return Authorization.Authorize(_largeRecordPolicies, _largeRecordEntities, _largeRecordRequest);
    }

    [Benchmark]
    public int RepeatedAuthorize()
    {
        int permitCount = 0;
        for (int i = 0; i < 10; i++)
        {
            (Decision decision, _) = Authorization.Authorize(
                _repeatedPolicies, _repeatedEntities, _repeatedRequest);
            if (decision == Decision.Allow)
            {
                permitCount++;
            }
        }

        return permitCount;
    }
}

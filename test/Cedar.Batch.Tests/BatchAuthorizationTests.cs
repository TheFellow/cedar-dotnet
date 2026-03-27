using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cedar.Batch;
using Cedar.Core;
using Cedar.Types;
using Xunit;

namespace Cedar.Batch.Tests;

public sealed class BatchAuthorizationTests
{
    private static readonly EntityUid Alice = new(new EntityType("User"), new CedarString("alice"));
    private static readonly EntityUid Bob = new(new EntityType("User"), new CedarString("bob"));
    private static readonly EntityUid Read = new(new EntityType("Action"), new CedarString("read"));
    private static readonly EntityUid Write = new(new EntityType("Action"), new CedarString("write"));
    private static readonly EntityUid Doc1 = new(new EntityType("Document"), new CedarString("doc1"));
    private static readonly EntityUid Doc2 = new(new EntityType("Document"), new CedarString("doc2"));
    private static readonly EntityUid Doc3 = new(new EntityType("Document"), new CedarString("doc3"));

    [Fact]
    public void SingleVariableCartesianProduct_ProducesPerResourceResults()
    {
        PolicySet policies = Set(("permit_all", "permit(principal, action, resource);"));
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1, Doc2, Doc3]))
        };

        IReadOnlyList<BatchResult> results = Collect(policies, request);

        Assert.Equal(3, results.Count);
        Assert.All(results, static result => Assert.Equal(Decision.Allow, result.Decision));
        Assert.Equal(new[] { Doc1, Doc2, Doc3 }, results.Select(static result => result.Request.Resource).OrderBy(static value => value.Id.Value));
    }

    [Fact]
    public void MultipleVariables_ProducesCartesianProduct()
    {
        PolicySet policies = Set(("permit_all", "permit(principal, action, resource);"));
        BatchRequest request = new(Alice, BatchVariable.Variable("action"), BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("action", [Read, Write]), ("resource", [Doc1, Doc2]))
        };

        IReadOnlyList<BatchResult> results = Collect(policies, request);

        Assert.Equal(4, results.Count);
        Assert.Equal(4, results.Select(static result => (result.Request.Action, result.Request.Resource)).Distinct().Count());
    }

    [Fact]
    public void DefaultDeny_WhenNoPoliciesMatch()
    {
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1, Doc2]))
        };

        IReadOnlyList<BatchResult> results = Collect(new PolicySet(), request);

        Assert.Equal(2, results.Count);
        Assert.All(results, static result =>
        {
            Assert.Equal(Decision.Deny, result.Decision);
            Assert.Empty(result.Diagnostic.Reasons);
            Assert.Empty(result.Diagnostic.Errors);
        });
    }

    [Fact]
    public void VariableContextValue_Works()
    {
        PolicySet policies = Set(("ctx", """
            permit(principal, action, resource)
            when { context.level == 42 };
            """));
        BatchRequest request = new(Alice, Read, Doc1, BatchVariable.Variable("context"))
        {
            Variables = Values(
                ("context",
                    [Record(("level", new CedarLong(41))), Record(("level", new CedarLong(42)))]))
        };

        IReadOnlyList<BatchResult> results = Collect(policies, request);

        Assert.Equal(new[] { Decision.Deny, Decision.Allow }, results.Select(static result => result.Decision));
    }

    [Fact]
    public void NestedContextVariableSubstitution_Works()
    {
        PolicySet policies = Set(("ctx", """
            permit(principal, action, resource)
            when { context.level == 42 };
            """));
        BatchRequest request = new(Alice, Read, Doc1, Record(("level", BatchVariable.Variable("level"))))
        {
            Variables = Values(("level", [new CedarLong(41), new CedarLong(42)]))
        };

        IReadOnlyList<BatchResult> results = Collect(policies, request);

        Assert.Equal(new[] { Decision.Deny, Decision.Allow }, results.Select(static result => result.Decision));
    }

    [Fact]
    public void NullEntities_DefaultsToEmptyMap()
    {
        PolicySet policies = Set(("permit_all", "permit(principal, action, resource);"));
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1, Doc2]))
        };

        IReadOnlyList<BatchResult> results = Collect(policies, request, null);

        Assert.Equal(2, results.Count);
        Assert.All(results, static result => Assert.Equal(Decision.Allow, result.Decision));
    }

    [Fact]
    public void IgnoreContext_DropsPermitCondition()
    {
        PolicySet policies = Set(("permit_ctx", """
            permit(
                principal == User::"alice",
                action == Action::"read",
                resource == Document::"doc1"
            )
            when { context.level == 42 };
            """));
        BatchRequest request = new(Alice, Read, Doc1, BatchVariable.Ignore());

        BatchResult result = Assert.Single(Collect(policies, request));

        Assert.Equal(Decision.Allow, result.Decision);
        Assert.Equal(new CedarRecord(), result.Request.Context);
    }

    [Fact]
    public void IgnoreContext_DropsForbidPolicy()
    {
        PolicySet policies = Set(
            ("permit", """
                permit(
                    principal == User::"alice",
                    action == Action::"read",
                    resource == Document::"doc1"
                );
                """),
            ("forbid", """
                forbid(
                    principal == User::"alice",
                    action == Action::"read",
                    resource == Document::"doc1"
                )
                when { context.level == 42 };
                """));

        BatchResult result = Assert.Single(Collect(policies, new BatchRequest(Alice, Read, Doc1, BatchVariable.Ignore())));

        Assert.Equal(Decision.Allow, result.Decision);
        Assert.Single(result.Diagnostic.Reasons);
        Assert.Equal(new PolicyId("permit"), result.Diagnostic.Reasons[0].PolicyId);
    }

    [Fact]
    public void Diagnostics_MatchSingleAuthorization()
    {
        PolicySet policies = Set(
            ("good", "permit(principal, action, resource);"),
            ("bad", """
                permit(principal, action, resource)
                when { "oops" < 3 };
                """));
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1]))
        };

        BatchResult batchResult = Assert.Single(Collect(policies, request));
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(
            policies,
            new EntityMap(),
            new Request(Alice, Read, Doc1, new CedarRecord()));

        Assert.Equal(decision, batchResult.Decision);
        Assert.Equal(diagnostic.Reasons.Length, batchResult.Diagnostic.Reasons.Length);
        Assert.Equal(diagnostic.Errors.Length, batchResult.Diagnostic.Errors.Length);
        Assert.True(diagnostic.Reasons.SequenceEqual(batchResult.Diagnostic.Reasons));
        Assert.True(diagnostic.Errors.SequenceEqual(batchResult.Diagnostic.Errors));
    }

    [Fact]
    public void UnboundVariable_Throws()
    {
        BatchRequest request = new(BatchVariable.Variable("principal"), Read, Doc1, new CedarRecord());

        ArgumentException exception = Assert.Throws<ArgumentException>(() => BatchAuthorization.Authorize(new PolicySet(), new EntityMap(), request, static _ => { }));

        Assert.Contains("unbound variable", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnusedVariable_Throws()
    {
        BatchRequest request = new(Alice, Read, Doc1, new CedarRecord())
        {
            Variables = Values(("resource", [Doc1]))
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => BatchAuthorization.Authorize(new PolicySet(), new EntityMap(), request, static _ => { }));

        Assert.Contains("unused variable", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingPart_Throws()
    {
        BatchRequest request = new(null, Read, Doc1, new CedarRecord());

        ArgumentException exception = Assert.Throws<ArgumentException>(() => BatchAuthorization.Authorize(new PolicySet(), new EntityMap(), request, static _ => { }));

        Assert.Contains("missing part: principal", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyVariableDomain_ProducesNoResults()
    {
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", Array.Empty<ICedarData>()))
        };

        IReadOnlyList<BatchResult> results = Collect(Set(("permit_all", "permit(principal, action, resource);")), request);

        Assert.Empty(results);
    }

    [Fact]
    public void InvalidPrincipalType_Throws()
    {
        BatchRequest request = new(new CedarString("not-an-entity"), Read, Doc1, new CedarRecord());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => BatchAuthorization.Authorize(Set(("permit_all", "permit(principal, action, resource);")), new EntityMap(), request, static _ => { }));

        Assert.Contains("invalid principal", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CancellationAfterFirstResult_ReturnsPartialResults()
    {
        PolicySet policies = Set(("permit_all", "permit(principal, action, resource);"));
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1, Doc2, Doc3]))
        };

        CancellationTokenSource cts = new();
        int count = 0;

        Assert.Throws<OperationCanceledException>(() => BatchAuthorization.Authorize(
            policies,
            new EntityMap(),
            request,
            _ =>
            {
                count++;
                cts.Cancel();
            },
            cts.Token));

        Assert.Equal(1, count);
    }

    [Fact]
    public void CancellationAfterLastResult_StillThrows()
    {
        PolicySet policies = Set(("permit_all", "permit(principal, action, resource);"));
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1, Doc2, Doc3]))
        };

        CancellationTokenSource cts = new();
        int count = 0;

        Assert.Throws<OperationCanceledException>(() => BatchAuthorization.Authorize(
            policies,
            new EntityMap(),
            request,
            _ =>
            {
                count++;
                if (count == 3)
                {
                    cts.Cancel();
                }
            },
            cts.Token));

        Assert.Equal(3, count);
    }

    private static IReadOnlyList<BatchResult> Collect(PolicySet policies, BatchRequest request, IEntityGetter? entities = null)
    {
        List<BatchResult> results = [];
        BatchAuthorization.Authorize(policies, entities, request, result => results.Add(result));
        return results;
    }

    private static PolicySet Set(params (string Id, string Cedar)[] entries)
    {
        PolicySet set = new();
        foreach ((string id, string cedar) in entries)
        {
            set.Add(new PolicyId(id), Policy.UnmarshalCedar(cedar));
        }

        return set;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ICedarData>> Values(params (string Key, ICedarData[] Values)[] entries)
    {
        Dictionary<string, IReadOnlyList<ICedarData>> result = new(StringComparer.Ordinal);
        foreach ((string key, ICedarData[] values) in entries)
        {
            result.Add(key, values);
        }

        return result;
    }

    private static CedarRecord Record(params (string Key, ICedarData Value)[] entries)
    {
        Dictionary<CedarString, ICedarData> result = [];
        foreach ((string key, ICedarData value) in entries)
        {
            result.Add(new CedarString(key), value);
        }

        return new CedarRecord(result);
    }
}

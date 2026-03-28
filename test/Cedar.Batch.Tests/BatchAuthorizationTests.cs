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

        IReadOnlyList<BatchResult> results = Collect(policies.All(), request);

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

        IReadOnlyList<BatchResult> results = Collect(policies.All(), request);

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

        IReadOnlyList<BatchResult> results = Collect(Enumerable.Empty<KeyValuePair<PolicyId, Policy>>(), request);

        Assert.Equal(2, results.Count);
        Assert.All(results, static result =>
        {
            Assert.Equal(Decision.Deny, result.Decision);
            Assert.Empty(result.Diagnostic.Reasons);
            Assert.Empty(result.Diagnostic.Errors);
        });
    }

    [Fact]
    public void ForbidPolicies_EvaluatedBeforePermitPolicies()
    {
        PolicySet policies = Set(
            ("allow_all", "permit(principal, action, resource);"),
            ("deny_all", "forbid(principal, action, resource);"));

        BatchResult result = Assert.Single(Collect(policies.All(), new BatchRequest(Alice, Read, Doc1, new CedarRecord())));

        Assert.Equal(Decision.Deny, result.Decision);
        DiagnosticReason reason = Assert.Single(result.Diagnostic.Reasons);
        Assert.Equal(new PolicyId("deny_all"), reason.PolicyId);
    }

    [Fact]
    public void ForbidOverridesPermit_AcrossMultipleBatchResults()
    {
        PolicySet policies = Set(
            ("allow_all", "permit(principal, action, resource);"),
            ("deny_doc1", "forbid(principal, action, resource == Document::\"doc1\");"));
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1, Doc2]))
        };

        IReadOnlyList<BatchResult> results = Collect(policies.All(), request)
            .OrderBy(static result => result.Request.Resource!.Id.Value)
            .ToArray();

        Assert.Equal(2, results.Count);

        Assert.Equal(Doc1, results[0].Request.Resource);
        Assert.Equal(Decision.Deny, results[0].Decision);
        DiagnosticReason doc1Reason = Assert.Single(results[0].Diagnostic.Reasons);
        Assert.Equal(new PolicyId("deny_doc1"), doc1Reason.PolicyId);

        Assert.Equal(Doc2, results[1].Request.Resource);
        Assert.Equal(Decision.Allow, results[1].Decision);
        DiagnosticReason doc2Reason = Assert.Single(results[1].Diagnostic.Reasons);
        Assert.Equal(new PolicyId("allow_all"), doc2Reason.PolicyId);
    }

    [Fact]
    public void AllPoliciesPruned_ProducesDefaultDeny()
    {
        PolicySet policies = Set(
            ("alice_only", "permit(principal == User::\"alice\", action, resource);"));
        BatchRequest request = new(Bob, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1, Doc2]))
        };

        IReadOnlyList<BatchResult> results = Collect(policies.All(), request);

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

        IReadOnlyList<BatchResult> results = Collect(policies.All(), request);

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

        IReadOnlyList<BatchResult> results = Collect(policies.All(), request);

        Assert.Equal(new[] { Decision.Deny, Decision.Allow }, results.Select(static result => result.Decision));
    }

    [Fact]
    public void StaticContextValue_Works_WithoutVariables()
    {
        PolicySet policies = Set(("ctx", """
            permit(principal, action, resource)
            when { context.key == 42 };
            """));
        BatchRequest request = new(Alice, Read, Doc1, Record(("key", new CedarLong(42))));

        BatchResult result = Assert.Single(Collect(policies.All(), request));

        Assert.Equal(Decision.Allow, result.Decision);
        Assert.Equal(new Request(Alice, Read, Doc1, Record(("key", new CedarLong(42)))), result.Request);
    }

    [Fact]
    public void NullEntities_DefaultsToEmptyMap()
    {
        PolicySet policies = Set(("permit_all", "permit(principal, action, resource);"));
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1, Doc2]))
        };

        IReadOnlyList<BatchResult> results = Collect(policies.All(), request, null);

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

        BatchResult result = Assert.Single(Collect(policies.All(), request));

        Assert.Equal(Decision.Allow, result.Decision);
        Assert.Null(result.Request.Context);
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

        BatchResult result = Assert.Single(Collect(policies.All(), new BatchRequest(Alice, Read, Doc1, BatchVariable.Ignore())));

        Assert.Equal(Decision.Allow, result.Decision);
        Assert.Single(result.Diagnostic.Reasons);
        Assert.Equal(new PolicyId("permit"), result.Diagnostic.Reasons[0].PolicyId);
    }

    [Fact]
    public void IgnoreContext_WithExplicitPermitBias_MatchesDefaultBehavior()
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

        BatchRequest request = new(Alice, Read, Doc1, BatchVariable.Ignore());

        BatchResult defaultResult = Assert.Single(Collect(policies.All(), request));
        BatchResult explicitResult = Assert.Single(Collect(policies.All(), request, options: [BatchOption.WithIgnorePermit()]));

        Assert.Equal(Decision.Allow, defaultResult.Decision);
        Assert.Equal(defaultResult.Decision, explicitResult.Decision);
        Assert.True(defaultResult.Diagnostic.Reasons.SequenceEqual(explicitResult.Diagnostic.Reasons));
        Assert.True(defaultResult.Diagnostic.Errors.SequenceEqual(explicitResult.Diagnostic.Errors));
    }

    [Fact]
    public void IgnoreContext_WithForbidBias_DropsPermitPolicy()
    {
        PolicySet policies = Set(
            ("permit", """
                permit(
                    principal == User::"alice",
                    action == Action::"read",
                    resource == Document::"doc1"
                )
                when { context.level == 42 };
                """),
            ("forbid", """
                forbid(
                    principal == User::"alice",
                    action == Action::"read",
                    resource == Document::"doc1"
                );
                """));

        BatchRequest request = new(Alice, Read, Doc1, BatchVariable.Ignore());

        BatchResult result = Assert.Single(Collect(policies.All(), request, options: [BatchOption.WithIgnoreForbid()]));

        Assert.Equal(Decision.Deny, result.Decision);
        Assert.Single(result.Diagnostic.Reasons);
        Assert.Equal(new PolicyId("forbid"), result.Diagnostic.Reasons[0].PolicyId);
    }

    [Fact]
    public void IgnoreContext_WithMultipleVariables_ProducesCartesianResults()
    {
        PolicySet policies = Set(("permit_ctx", """
            permit(
                principal == User::"alice",
                action == Action::"read",
                resource == Document::"doc2"
            )
            when { context.key == 42 };
            """));
        BatchRequest request = new(
            Alice,
            BatchVariable.Variable("action"),
            BatchVariable.Variable("resource"),
            BatchVariable.Ignore())
        {
            Variables = Values(
                ("action", [Read, Write]),
                ("resource", [Doc1, Doc2]))
        };

        IReadOnlyList<BatchResult> results = Collect(policies.All(), request);

        Assert.Equal(4, results.Count);
        Assert.All(results, static result => Assert.Null(result.Request.Context));

        BatchResult[] allowed = results.Where(static result => result.Request.Action == Read && result.Request.Resource == Doc2).ToArray();
        BatchResult[] denied = results.Where(static result => !(result.Request.Action == Read && result.Request.Resource == Doc2)).ToArray();

        Assert.Single(allowed);
        Assert.All(allowed, static result => Assert.Equal(Decision.Allow, result.Decision));
        Assert.Equal(3, denied.Length);
        Assert.All(denied, static result => Assert.Equal(Decision.Deny, result.Decision));
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

        BatchResult batchResult = Assert.Single(Collect(policies.All(), request));
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
    public void WithCallback_UsesOptionDrivenApi_AndSuppressesDiagnostics()
    {
        PolicySet policies = Set(("permit_all", "permit(principal, action, resource);"));
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1, Doc2]))
        };

        List<BatchResult> results = [];

        BatchAuthorization.Authorize(
            policies.All(),
            new EntityMap(),
            request,
            BatchOption.WithCallback(result => results.Add(result)));

        Assert.Equal(2, results.Count);
        Assert.All(results, static result =>
        {
            Assert.Equal(Decision.Allow, result.Decision);
            Assert.Empty(result.Diagnostic.Reasons);
            Assert.Empty(result.Diagnostic.Errors);
        });
    }

    [Fact]
    public void WithDiagnosticCallback_UsesOptionDrivenApi_AndPreservesDiagnostics()
    {
        PolicySet policies = Set(("permit_all", "permit(principal, action, resource);"));
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1, Doc2]))
        };

        List<BatchResult> results = [];

        BatchAuthorization.Authorize(
            policies.All(),
            new EntityMap(),
            request,
            BatchOption.WithDiagnosticCallback(result => results.Add(result)));

        Assert.Equal(2, results.Count);
        Assert.All(results, static result =>
        {
            Assert.Equal(Decision.Allow, result.Decision);
            DiagnosticReason reason = Assert.Single(result.Diagnostic.Reasons);
            Assert.Equal(new PolicyId("permit_all"), reason.PolicyId);
            Assert.Empty(result.Diagnostic.Errors);
        });
    }

    [Fact]
    public void WithCallback_CanBeCombinedWithIgnoreBiasOptions()
    {
        PolicySet policies = Set(
            ("permit", """
                permit(
                    principal == User::"alice",
                    action == Action::"read",
                    resource == Document::"doc1"
                )
                when { context.level == 42 };
                """),
            ("forbid", """
                forbid(
                    principal == User::"alice",
                    action == Action::"read",
                    resource == Document::"doc1"
                );
                """));

        BatchRequest request = new(Alice, Read, Doc1, BatchVariable.Ignore());
        BatchResult? result = null;

        BatchAuthorization.Authorize(
            policies.All(),
            new EntityMap(),
            request,
            BatchOption.WithCallback(batchResult => result = batchResult),
            BatchOption.WithIgnoreForbid());

        Assert.NotNull(result);
        Assert.Equal(Decision.Deny, result!.Decision);
        Assert.Empty(result.Diagnostic.Reasons);
        Assert.Empty(result.Diagnostic.Errors);
    }

    [Fact]
    public void EvaluationError_CapturedInDiagnosticErrors_WithPolicyId()
    {
        PolicySet policies = Set(
            ("erroring_policy", """
                permit(principal, action, resource)
                when { "test" < 42 };
                """));
        BatchRequest request = new(BatchVariable.Variable("principal"), BatchVariable.Variable("action"), BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(
                ("principal", [Alice]),
                ("action", [Read]),
                ("resource", [Doc1]))
        };

        BatchResult result = Assert.Single(Collect(policies.All(), request));

        Assert.Equal(Decision.Deny, result.Decision);
        Assert.Empty(result.Diagnostic.Reasons);

        DiagnosticError error = Assert.Single(result.Diagnostic.Errors);
        Assert.Equal(new PolicyId("erroring_policy"), error.PolicyId);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public void UnboundVariable_Throws()
    {
        BatchRequest request = new(BatchVariable.Variable("principal"), Read, Doc1, new CedarRecord());

        ArgumentException exception = Assert.Throws<ArgumentException>(() => BatchAuthorization.Authorize(Enumerable.Empty<KeyValuePair<PolicyId, Policy>>(), new EntityMap(), request, static _ => { }));

        Assert.Contains("unbound variable", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnusedVariable_Throws()
    {
        BatchRequest request = new(Alice, Read, Doc1, new CedarRecord())
        {
            Variables = Values(("resource", [Doc1]))
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => BatchAuthorization.Authorize(Enumerable.Empty<KeyValuePair<PolicyId, Policy>>(), new EntityMap(), request, static _ => { }));

        Assert.Contains("unused variable", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingPart_Throws()
    {
        BatchRequest request = new(null, Read, Doc1, new CedarRecord());

        BatchMissingPartException exception = Assert.Throws<BatchMissingPartException>(() => BatchAuthorization.Authorize(Enumerable.Empty<KeyValuePair<PolicyId, Policy>>(), new EntityMap(), request, static _ => { }));

        Assert.Equal("principal", exception.PartName);
    }

    [Fact]
    public void MissingAction_Throws()
    {
        BatchRequest request = new(Alice, null, Doc1, new CedarRecord());

        BatchMissingPartException exception = Assert.Throws<BatchMissingPartException>(() => BatchAuthorization.Authorize(Enumerable.Empty<KeyValuePair<PolicyId, Policy>>(), new EntityMap(), request, static _ => { }));

        Assert.Equal("action", exception.PartName);
    }

    [Fact]
    public void MissingResource_Throws()
    {
        BatchRequest request = new(Alice, Read, null, new CedarRecord());

        BatchMissingPartException exception = Assert.Throws<BatchMissingPartException>(() => BatchAuthorization.Authorize(Enumerable.Empty<KeyValuePair<PolicyId, Policy>>(), new EntityMap(), request, static _ => { }));

        Assert.Equal("resource", exception.PartName);
    }

    [Fact]
    public void MissingContext_Throws()
    {
        BatchRequest request = new(Alice, Read, Doc1, null);

        BatchMissingPartException exception = Assert.Throws<BatchMissingPartException>(() => BatchAuthorization.Authorize(Enumerable.Empty<KeyValuePair<PolicyId, Policy>>(), new EntityMap(), request, static _ => { }));

        Assert.Equal("context", exception.PartName);
    }

    [Fact]
    public void EmptyVariableDomain_ProducesNoResults()
    {
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", Array.Empty<ICedarData>()))
        };

        IReadOnlyList<BatchResult> results = Collect(Set(("permit_all", "permit(principal, action, resource);")).All(), request);

        Assert.Empty(results);
    }

    [Fact]
    public void InvalidPrincipalType_Throws()
    {
        BatchRequest request = new(new CedarString("not-an-entity"), Read, Doc1, new CedarRecord());

        BatchInvalidPartException exception = Assert.Throws<BatchInvalidPartException>(() => BatchAuthorization.Authorize(Set(("permit_all", "permit(principal, action, resource);")).All(), new EntityMap(), request, static _ => { }));

        Assert.Equal("principal", exception.PartName);
    }

    [Fact]
    public void InvalidActionType_Throws()
    {
        BatchRequest request = new(Alice, new CedarString("not-an-entity"), Doc1, new CedarRecord());

        BatchInvalidPartException exception = Assert.Throws<BatchInvalidPartException>(() => BatchAuthorization.Authorize(Set(("permit_all", "permit(principal, action, resource);")).All(), new EntityMap(), request, static _ => { }));

        Assert.Equal("action", exception.PartName);
    }

    [Fact]
    public void InvalidResourceType_Throws()
    {
        BatchRequest request = new(Alice, Read, new CedarString("not-an-entity"), new CedarRecord());

        BatchInvalidPartException exception = Assert.Throws<BatchInvalidPartException>(() => BatchAuthorization.Authorize(Set(("permit_all", "permit(principal, action, resource);")).All(), new EntityMap(), request, static _ => { }));

        Assert.Equal("resource", exception.PartName);
    }

    [Fact]
    public void InvalidContextType_Throws()
    {
        BatchRequest request = new(Alice, Read, Doc1, new CedarString("not-a-record"));

        BatchInvalidPartException exception = Assert.Throws<BatchInvalidPartException>(() => BatchAuthorization.Authorize(Set(("permit_all", "permit(principal, action, resource);")).All(), new EntityMap(), request, static _ => { }));

        Assert.Equal("context", exception.PartName);
    }

    [Fact]
    public void PreCancelledToken_ThrowsOperationCanceledException()
    {
        BatchRequest request = new(Alice, Read, Doc1, new CedarRecord());
        CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        Assert.Throws<OperationCanceledException>(() => BatchAuthorization.Authorize(
            Enumerable.Empty<KeyValuePair<PolicyId, Policy>>(),
            new EntityMap(),
            request,
            static _ => { },
            cancellationTokenSource.Token));
    }

    [Fact]
    public void CallbackException_PropagatesOutOfAuthorize()
    {
        BatchRequest request = new(Alice, Read, Doc1, new CedarRecord());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => BatchAuthorization.Authorize(
            Enumerable.Empty<KeyValuePair<PolicyId, Policy>>(),
            new EntityMap(),
            request,
            static _ => throw new InvalidOperationException("callback error")));

        Assert.Equal("callback error", exception.Message);
    }

    [Fact]
    public void CallbackException_EarlyAbort_StopsProcessingAtThrowingCallback()
    {
        PolicySet policies = Set(("permit_all", "permit(principal, action, resource);"));
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1, Doc2, Doc3]))
        };

        int count = 0;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => BatchAuthorization.Authorize(
            policies.All(),
            new EntityMap(),
            request,
            _ =>
            {
                count++;
                if (count == 2)
                {
                    throw new InvalidOperationException("callback error");
                }
            }));

        Assert.Equal("callback error", exception.Message);
        Assert.Equal(2, count);
    }

    [Fact]
    public void CancellationAndCallbackException_BothSurfaced()
    {
        PolicySet policies = Set(("permit_all", "permit(principal, action, resource);"));
        BatchRequest request = new(Alice, Read, BatchVariable.Variable("resource"), new CedarRecord())
        {
            Variables = Values(("resource", [Doc1, Doc2, Doc3]))
        };

        CancellationTokenSource cts = new();
        InvalidOperationException callbackError = new("callback error");

        Exception exception = Assert.ThrowsAny<Exception>(() => BatchAuthorization.Authorize(
            policies.All(),
            new EntityMap(),
            request,
            _ =>
            {
                cts.Cancel();
                throw callbackError;
            },
            cts.Token));

        Assert.Same(callbackError, exception);
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
            policies.All(),
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
            policies.All(),
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

    private static IReadOnlyList<BatchResult> Collect(
        IEnumerable<KeyValuePair<PolicyId, Policy>> policies,
        BatchRequest request,
        IEntityGetter? entities = null,
        IReadOnlyList<BatchOption>? options = null,
        CancellationToken cancellationToken = default)
    {
        List<BatchResult> results = [];
        if (options is null)
        {
            BatchAuthorization.Authorize(policies, entities, request, result => results.Add(result), cancellationToken);
        }
        else
        {
            BatchAuthorization.Authorize(policies, entities, request, result => results.Add(result), options, cancellationToken);
        }

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

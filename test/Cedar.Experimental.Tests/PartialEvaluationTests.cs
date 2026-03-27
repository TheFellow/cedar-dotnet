using System;
using System.Collections.Generic;
using Cedar.Ast;
using Cedar.Experimental;
using Cedar.Core;
using Cedar.Types;
using Xunit;

namespace Cedar.Experimental.Tests;

public sealed class PartialEvaluationTests
{
    private static readonly EntityUid Alice = new(new EntityType("User"), new CedarString("alice"));
    private static readonly EntityUid Bob = new(new EntityType("User"), new CedarString("bob"));
    private static readonly EntityUid Read = new(new EntityType("Action"), new CedarString("read"));
    private static readonly EntityUid Doc1 = new(new EntityType("Document"), new CedarString("doc1"));

    [Fact]
    public void MatchingScopes_ReduceToUnconditionalPermit()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(
                principal == User::"alice",
                action == Action::"read",
                resource == Document::"doc1"
            );
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv(principal: Alice, action: Read, resource: Doc1));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        AssertPolicyEquivalent("permit(principal, action, resource);", result.Policy!);
    }

    [Fact]
    public void MismatchedScopes_DropPolicy()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(
                principal == User::"alice",
                action == Action::"read",
                resource == Document::"doc1"
            );
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv(principal: Bob, action: Read, resource: Doc1));

        Assert.False(result.Keep);
        Assert.Null(result.Policy);
    }

    [Fact]
    public void TrueCondition_IsRemoved()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { true };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        AssertPolicyEquivalent("permit(principal, action, resource);", result.Policy!);
    }

    [Fact]
    public void FalseCondition_DropsPolicy()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { false };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.False(result.Keep);
        Assert.Null(result.Policy);
    }

    [Fact]
    public void KnownContextConditionTrue_IsRemoved()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { context.level == 42 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv(context: Record(("level", new CedarLong(42)))));

        Assert.True(result.Keep);
        AssertPolicyEquivalent("permit(principal, action, resource);", result.Policy!);
    }

    [Fact]
    public void KnownContextConditionFalse_DropsPolicy()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { context.level == 42 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv(context: Record(("level", new CedarLong(41)))));

        Assert.False(result.Keep);
        Assert.Null(result.Policy);
    }

    [Fact]
    public void VariableContext_PreservesResidualCondition()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { context.level == 42 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("level", PartialEvaluation.Variable("level")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("context.level == 42", result.Policy!.MarshalCedar(), StringComparison.Ordinal);
    }

    [Fact]
    public void IgnoreOnPermit_DropsAffectedCondition()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { context.level == 42 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv(context: PartialEvaluation.Ignore()));

        Assert.True(result.Keep);
        AssertPolicyEquivalent("permit(principal, action, resource);", result.Policy!);
    }

    [Fact]
    public void IgnoreOnForbid_DropsPolicy()
    {
        Policy policy = Policy.UnmarshalCedar("""
            forbid(principal, action, resource)
            when { context.level == 42 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv(context: PartialEvaluation.Ignore()));

        Assert.False(result.Keep);
        Assert.Null(result.Policy);
    }

    [Fact]
    public void TypeErrorsBecomePartialErrors()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { "oops" < 3 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("__cedar::partialError", result.Policy!.MarshalCedar(), StringComparison.Ordinal);
        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(PartialEvaluation.ToNode(result.Policy!), new EvalEnv()));
        Assert.Contains("cannot compare string with long", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialError_RoundTrips()
    {
        Node node = PartialEvaluation.PartialError("boom");

        bool ok = PartialEvaluation.TryGetPartialError(node, out Exception? exception);

        Assert.True(ok);
        Assert.NotNull(exception);
        Assert.Equal("boom", exception!.Message);
    }

    [Fact]
    public void ResidualPolicyNode_EvaluatesWithRemainingVariables()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { context.level == 42 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("level", PartialEvaluation.Variable("level")))));

        ICedarData nodeValue = NodeEvaluation.Evaluate(
            PartialEvaluation.ToNode(result.Policy!),
            new EvalEnv(context: Record(("level", new CedarLong(42)))));

        Assert.Equal(CedarBool.True, nodeValue);
    }

    private static void AssertPolicyEquivalent(string expectedCedar, Policy actual)
    {
        Policy expected = Policy.UnmarshalCedar(expectedCedar);
        Assert.Equal(expected.MarshalJson(), actual.MarshalJson());
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

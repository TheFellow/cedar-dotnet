using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Experimental;
using Cedar.Core;
using Cedar.Core.Internal.Eval;
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
    public void IgnoreOnPermit_WithIgnoredPrincipalScope_DropsScope()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(
                principal == User::"alice",
                action,
                resource
            );
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv(principal: PartialEvaluation.Ignore()));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        AssertPolicyEquivalent("permit(principal, action, resource);", result.Policy!);
    }

    [Fact]
    public void IgnoreOnForbid_WithIgnoredPrincipalScope_DropsScope()
    {
        Policy policy = Policy.UnmarshalCedar("""
            forbid(
                principal == User::"alice",
                action,
                resource
            );
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv(principal: PartialEvaluation.Ignore()));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        AssertPolicyEquivalent("forbid(principal, action, resource);", result.Policy!);
    }

    [Fact]
    public void IgnoreOnPermit_InAndCondition_DropsIgnoredBranch()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { context.variable && context.ignore == 42 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("ignore", PartialEvaluation.Ignore()), ("variable", PartialEvaluation.Variable("variable")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        AssertPolicyEquivalent("permit(principal, action, resource);", result.Policy!);
    }

    [Fact]
    public void IgnoreOnPermit_InOrCondition_DropsIgnoredBranch()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { context.variable || context.ignore == 42 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("ignore", PartialEvaluation.Ignore()), ("variable", PartialEvaluation.Variable("variable")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        AssertPolicyEquivalent("permit(principal, action, resource);", result.Policy!);
    }

    [Fact]
    public void IgnoreOnPermit_InIfThenElseThenBranch_DropsCondition()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { if context.variable then context.ignore == 42 else true };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("ignore", PartialEvaluation.Ignore()), ("variable", PartialEvaluation.Variable("variable")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        AssertPolicyEquivalent("permit(principal, action, resource);", result.Policy!);
    }

    [Fact]
    public void IgnoreOnPermit_InIfThenElseElseBranch_DropsCondition()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { if context.variable then true else context.ignore == 42 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("ignore", PartialEvaluation.Ignore()), ("variable", PartialEvaluation.Variable("variable")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        AssertPolicyEquivalent("permit(principal, action, resource);", result.Policy!);
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
        Assert.Contains("incompatible types in comparison", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorConditionShortCircuit_PolicyKeptWithErrorNode()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { "oops" < 3 }
            when { context.variable == 42 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("variable", PartialEvaluation.Variable("variable")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("__cedar::partialError", result.Policy!.MarshalCedar(), StringComparison.Ordinal);
        Assert.DoesNotContain("context.variable == 42", result.Policy.MarshalCedar(), StringComparison.Ordinal);

        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(PartialEvaluation.ToNode(result.Policy), new EvalEnv()));
        Assert.Contains("incompatible types in comparison", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorConditionShortCircuit_PrecedingVariableConditionKept()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { context.variable == 42 }
            when { "oops" < 3 }
            when { context.variable == 99 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("variable", PartialEvaluation.Variable("variable")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("context.variable == 42", result.Policy!.MarshalCedar(), StringComparison.Ordinal);
        Assert.Contains("__cedar::partialError", result.Policy.MarshalCedar(), StringComparison.Ordinal);
        Assert.DoesNotContain("context.variable == 99", result.Policy.MarshalCedar(), StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorConditionShortCircuit_NonBooleanConditionBecomesPartialError()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { true }
            when { "test" }
            when { context.variable == 42 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("variable", PartialEvaluation.Variable("variable")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("__cedar::partialError", result.Policy!.MarshalCedar(), StringComparison.Ordinal);
        Assert.DoesNotContain("context.variable == 42", result.Policy.MarshalCedar(), StringComparison.Ordinal);

        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(PartialEvaluation.ToNode(result.Policy), new EvalEnv()));
        Assert.Contains("condition expected bool", exception.Message, StringComparison.Ordinal);
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
    public void TryGetPartialError_WhenArgEvalFails_ReturnsFalse()
    {
        INode badArg = new NodeAdd(
            new NodeValue(new CedarLong(42)),
            new NodeValue(CedarBool.False));
        INode node = new NodeExtensionCall(
            new CedarPath(PartialEvaluator.PartialErrorExtensionName),
            ImmutableArray.Create(badArg));

        bool ok = PartialEvaluator.TryGetPartialError(node, out string message);

        Assert.False(ok);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void IsPartialError_ReturnsTrueForPartialErrorNode()
    {
        Node node = PartialEvaluation.PartialError("boom");

        bool actual = PartialEvaluation.IsPartialError(node);

        Assert.True(actual);
    }

    [Fact]
    public void IsPartialError_ReturnsFalseForOrdinaryNode()
    {
        Policy policy = Policy.UnmarshalCedar("permit(principal, action, resource);");

        PartialNodeResult result = PartialEvaluation.EvaluateToNode(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.False(PartialEvaluation.IsPartialError(result.Node));
    }

    [Fact]
    public void EvaluateToNode_MismatchedScopes_ReturnsFalseNodeAndKeepFalse()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(
                principal == User::"alice",
                action == Action::"read",
                resource == Document::"doc1"
            );
            """);

        PartialNodeResult result = PartialEvaluation.EvaluateToNode(policy, new EvalEnv(principal: Bob, action: Read, resource: Doc1));

        Assert.False(result.Keep);
        Assert.NotNull(result.Node);

        ICedarData value = NodeEvaluation.Evaluate(result.Node, new EvalEnv(principal: Bob, action: Read, resource: Doc1));
        Assert.Equal(CedarBool.False, value);
    }

    [Fact]
    public void EvaluateToNode_ForbidPolicy_ReturnsResidualNodeAndKeepTrue()
    {
        Policy policy = Policy.UnmarshalCedar("forbid(principal, action, resource);");

        PartialNodeResult result = PartialEvaluation.EvaluateToNode(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Node);

        ICedarData value = NodeEvaluation.Evaluate(result.Node, new EvalEnv());
        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void GetTagWithNonEntityLiteral_BecomesPartialError()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { 42.getTag("key") == "value" };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("__cedar::partialError", result.Policy!.MarshalCedar(), StringComparison.Ordinal);

        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(PartialEvaluation.ToNode(result.Policy), new EvalEnv()));
        Assert.Contains("fold.GetTag", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HasTagWithNonEntityLiteral_BecomesPartialError()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { 42.hasTag("key") };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("__cedar::partialError", result.Policy!.MarshalCedar(), StringComparison.Ordinal);

        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(PartialEvaluation.ToNode(result.Policy), new EvalEnv()));
        Assert.Contains("fold.HasTag", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTagWithEntityLiteral_BecomesPartialError()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { User::"alice".getTag("key") == "value" };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("__cedar::partialError", result.Policy!.MarshalCedar(), StringComparison.Ordinal);

        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(PartialEvaluation.ToNode(result.Policy), new EvalEnv()));
        Assert.Contains("fold.GetTag", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HasTagWithEntityLiteral_BecomesPartialError()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { User::"alice".hasTag("key") };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("__cedar::partialError", result.Policy!.MarshalCedar(), StringComparison.Ordinal);

        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(PartialEvaluation.ToNode(result.Policy), new EvalEnv()));
        Assert.Contains("fold.HasTag", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AndWithFalseLeft_ShortCircuitsErrorRight_DropsPolicy()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { false && "oops" < 3 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.False(result.Keep);
        Assert.Null(result.Policy);
    }

    [Fact]
    public void AndWithTrueLeft_PropagatesErrorRight_AsPartialError()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { true && "oops" < 3 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("__cedar::partialError", result.Policy!.MarshalCedar(), StringComparison.Ordinal);

        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(PartialEvaluation.ToNode(result.Policy), new EvalEnv()));
        Assert.Contains("incompatible types in comparison", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OrWithTrueLeft_ShortCircuitsErrorRight_RemovesCondition()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { true || "oops" < 3 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        AssertPolicyEquivalent("permit(principal, action, resource);", result.Policy!);
    }

    [Fact]
    public void OrWithFalseLeft_PropagatesErrorRight_AsPartialError()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { false || "oops" < 3 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("__cedar::partialError", result.Policy!.MarshalCedar(), StringComparison.Ordinal);

        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(PartialEvaluation.ToNode(result.Policy), new EvalEnv()));
        Assert.Contains("incompatible types in comparison", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AndWithVariableLeft_AndErrorRight_PreservesResidualAndPartialError()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { context.variable && "oops" < 3 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("variable", PartialEvaluation.Variable("variable")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("context.variable", result.Policy!.MarshalCedar(), StringComparison.Ordinal);
        Assert.Contains("__cedar::partialError", result.Policy.MarshalCedar(), StringComparison.Ordinal);
    }

    [Fact]
    public void OrWithVariableLeft_AndErrorRight_PreservesResidualOrPartialError()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { context.variable || "oops" < 3 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("variable", PartialEvaluation.Variable("variable")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("context.variable", result.Policy!.MarshalCedar(), StringComparison.Ordinal);
        Assert.Contains("__cedar::partialError", result.Policy.MarshalCedar(), StringComparison.Ordinal);
    }

    [Fact]
    public void IfThenElseWithTrueCondition_DoesNotEvaluateElseError()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { if true then context.variable == 42 else "oops" < 3 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("variable", PartialEvaluation.Variable("variable")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        AssertPolicyEquivalent(
            """
            permit(principal, action, resource)
            when { context.variable == 42 };
            """,
            result.Policy!);
        Assert.DoesNotContain("__cedar::partialError", result.Policy.MarshalCedar(), StringComparison.Ordinal);
    }

    [Fact]
    public void IfThenElseWithFalseCondition_DoesNotEvaluateThenError()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { if false then "oops" < 3 else context.variable == 42 };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("variable", PartialEvaluation.Variable("variable")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        AssertPolicyEquivalent(
            """
            permit(principal, action, resource)
            when { context.variable == 42 };
            """,
            result.Policy!);
        Assert.DoesNotContain("__cedar::partialError", result.Policy.MarshalCedar(), StringComparison.Ordinal);
    }

    [Fact]
    public void IfThenElseWithVariableCondition_PreservesResidualCondition()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { if context.variable then true else false };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(
            policy,
            new EvalEnv(context: Record(("variable", PartialEvaluation.Variable("variable")))));

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains("if context.variable then true else false", result.Policy!.MarshalCedar(), StringComparison.Ordinal);
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

    [Fact]
    public void DatetimeComparisonLessThan_WithVariableContext_PreservesResidualCondition()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { datetime("1970-01-01T00:00:00.042Z") < context };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains(
            "datetime(\"1970-01-01T00:00:00.042Z\") < context",
            result.Policy!.MarshalCedar(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DatetimeComparisonLessThan_FoldsToTrue()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { datetime("1970-01-01T00:00:00.042Z") < datetime("1970-01-01T00:00:00.043Z") };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        AssertPolicyEquivalent("permit(principal, action, resource);", result.Policy!);
    }

    [Fact]
    public void DatetimeComparisonLessThanOrEqual_WithVariableContext_PreservesResidualCondition()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { datetime("1970-01-01T00:00:00.042Z") <= context };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains(
            "datetime(\"1970-01-01T00:00:00.042Z\") <= context",
            result.Policy!.MarshalCedar(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DatetimeComparisonLessThanOrEqual_FoldsToTrue()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { datetime("1970-01-01T00:00:00.042Z") <= datetime("1970-01-01T00:00:00.043Z") };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        AssertPolicyEquivalent("permit(principal, action, resource);", result.Policy!);
    }

    [Fact]
    public void DatetimeComparisonGreaterThan_WithVariableContext_PreservesResidualCondition()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { datetime("1970-01-01T00:00:00.042Z") > context };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains(
            "datetime(\"1970-01-01T00:00:00.042Z\") > context",
            result.Policy!.MarshalCedar(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DatetimeComparisonGreaterThan_FoldsToFalse()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { datetime("1970-01-01T00:00:00.042Z") > datetime("1970-01-01T00:00:00.043Z") };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.False(result.Keep);
        Assert.Null(result.Policy);
    }

    [Fact]
    public void DatetimeComparisonGreaterThanOrEqual_WithVariableContext_PreservesResidualCondition()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { datetime("1970-01-01T00:00:00.042Z") >= context };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.True(result.Keep);
        Assert.NotNull(result.Policy);
        Assert.Contains(
            "datetime(\"1970-01-01T00:00:00.042Z\") >= context",
            result.Policy!.MarshalCedar(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DatetimeComparisonGreaterThanOrEqual_FoldsToFalse()
    {
        Policy policy = Policy.UnmarshalCedar("""
            permit(principal, action, resource)
            when { datetime("1970-01-01T00:00:00.042Z") >= datetime("1970-01-01T00:00:00.043Z") };
            """);

        PartialPolicyResult result = PartialEvaluation.Evaluate(policy, new EvalEnv());

        Assert.False(result.Keep);
        Assert.Null(result.Policy);
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

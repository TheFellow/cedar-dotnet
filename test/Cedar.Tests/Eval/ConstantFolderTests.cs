using System;
using System.Collections.Immutable;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Core.Internal.Eval;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Eval;

public sealed class ConstantFolderTests
{
    private static readonly EntityUid Alice = new(new EntityType("User"), new CedarString("alice"));
    private static readonly EntityUid Bob = new(new EntityType("User"), new CedarString("bob"));
    private static readonly EntityUid ActionRead = new(new EntityType("Action"), new CedarString("read"));
    private static readonly EntityUid Doc1 = new(new EntityType("Document"), new CedarString("doc1"));
    private static readonly Position NoPos = new("", 0, 0, 0);

    [Fact]
    public void FoldPolicy_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ConstantFolder.FoldPolicy(null!));
    }

    [Fact]
    public void FoldPolicy_WithoutConditions_ReturnsOriginal()
    {
        PolicyAst policy = BuildPolicy();
        PolicyAst folded = ConstantFolder.FoldPolicy(policy);
        Assert.Same(policy, folded);
    }

    [Fact]
    public void Folds_SimpleArithmetic()
    {
        INode folded = FoldSingle(new NodeAdd(new NodeValue(new CedarLong(1)), new NodeValue(new CedarLong(2))));
        NodeValue value = Assert.IsType<NodeValue>(folded);
        Assert.Equal(new CedarLong(3), value.Value);
    }

    [Fact]
    public void Folds_NestedArithmetic_Recursively()
    {
        INode folded = FoldSingle(
            new NodeMult(
                new NodeAdd(new NodeValue(new CedarLong(1)), new NodeValue(new CedarLong(2))),
                new NodeValue(new CedarLong(3))));
        NodeValue value = Assert.IsType<NodeValue>(folded);
        Assert.Equal(new CedarLong(9), value.Value);
    }

    [Fact]
    public void Folds_Comparison()
    {
        INode folded = FoldSingle(new NodeLessThan(new NodeValue(new CedarLong(1)), new NodeValue(new CedarLong(2))));
        NodeValue value = Assert.IsType<NodeValue>(folded);
        Assert.Equal(CedarBool.True, value.Value);
    }

    [Fact]
    public void Folds_LogicalOperations()
    {
        INode folded = FoldSingle(
            new NodeAnd(
                new NodeValue(CedarBool.True),
                new NodeNot(new NodeValue(CedarBool.False))));
        NodeValue value = Assert.IsType<NodeValue>(folded);
        Assert.Equal(CedarBool.True, value.Value);
    }

    [Fact]
    public void Folds_Negation()
    {
        INode folded = FoldSingle(new NodeNegate(new NodeValue(new CedarLong(42))));
        NodeValue value = Assert.IsType<NodeValue>(folded);
        Assert.Equal(new CedarLong(-42), value.Value);
    }

    [Fact]
    public void Folds_SetLiteral_WithFoldedElements()
    {
        INode folded = FoldSingle(new NodeSet(ImmutableArray.Create<INode>(
            new NodeValue(new CedarLong(1)),
            new NodeAdd(new NodeValue(new CedarLong(1)), new NodeValue(new CedarLong(1))))));
        CedarSet set = Assert.IsType<CedarSet>(Assert.IsType<NodeValue>(folded).Value);
        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(new CedarLong(1)));
        Assert.True(set.Contains(new CedarLong(2)));
    }

    [Fact]
    public void Folds_RecordLiteral_WithFoldedElements()
    {
        INode folded = FoldSingle(new NodeRecord(ImmutableArray.Create(
            new NodeRecordElement(
                new CedarString("sum"),
                new NodeAdd(new NodeValue(new CedarLong(2)), new NodeValue(new CedarLong(3)))))));
        CedarRecord record = Assert.IsType<CedarRecord>(Assert.IsType<NodeValue>(folded).Value);
        Assert.True(record.TryGetValue(new CedarString("sum"), out ICedarData value));
        Assert.Equal(new CedarLong(5), value);
    }

    [Fact]
    public void Folds_ExtensionConstructors_WithLiteralArguments()
    {
        INode folded = FoldSingle(new NodeExtensionCall("decimal", ImmutableArray.Create<INode>(new NodeValue(new CedarString("3.14")))));
        CedarDecimal value = Assert.IsType<CedarDecimal>(Assert.IsType<NodeValue>(folded).Value);
        Assert.Equal(CedarDecimal.Parse("3.14"), value);
    }

    [Fact]
    public void DoesNotFold_ExtensionMethods()
    {
        INode folded = FoldSingle(new NodeExtensionCall("isIpv4", ImmutableArray.Create<INode>(
            new NodeExtensionCall("ip", ImmutableArray.Create<INode>(new NodeValue(new CedarString("1.2.3.4")))))));

        NodeExtensionCall methodCall = Assert.IsType<NodeExtensionCall>(folded);
        Assert.Equal("isIpv4", methodCall.Name);
        Assert.IsType<NodeValue>(methodCall.Args[0]);
    }

    [Fact]
    public void DoesNotFold_UnknownExtension()
    {
        INode folded = FoldSingle(new NodeExtensionCall("unknownExt", ImmutableArray.Create<INode>(new NodeValue(new CedarString("x")))));
        Assert.IsType<NodeExtensionCall>(folded);
    }

    [Fact]
    public void DoesNotFold_ParcDependentExpressions()
    {
        INode folded = FoldSingle(new NodeEquals(new NodeVariable(new CedarString("principal")), new NodeValue(Alice)));
        Assert.IsType<NodeEquals>(folded);
    }

    [Fact]
    public void DoesNotFold_EntityDependentNodeAccess()
    {
        INode folded = FoldSingle(new NodeAccess(new NodeValue(Alice), new CedarString("name")));
        Assert.IsType<NodeAccess>(folded);
    }

    [Fact]
    public void DoesNotFold_EntityDependentNodeIn()
    {
        INode folded = FoldSingle(new NodeIn(new NodeValue(Alice), new NodeValue(Alice)));
        Assert.IsType<NodeIn>(folded);
    }

    [Fact]
    public void DoesNotFold_EntityDependentNodeIs()
    {
        INode folded = FoldSingle(new NodeIs(new NodeValue(Alice), new EntityType("User")));
        Assert.IsType<NodeIs>(folded);
    }

    [Fact]
    public void DoesNotFold_EntityDependentNodeIsIn()
    {
        INode folded = FoldSingle(new NodeIsIn(new NodeValue(Alice), new EntityType("User"), new NodeValue(Alice)));
        Assert.IsType<NodeIsIn>(folded);
    }

    [Fact]
    public void DoesNotFold_EntityDependentNodeHas()
    {
        INode folded = FoldSingle(new NodeHas(new NodeValue(Alice), new CedarString("name")));
        Assert.IsType<NodeHas>(folded);
    }

    [Fact]
    public void DoesNotFold_EntityDependentNodeHasTag()
    {
        INode folded = FoldSingle(new NodeHasTag(new NodeValue(Alice), new NodeValue(new CedarString("env"))));
        Assert.IsType<NodeHasTag>(folded);
    }

    [Fact]
    public void DoesNotFold_EntityDependentNodeGetTag()
    {
        INode folded = FoldSingle(new NodeGetTag(new NodeValue(Alice), new NodeValue(new CedarString("env"))));
        Assert.IsType<NodeGetTag>(folded);
    }

    [Fact]
    public void PreservesEvaluationSemantics()
    {
        INode expression = new NodeAnd(
            new NodeEquals(new NodeVariable(new CedarString("principal")), new NodeValue(Alice)),
            new NodeEquals(
                new NodeAdd(new NodeValue(new CedarLong(1)), new NodeValue(new CedarLong(2))),
                new NodeValue(new CedarLong(3))));
        PolicyAst policy = BuildPolicy(expression);
        PolicyAst foldedPolicy = ConstantFolder.FoldPolicy(policy);

        IEvaluator unfolded = Compiler.ToEval(ScopeCompiler.CompilePolicy(policy));
        IEvaluator folded = Compiler.ToEval(ScopeCompiler.CompilePolicy(foldedPolicy));

        Assert.Equal(unfolded.Eval(MakeEnv(Alice)), folded.Eval(MakeEnv(Alice)));
        Assert.Equal(unfolded.Eval(MakeEnv(Bob)), folded.Eval(MakeEnv(Bob)));
    }

    [Fact]
    public void InvalidConstantExpression_RemainsUnfolded()
    {
        INode folded = FoldSingle(new NodeAdd(new NodeValue(new CedarLong(1)), new NodeValue(CedarBool.True)));
        Assert.IsType<NodeAdd>(folded);
    }

    [Fact]
    public void OverflowConstantExpression_RemainsUnfolded()
    {
        INode folded = FoldSingle(new NodeAdd(new NodeValue(new CedarLong(long.MaxValue)), new NodeValue(new CedarLong(1))));
        Assert.IsType<NodeAdd>(folded);
    }

    private static EvalEnv MakeEnv(EntityUid principal)
    {
        return new EvalEnv(new EntityMap(), principal, ActionRead, Doc1, new CedarRecord());
    }

    private static INode FoldSingle(INode condition)
    {
        PolicyAst policy = BuildPolicy(condition);
        PolicyAst folded = ConstantFolder.FoldPolicy(policy);
        return Assert.Single(folded.Conditions);
    }

    private static PolicyAst BuildPolicy(params INode[] conditions)
    {
        return new PolicyAst(
            Effect.Permit,
            new ScopeAll(),
            new ScopeAll(),
            new ScopeAll(),
            conditions.Length == 0 ? ImmutableArray<INode>.Empty : ImmutableArray.Create(conditions),
            ImmutableArray<Annotation>.Empty,
            NoPos);
    }
}

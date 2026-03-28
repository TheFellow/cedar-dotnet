using System.Collections.Immutable;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Core.Internal.Eval;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Eval;

public sealed class CompilerTests
{
    private static readonly EntityUid Alice = new(new EntityType("User"), new CedarString("alice"));
    private static readonly EntityUid ActionRead = new(new EntityType("Action"), new CedarString("read"));
    private static readonly EntityUid Doc1 = new(new EntityType("Document"), new CedarString("doc1"));
    private static readonly EntityUid Group = new(new EntityType("Group"), new CedarString("admins"));

    private static EvalEnv MakeEnv(params Entity[] entities)
    {
        return new EvalEnv(
            new EntityMap(entities),
            Alice,
            ActionRead,
            Doc1,
            new CedarRecord());
    }

    // --- ToEval node compilation ---

    [Fact]
    public void ToEval_NodeValue_ReturnsLiteral()
    {
        IEvaluator evaluator = Compiler.ToEval(new NodeValue(new CedarLong(42)));
        Assert.Equal(new CedarLong(42), evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ToEval_NodeVariable_ResolvesVariable()
    {
        IEvaluator evaluator = Compiler.ToEval(new NodeVariable(new CedarString("principal")));
        Assert.Equal(Alice, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ToEval_NodeAdd_ComputesSum()
    {
        INode node = new NodeAdd(new NodeValue(new CedarLong(3)), new NodeValue(new CedarLong(4)));
        IEvaluator evaluator = Compiler.ToEval(node);
        Assert.Equal(new CedarLong(7), evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ToEval_NodeAnd_CombinesConditions()
    {
        INode node = new NodeAnd(new NodeValue(CedarBool.True), new NodeValue(CedarBool.True));
        IEvaluator evaluator = Compiler.ToEval(node);
        ICedarData result = evaluator.Eval(MakeEnv());
        Assert.Equal(CedarBool.True, result);
    }

    [Fact]
    public void ToEval_NodeEquals_ComparesValues()
    {
        INode node = new NodeEquals(new NodeValue(new CedarLong(5)), new NodeValue(new CedarLong(5)));
        IEvaluator evaluator = Compiler.ToEval(node);
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ToEval_NodeNot_InvertsBoolean()
    {
        INode node = new NodeNot(new NodeValue(CedarBool.False));
        IEvaluator evaluator = Compiler.ToEval(node);
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ToEval_NodeExtensionCall_InvokesExtension()
    {
        INode node = new NodeExtensionCall(new CedarPath("decimal"), ImmutableArray.Create<INode>(new NodeValue(new CedarString("2.5"))));
        IEvaluator evaluator = Compiler.ToEval(node);
        ICedarData result = evaluator.Eval(MakeEnv());
        Assert.IsType<CedarDecimal>(result);
    }

    [Fact]
    public void ToEval_NodeSet_CreatesSet()
    {
        INode node = new NodeSet(ImmutableArray.Create<INode>(
            new NodeValue(new CedarLong(1)),
            new NodeValue(new CedarLong(2))));
        IEvaluator evaluator = Compiler.ToEval(node);
        CedarSet result = Assert.IsType<CedarSet>(evaluator.Eval(MakeEnv()));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ToEval_NodeRecord_CreatesRecord()
    {
        INode node = new NodeRecord(ImmutableArray.Create(
            new NodeRecordElement(new CedarString("key"), new NodeValue(new CedarLong(1)))));
        IEvaluator evaluator = Compiler.ToEval(node);
        CedarRecord result = Assert.IsType<CedarRecord>(evaluator.Eval(MakeEnv()));
        Assert.True(result.TryGetValue(new CedarString("key"), out _));
    }

    // --- ScopeCompiler ---

    [Fact]
    public void ScopeCompiler_ScopeAll_EvaluatesToTrue()
    {
        INode node = ScopeCompiler.Compile(new CedarString("principal"), new ScopeAll());
        IEvaluator evaluator = Compiler.ToEval(node);
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ScopeCompiler_ScopeEq_MatchingPrincipal_ReturnsTrue()
    {
        INode node = ScopeCompiler.Compile(new CedarString("principal"), new ScopeEq(Alice));
        IEvaluator evaluator = Compiler.ToEval(node);
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ScopeCompiler_ScopeEq_DifferentPrincipal_ReturnsFalse()
    {
        EntityUid bob = new(new EntityType("User"), new CedarString("bob"));
        INode node = ScopeCompiler.Compile(new CedarString("principal"), new ScopeEq(bob));
        IEvaluator evaluator = Compiler.ToEval(node);
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ScopeCompiler_ScopeIn_WithParent_ReturnsTrue()
    {
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        INode node = ScopeCompiler.Compile(new CedarString("principal"), new ScopeIn(Group));
        IEvaluator evaluator = Compiler.ToEval(node);
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv(aliceEntity)));
    }

    // --- Full policy compilation ---

    [Fact]
    public void Compile_FullPolicy_PermitAll_ReturnsTrue()
    {
        PolicyAst policy = new(
            Effect.Permit,
            new ScopeAll(),
            new ScopeAll(),
            new ScopeAll(),
            ImmutableArray<INode>.Empty,
            ImmutableArray<Annotation>.Empty,
            new Position("", 0, 0, 0));
        BoolEvaluator evaluator = Compiler.Compile(policy);
        Assert.True(evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void Compile_FullPolicy_WithCondition_ConditionFalse_ReturnsFalse()
    {
        PolicyAst policy = new(
            Effect.Permit,
            new ScopeAll(),
            new ScopeAll(),
            new ScopeAll(),
            ImmutableArray.Create<INode>(new NodeValue(CedarBool.False)),
            ImmutableArray<Annotation>.Empty,
            new Position("", 0, 0, 0));
        BoolEvaluator evaluator = Compiler.Compile(policy);
        Assert.False(evaluator.Eval(MakeEnv()));
    }
}

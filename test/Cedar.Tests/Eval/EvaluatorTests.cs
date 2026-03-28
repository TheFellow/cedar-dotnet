using System.Collections.Generic;
using Cedar.Core.Internal.Eval;
using Cedar.Core.Internal.Eval.Evaluators;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Eval;

public sealed class EvaluatorTests
{
    private static readonly EntityUid Alice = new(new EntityType("User"), new CedarString("alice"));
    private static readonly EntityUid Bob = new(new EntityType("User"), new CedarString("bob"));
    private static readonly EntityUid Action = new(new EntityType("Action"), new CedarString("read"));
    private static readonly EntityUid Resource = new(new EntityType("Document"), new CedarString("doc1"));
    private static readonly EntityUid Group = new(new EntityType("Group"), new CedarString("admins"));

    private static EvalEnv MakeEnv(params Entity[] entities)
    {
        return new EvalEnv(
            new EntityMap(entities),
            Alice,
            Action,
            Resource,
            new CedarRecord());
    }

    private static IEvaluator Lit(ICedarData value)
    {
        return new LiteralEvaluator(value);
    }

    private sealed class ThrowingEvaluator : IEvaluator
    {
        public ICedarData Eval(EvalEnv env)
        {
            throw new EvalException("should not be evaluated");
        }
    }

    private sealed class CountingEntityGetter : IEntityGetter
    {
        private readonly Dictionary<EntityUid, Entity> _entities;

        public CountingEntityGetter(params Entity[] entities)
        {
            _entities = new Dictionary<EntityUid, Entity>();
            foreach (Entity entity in entities)
            {
                _entities.Add(entity.Uid, entity);
            }
        }

        public int TryGetCount { get; private set; }

        public bool TryGet(EntityUid uid, out Entity entity)
        {
            TryGetCount++;
            return _entities.TryGetValue(uid, out entity!);
        }
    }

    // --- LiteralEvaluator ---

    [Fact]
    public void LiteralEvaluator_ReturnsValueUnchanged()
    {
        IEvaluator evaluator = Lit(new CedarLong(42));
        ICedarData result = evaluator.Eval(MakeEnv());
        Assert.Equal(new CedarLong(42), result);
    }

    [Fact]
    public void LiteralEvaluator_ReturnsBoolValue()
    {
        IEvaluator evaluator = Lit(CedarBool.True);
        ICedarData result = evaluator.Eval(MakeEnv());
        Assert.Equal(CedarBool.True, result);
    }

    // --- VariableEvaluator ---

    [Fact]
    public void VariableEvaluator_Principal_ReturnsPrincipal()
    {
        IEvaluator evaluator = new VariableEvaluator(new CedarString("principal"));
        ICedarData result = evaluator.Eval(MakeEnv());
        Assert.Equal(Alice, result);
    }

    [Fact]
    public void VariableEvaluator_Action_ReturnsAction()
    {
        IEvaluator evaluator = new VariableEvaluator(new CedarString("action"));
        ICedarData result = evaluator.Eval(MakeEnv());
        Assert.Equal(Action, result);
    }

    [Fact]
    public void VariableEvaluator_Resource_ReturnsResource()
    {
        IEvaluator evaluator = new VariableEvaluator(new CedarString("resource"));
        ICedarData result = evaluator.Eval(MakeEnv());
        Assert.Equal(Resource, result);
    }

    [Fact]
    public void VariableEvaluator_Context_ReturnsContext()
    {
        IEvaluator evaluator = new VariableEvaluator(new CedarString("context"));
        ICedarData result = evaluator.Eval(MakeEnv());
        Assert.IsType<CedarRecord>(result);
    }

    [Fact]
    public void VariableEvaluator_Unknown_ThrowsEvalException()
    {
        IEvaluator evaluator = new VariableEvaluator(new CedarString("unknown"));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- LogicalEvaluators ---

    [Fact]
    public void AndEvaluator_BothTrue_ReturnsTrue()
    {
        IEvaluator evaluator = new AndEvaluator(Lit(CedarBool.True), Lit(CedarBool.True));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void AndEvaluator_LeftFalse_ShortCircuits()
    {
        IEvaluator evaluator = new AndEvaluator(Lit(CedarBool.False), new ThrowingEvaluator());
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void AndEvaluator_LeftTrueRightFalse_ReturnsFalse()
    {
        IEvaluator evaluator = new AndEvaluator(Lit(CedarBool.True), Lit(CedarBool.False));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void OrEvaluator_BothFalse_ReturnsFalse()
    {
        IEvaluator evaluator = new OrEvaluator(Lit(CedarBool.False), Lit(CedarBool.False));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void OrEvaluator_LeftTrue_ShortCircuits()
    {
        IEvaluator evaluator = new OrEvaluator(Lit(CedarBool.True), new ThrowingEvaluator());
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void OrEvaluator_LeftFalseRightTrue_ReturnsTrue()
    {
        IEvaluator evaluator = new OrEvaluator(Lit(CedarBool.False), Lit(CedarBool.True));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void NotEvaluator_True_ReturnsFalse()
    {
        IEvaluator evaluator = new NotEvaluator(Lit(CedarBool.True));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void NotEvaluator_False_ReturnsTrue()
    {
        IEvaluator evaluator = new NotEvaluator(Lit(CedarBool.False));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    // --- ComparisonEvaluators ---

    [Fact]
    public void EqualEvaluator_SameValues_ReturnsTrue()
    {
        IEvaluator evaluator = new EqualEvaluator(Lit(new CedarLong(5)), Lit(new CedarLong(5)));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void EqualEvaluator_DifferentValues_ReturnsFalse()
    {
        IEvaluator evaluator = new EqualEvaluator(Lit(new CedarLong(5)), Lit(new CedarLong(10)));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void NotEqualEvaluator_DifferentValues_ReturnsTrue()
    {
        IEvaluator evaluator = new NotEqualEvaluator(Lit(new CedarLong(1)), Lit(new CedarLong(2)));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void NotEqualEvaluator_SameValues_ReturnsFalse()
    {
        IEvaluator evaluator = new NotEqualEvaluator(Lit(new CedarLong(1)), Lit(new CedarLong(1)));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void LessThanEvaluator_Less_ReturnsTrue()
    {
        IEvaluator evaluator = new LessThanEvaluator(Lit(new CedarLong(1)), Lit(new CedarLong(2)));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void LessThanEvaluator_Equal_ReturnsFalse()
    {
        IEvaluator evaluator = new LessThanEvaluator(Lit(new CedarLong(2)), Lit(new CedarLong(2)));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void LessThanOrEqualEvaluator_Equal_ReturnsTrue()
    {
        IEvaluator evaluator = new LessThanOrEqualEvaluator(Lit(new CedarLong(2)), Lit(new CedarLong(2)));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void LessThanEvaluator_IncompatibleTypes_ThrowsEvalException()
    {
        IEvaluator evaluator = new LessThanEvaluator(Lit(CedarBool.True), Lit(new CedarLong(2)));

        EvalException exception = Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));

        Assert.Equal(EvalErrors.IncompatibleComparison, exception.Message);
    }

    [Fact]
    public void GreaterThanEvaluator_Greater_ReturnsTrue()
    {
        IEvaluator evaluator = new GreaterThanEvaluator(Lit(new CedarLong(5)), Lit(new CedarLong(3)));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void GreaterThanOrEqualEvaluator_Equal_ReturnsTrue()
    {
        IEvaluator evaluator = new GreaterThanOrEqualEvaluator(Lit(new CedarLong(3)), Lit(new CedarLong(3)));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    // --- ArithmeticEvaluators ---

    [Fact]
    public void AddEvaluator_Normal_ReturnsSum()
    {
        IEvaluator evaluator = new AddEvaluator(Lit(new CedarLong(3)), Lit(new CedarLong(4)));
        Assert.Equal(new CedarLong(7), evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void AddEvaluator_Overflow_ThrowsEvalException()
    {
        IEvaluator evaluator = new AddEvaluator(Lit(new CedarLong(long.MaxValue)), Lit(new CedarLong(1)));
        EvalException ex = Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
        Assert.Contains("overflow", ex.Message);
    }

    [Fact]
    public void SubEvaluator_Normal_ReturnsDifference()
    {
        IEvaluator evaluator = new SubEvaluator(Lit(new CedarLong(10)), Lit(new CedarLong(3)));
        Assert.Equal(new CedarLong(7), evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void SubEvaluator_Overflow_ThrowsEvalException()
    {
        IEvaluator evaluator = new SubEvaluator(Lit(new CedarLong(long.MinValue)), Lit(new CedarLong(1)));
        EvalException ex = Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
        Assert.Contains("overflow", ex.Message);
    }

    [Fact]
    public void MultEvaluator_Normal_ReturnsProduct()
    {
        IEvaluator evaluator = new MultEvaluator(Lit(new CedarLong(6)), Lit(new CedarLong(7)));
        Assert.Equal(new CedarLong(42), evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void MultEvaluator_Overflow_ThrowsEvalException()
    {
        IEvaluator evaluator = new MultEvaluator(Lit(new CedarLong(long.MaxValue)), Lit(new CedarLong(2)));
        EvalException ex = Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
        Assert.Contains("overflow", ex.Message);
    }

    [Fact]
    public void NegateEvaluator_Normal_ReturnsNegated()
    {
        IEvaluator evaluator = new NegateEvaluator(Lit(new CedarLong(5)));
        Assert.Equal(new CedarLong(-5), evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void NegateEvaluator_Overflow_ThrowsEvalException()
    {
        IEvaluator evaluator = new NegateEvaluator(Lit(new CedarLong(long.MinValue)));
        EvalException ex = Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
        Assert.Contains("overflow", ex.Message);
    }

    // --- CollectionEvaluators ---

    [Fact]
    public void ContainsEvaluator_Present_ReturnsTrue()
    {
        CedarSet set = new(new CedarLong(1), new CedarLong(2), new CedarLong(3));
        IEvaluator evaluator = new ContainsEvaluator(Lit(set), Lit(new CedarLong(2)));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsEvaluator_Absent_ReturnsFalse()
    {
        CedarSet set = new(new CedarLong(1), new CedarLong(2));
        IEvaluator evaluator = new ContainsEvaluator(Lit(set), Lit(new CedarLong(5)));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsAllEvaluator_AllPresent_ReturnsTrue()
    {
        CedarSet haystack = new(new CedarLong(1), new CedarLong(2), new CedarLong(3));
        CedarSet needle = new(new CedarLong(1), new CedarLong(3));
        IEvaluator evaluator = new ContainsAllEvaluator(Lit(haystack), Lit(needle));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsAllEvaluator_SomeMissing_ReturnsFalse()
    {
        CedarSet haystack = new(new CedarLong(1), new CedarLong(2));
        CedarSet needle = new(new CedarLong(1), new CedarLong(5));
        IEvaluator evaluator = new ContainsAllEvaluator(Lit(haystack), Lit(needle));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsAnyEvaluator_OnePresent_ReturnsTrue()
    {
        CedarSet haystack = new(new CedarLong(1), new CedarLong(2));
        CedarSet needle = new(new CedarLong(5), new CedarLong(2));
        IEvaluator evaluator = new ContainsAnyEvaluator(Lit(haystack), Lit(needle));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsAnyEvaluator_NonePresent_ReturnsFalse()
    {
        CedarSet haystack = new(new CedarLong(1), new CedarLong(2));
        CedarSet needle = new(new CedarLong(5), new CedarLong(6));
        IEvaluator evaluator = new ContainsAnyEvaluator(Lit(haystack), Lit(needle));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void IsEmptyEvaluator_EmptySet_ReturnsTrue()
    {
        IEvaluator evaluator = new IsEmptyEvaluator(Lit(new CedarSet()));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void IsEmptyEvaluator_NonEmptySet_ReturnsFalse()
    {
        IEvaluator evaluator = new IsEmptyEvaluator(Lit(new CedarSet(new CedarLong(1))));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void SetLiteralEvaluator_CreatesSet()
    {
        IEvaluator evaluator = new SetLiteralEvaluator(new[] { Lit(new CedarLong(1)), Lit(new CedarLong(2)) });
        ICedarData result = evaluator.Eval(MakeEnv());
        CedarSet set = Assert.IsType<CedarSet>(result);
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void RecordLiteralEvaluator_CreatesRecord()
    {
        KeyValuePair<CedarString, IEvaluator>[] elements =
        {
            new(new CedarString("name"), Lit(new CedarString("alice")))
        };
        IEvaluator evaluator = new RecordLiteralEvaluator(elements);
        ICedarData result = evaluator.Eval(MakeEnv());
        CedarRecord record = Assert.IsType<CedarRecord>(result);
        Assert.True(record.TryGetValue(new CedarString("name"), out ICedarData? value));
        Assert.Equal(new CedarString("alice"), value);
    }

    // --- MembershipEvaluators ---

    [Fact]
    public void InEvaluator_DirectMatch_ReturnsTrue()
    {
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(Alice));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void InEvaluator_ParentMatch_ReturnsTrue()
    {
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(Group));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv(aliceEntity)));
    }

    [Fact]
    public void InEvaluator_NoMatch_ReturnsFalse()
    {
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(Group));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void InEvaluator_InSet_ReturnsTrue()
    {
        CedarSet set = new(Group, Bob);
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(set));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv(aliceEntity)));
    }

    [Fact]
    public void InEvaluator_InSet_ReusesCacheAcrossEvaluations()
    {
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        Entity groupEntity = new(Group, new EntityUidSet(), new CedarRecord(), new CedarRecord());
        CountingEntityGetter entities = new(aliceEntity, groupEntity);
        EvalEnv env = new(entities, Alice, Action, Resource, new CedarRecord());
        CedarSet set = new(Bob, Group);
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(set));

        Assert.Equal(CedarBool.True, evaluator.Eval(env));
        Assert.Equal(2, env.InCache.Count);
        Assert.True(env.InCache.TryGetValue((Alice, Bob), out bool missingParentResult));
        Assert.False(missingParentResult);
        Assert.True(env.InCache.TryGetValue((Alice, Group), out bool matchingParentResult));
        Assert.True(matchingParentResult);

        int tryGetCountAfterFirstEvaluation = entities.TryGetCount;
        Assert.True(tryGetCountAfterFirstEvaluation > 0);

        Assert.Equal(CedarBool.True, evaluator.Eval(env));
        Assert.Equal(tryGetCountAfterFirstEvaluation, entities.TryGetCount);
        Assert.Equal(2, env.InCache.Count);
    }

    [Fact]
    public void InEvaluator_DeepHierarchy_ReusesSingularCache()
    {
        EntityUid team = new(new EntityType("Group"), new CedarString("team"));
        EntityUid org = new(new EntityType("Group"), new CedarString("org"));
        EntityUid root = new(new EntityType("Group"), new CedarString("root"));

        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        Entity groupEntity = new(Group, new EntityUidSet(new[] { team }), new CedarRecord(), new CedarRecord());
        Entity teamEntity = new(team, new EntityUidSet(new[] { org }), new CedarRecord(), new CedarRecord());
        Entity orgEntity = new(org, new EntityUidSet(new[] { root }), new CedarRecord(), new CedarRecord());
        EvalEnv env = MakeEnv(aliceEntity, groupEntity, teamEntity, orgEntity);
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(root));

        Assert.Equal(CedarBool.True, evaluator.Eval(env));
        Assert.Single(env.InCache);
        Assert.True(env.InCache.TryGetValue((Alice, root), out bool cached));
        Assert.True(cached);

        Assert.Equal(CedarBool.True, evaluator.Eval(env));
        Assert.Single(env.InCache);
    }

    [Fact]
    public void InEvaluator_SingularCache_IsolatedPerEnvironment()
    {
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(Group));
        EvalEnv firstEnv = MakeEnv(aliceEntity);
        EvalEnv secondEnv = MakeEnv(aliceEntity);

        Assert.Empty(firstEnv.InCache);
        Assert.Empty(secondEnv.InCache);

        Assert.Equal(CedarBool.True, evaluator.Eval(firstEnv));
        Assert.Single(firstEnv.InCache);
        Assert.Empty(secondEnv.InCache);
    }

    [Fact]
    public void IsEvaluator_MatchingType_ReturnsTrue()
    {
        IEvaluator evaluator = new IsEvaluator(Lit(Alice), new CedarPath("User"));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void IsEvaluator_DifferentType_ReturnsFalse()
    {
        IEvaluator evaluator = new IsEvaluator(Lit(Alice), new CedarPath("Admin"));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void IsInEvaluator_TypeMatchAndIn_ReturnsTrue()
    {
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        IEvaluator evaluator = new IsInEvaluator(Lit(Alice), new CedarPath("User"), Lit(Group));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv(aliceEntity)));
    }

    [Fact]
    public void IsInEvaluator_TypeMismatch_ReturnsFalse()
    {
        IEvaluator evaluator = new IsInEvaluator(Lit(Alice), new CedarPath("Admin"), Lit(Group));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    // --- AccessEvaluators ---

    [Fact]
    public void AttributeAccessEvaluator_RecordHasAttribute_ReturnsValue()
    {
        CedarRecord record = new(new Dictionary<CedarString, ICedarData>
        {
            { new CedarString("name"), new CedarString("alice") }
        });
        IEvaluator evaluator = new AttributeAccessEvaluator(Lit(record), Lit(new CedarString("name")));
        Assert.Equal(new CedarString("alice"), evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void AttributeAccessEvaluator_RecordMissingAttribute_ThrowsEvalException()
    {
        CedarRecord record = new();
        IEvaluator evaluator = new AttributeAccessEvaluator(Lit(record), Lit(new CedarString("missing")));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void AttributeAccessEvaluator_EntityHasAttribute_ReturnsValue()
    {
        Entity entity = new(Alice,
            new EntityUidSet(),
            new CedarRecord(new Dictionary<CedarString, ICedarData>
            {
                { new CedarString("role"), new CedarString("admin") }
            }),
            new CedarRecord());
        IEvaluator evaluator = new AttributeAccessEvaluator(Lit(Alice), Lit(new CedarString("role")));
        Assert.Equal(new CedarString("admin"), evaluator.Eval(MakeEnv(entity)));
    }

    [Fact]
    public void AttributeAccessEvaluator_EntityMissing_ThrowsEvalException()
    {
        IEvaluator evaluator = new AttributeAccessEvaluator(Lit(Alice), Lit(new CedarString("role")));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void HasEvaluator_RecordHasAttribute_ReturnsTrue()
    {
        CedarRecord record = new(new Dictionary<CedarString, ICedarData>
        {
            { new CedarString("name"), new CedarString("alice") }
        });
        IEvaluator evaluator = new HasEvaluator(Lit(record), new CedarString("name"));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void HasEvaluator_RecordMissingAttribute_ReturnsFalse()
    {
        CedarRecord record = new();
        IEvaluator evaluator = new HasEvaluator(Lit(record), new CedarString("missing"));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void HasEvaluator_EntityHasAttribute_ReturnsTrue()
    {
        Entity entity = new(Alice,
            new EntityUidSet(),
            new CedarRecord(new Dictionary<CedarString, ICedarData>
            {
                { new CedarString("role"), new CedarString("admin") }
            }),
            new CedarRecord());
        IEvaluator evaluator = new HasEvaluator(Lit(Alice), new CedarString("role"));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv(entity)));
    }

    [Fact]
    public void HasEvaluator_EntityMissingAttribute_ReturnsFalse()
    {
        IEvaluator evaluator = new HasEvaluator(Lit(Alice), new CedarString("role"));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    // --- TagEvaluators ---

    [Fact]
    public void GetTagEvaluator_TagFound_ReturnsValue()
    {
        Entity entity = new(Alice,
            new EntityUidSet(),
            new CedarRecord(),
            new CedarRecord(new Dictionary<CedarString, ICedarData>
            {
                { new CedarString("env"), new CedarString("prod") }
            }));
        IEvaluator evaluator = new GetTagEvaluator(Lit(Alice), Lit(new CedarString("env")));
        Assert.Equal(new CedarString("prod"), evaluator.Eval(MakeEnv(entity)));
    }

    [Fact]
    public void GetTagEvaluator_TagMissing_ThrowsEvalException()
    {
        Entity entity = new(Alice, new EntityUidSet(), new CedarRecord(), new CedarRecord());
        IEvaluator evaluator = new GetTagEvaluator(Lit(Alice), Lit(new CedarString("missing")));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv(entity)));
    }

    [Fact]
    public void GetTagEvaluator_EntityMissing_ThrowsEvalException()
    {
        IEvaluator evaluator = new GetTagEvaluator(Lit(Alice), Lit(new CedarString("tag")));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void HasTagEvaluator_TagFound_ReturnsTrue()
    {
        Entity entity = new(Alice,
            new EntityUidSet(),
            new CedarRecord(),
            new CedarRecord(new Dictionary<CedarString, ICedarData>
            {
                { new CedarString("env"), new CedarString("prod") }
            }));
        IEvaluator evaluator = new HasTagEvaluator(Lit(Alice), Lit(new CedarString("env")));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv(entity)));
    }

    [Fact]
    public void HasTagEvaluator_TagMissing_ReturnsFalse()
    {
        Entity entity = new(Alice, new EntityUidSet(), new CedarRecord(), new CedarRecord());
        IEvaluator evaluator = new HasTagEvaluator(Lit(Alice), Lit(new CedarString("missing")));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv(entity)));
    }

    [Fact]
    public void HasTagEvaluator_EntityMissing_ReturnsFalse()
    {
        IEvaluator evaluator = new HasTagEvaluator(Lit(Alice), Lit(new CedarString("tag")));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    // --- PatternEvaluators ---

    [Fact]
    public void LikeEvaluator_Matches_ReturnsTrue()
    {
        CedarPattern pattern = CedarPattern.Parse("hello*");
        IEvaluator evaluator = new LikeEvaluator(Lit(new CedarString("hello world")), pattern);
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void LikeEvaluator_NoMatch_ReturnsFalse()
    {
        CedarPattern pattern = CedarPattern.Parse("goodbye*");
        IEvaluator evaluator = new LikeEvaluator(Lit(new CedarString("hello world")), pattern);
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void LikeEvaluator_ExactMatch_ReturnsTrue()
    {
        CedarPattern pattern = CedarPattern.Parse("exact");
        IEvaluator evaluator = new LikeEvaluator(Lit(new CedarString("exact")), pattern);
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    // --- ConditionalEvaluator ---

    [Fact]
    public void ConditionalEvaluator_TrueBranch_ReturnsThenValue()
    {
        IEvaluator evaluator = new ConditionalEvaluator(Lit(CedarBool.True), Lit(new CedarLong(1)), Lit(new CedarLong(2)));
        Assert.Equal(new CedarLong(1), evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ConditionalEvaluator_FalseBranch_ReturnsElseValue()
    {
        IEvaluator evaluator = new ConditionalEvaluator(Lit(CedarBool.False), Lit(new CedarLong(1)), Lit(new CedarLong(2)));
        Assert.Equal(new CedarLong(2), evaluator.Eval(MakeEnv()));
    }

    // --- ExtensionEvaluator ---

    [Fact]
    public void ExtensionEvaluator_DispatchesToRegistry()
    {
        IEvaluator evaluator = new ExtensionEvaluator("decimal", new[] { Lit(new CedarString("1.5")) });
        ICedarData result = evaluator.Eval(MakeEnv());
        Assert.IsType<CedarDecimal>(result);
    }

    [Fact]
    public void ExtensionEvaluator_UnknownFunction_ThrowsEvalException()
    {
        IEvaluator evaluator = new ExtensionEvaluator("nonexistent", System.Array.Empty<IEvaluator>());
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }
}

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
    private static readonly EntityUid UnspecifiedPrincipal = new(new EntityType("__cedar::empty"), new CedarString("principal"));

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

        Assert.Equal("type error: expected comparable value, got bool", exception.Message);
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
    public void IsEmptyEvaluator_NonSetOperand_ThrowsTypeError()
    {
        IEvaluator evaluator = new IsEmptyEvaluator(Lit(CedarBool.True));
        EvalException exception = Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
        Assert.Equal("expected set, got bool", exception.Message);
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
    public void InEvaluator_InSet_ReevaluatesHierarchyWithoutCache()
    {
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        Entity groupEntity = new(Group, new EntityUidSet(), new CedarRecord(), new CedarRecord());
        CountingEntityGetter entities = new(aliceEntity, groupEntity);
        EvalEnv env = new(entities, Alice, Action, Resource, new CedarRecord());
        CedarSet set = new(Bob, Group);
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(set));

        Assert.Equal(CedarBool.True, evaluator.Eval(env));
        int tryGetCountAfterFirstEvaluation = entities.TryGetCount;
        Assert.True(tryGetCountAfterFirstEvaluation > 0);

        Assert.Equal(CedarBool.True, evaluator.Eval(env));
        Assert.True(entities.TryGetCount > tryGetCountAfterFirstEvaluation);
    }

    [Fact]
    public void InEvaluator_DeepHierarchy_ReevaluatesWithoutCache()
    {
        EntityUid team = new(new EntityType("Group"), new CedarString("team"));
        EntityUid org = new(new EntityType("Group"), new CedarString("org"));
        EntityUid root = new(new EntityType("Group"), new CedarString("root"));

        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        Entity groupEntity = new(Group, new EntityUidSet(new[] { team }), new CedarRecord(), new CedarRecord());
        Entity teamEntity = new(team, new EntityUidSet(new[] { org }), new CedarRecord(), new CedarRecord());
        Entity orgEntity = new(org, new EntityUidSet(new[] { root }), new CedarRecord(), new CedarRecord());
        CountingEntityGetter entities = new(aliceEntity, groupEntity, teamEntity, orgEntity);
        EvalEnv env = new(entities, Alice, Action, Resource, new CedarRecord());
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(root));

        Assert.Equal(CedarBool.True, evaluator.Eval(env));
        int tryGetCountAfterFirstEvaluation = entities.TryGetCount;
        Assert.True(tryGetCountAfterFirstEvaluation > 0);

        Assert.Equal(CedarBool.True, evaluator.Eval(env));
        Assert.True(entities.TryGetCount > tryGetCountAfterFirstEvaluation);
    }

    [Fact]
    public void InEvaluator_Evaluations_AreIsolatedPerEnvironment()
    {
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(Group));
        CountingEntityGetter firstEntities = new(aliceEntity);
        CountingEntityGetter secondEntities = new(aliceEntity);
        EvalEnv firstEnv = new(firstEntities, Alice, Action, Resource, new CedarRecord());
        EvalEnv secondEnv = new(secondEntities, Alice, Action, Resource, new CedarRecord());

        Assert.Equal(0, firstEntities.TryGetCount);
        Assert.Equal(0, secondEntities.TryGetCount);

        Assert.Equal(CedarBool.True, evaluator.Eval(firstEnv));
        Assert.True(firstEntities.TryGetCount > 0);
        Assert.Equal(0, secondEntities.TryGetCount);

        Assert.Equal(CedarBool.True, evaluator.Eval(secondEnv));
        Assert.True(secondEntities.TryGetCount > 0);
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
    public void GetTagEvaluator_UnspecifiedEntity_DoesNotEvaluateTagAndThrowsEvalException()
    {
        IEvaluator evaluator = new GetTagEvaluator(Lit(UnspecifiedPrincipal), new ThrowingEvaluator());

        EvalException exception = Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
        Assert.Contains(EvalErrors.UnspecifiedEntity, exception.Message);
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

    [Fact]
    public void HasTagEvaluator_UnspecifiedEntity_ReturnsFalse()
    {
        IEvaluator evaluator = new HasTagEvaluator(Lit(UnspecifiedPrincipal), Lit(new CedarString("tag")));
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

    // --- Or truth table (from Go TestOrNode) ---

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void OrEvaluator_TruthTable(bool lhs, bool rhs, bool expected)
    {
        IEvaluator evaluator = new OrEvaluator(Lit(new CedarBool(lhs)), Lit(new CedarBool(rhs)));
        Assert.Equal(new CedarBool(expected), evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void OrEvaluator_LeftNonBool_ThrowsEvalException()
    {
        IEvaluator evaluator = new OrEvaluator(Lit(new CedarLong(1)), Lit(CedarBool.True));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void OrEvaluator_RightNonBool_ThrowsEvalException()
    {
        IEvaluator evaluator = new OrEvaluator(Lit(CedarBool.False), Lit(new CedarLong(1)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- And truth table (from Go TestAndNode) ---

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void AndEvaluator_TruthTable(bool lhs, bool rhs, bool expected)
    {
        IEvaluator evaluator = new AndEvaluator(Lit(new CedarBool(lhs)), Lit(new CedarBool(rhs)));
        Assert.Equal(new CedarBool(expected), evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void AndEvaluator_LeftNonBool_ThrowsEvalException()
    {
        IEvaluator evaluator = new AndEvaluator(Lit(new CedarLong(1)), Lit(CedarBool.True));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void AndEvaluator_RightNonBool_ThrowsEvalException()
    {
        IEvaluator evaluator = new AndEvaluator(Lit(CedarBool.True), Lit(new CedarLong(1)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- Not error cases (from Go TestNotNode) ---

    [Fact]
    public void NotEvaluator_NonBool_ThrowsEvalException()
    {
        IEvaluator evaluator = new NotEvaluator(Lit(new CedarLong(1)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- Arithmetic type error cases (from Go TestAddNode/TestSubtractNode/TestMultiplyNode/TestNegateNode) ---

    [Fact]
    public void AddEvaluator_LeftNonLong_ThrowsEvalException()
    {
        IEvaluator evaluator = new AddEvaluator(Lit(CedarBool.True), Lit(new CedarLong(0)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void AddEvaluator_RightNonLong_ThrowsEvalException()
    {
        IEvaluator evaluator = new AddEvaluator(Lit(new CedarLong(0)), Lit(CedarBool.True));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void AddEvaluator_NegativeOverflow_ThrowsEvalException()
    {
        IEvaluator evaluator = new AddEvaluator(Lit(new CedarLong(long.MinValue)), Lit(new CedarLong(-1)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void SubEvaluator_LeftNonLong_ThrowsEvalException()
    {
        IEvaluator evaluator = new SubEvaluator(Lit(CedarBool.True), Lit(new CedarLong(0)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void SubEvaluator_RightNonLong_ThrowsEvalException()
    {
        IEvaluator evaluator = new SubEvaluator(Lit(new CedarLong(0)), Lit(CedarBool.True));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void SubEvaluator_PositiveOverflow_ThrowsEvalException()
    {
        IEvaluator evaluator = new SubEvaluator(Lit(new CedarLong(long.MaxValue)), Lit(new CedarLong(-1)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void MultEvaluator_LeftNonLong_ThrowsEvalException()
    {
        IEvaluator evaluator = new MultEvaluator(Lit(CedarBool.True), Lit(new CedarLong(0)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void MultEvaluator_RightNonLong_ThrowsEvalException()
    {
        IEvaluator evaluator = new MultEvaluator(Lit(new CedarLong(0)), Lit(CedarBool.True));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void MultEvaluator_NegativeOverflow_ThrowsEvalException()
    {
        IEvaluator evaluator = new MultEvaluator(Lit(new CedarLong(long.MinValue)), Lit(new CedarLong(2)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void NegateEvaluator_NonLong_ThrowsEvalException()
    {
        IEvaluator evaluator = new NegateEvaluator(Lit(CedarBool.True));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- Conditional error cases (from Go TestIfThenElseNode) ---

    [Fact]
    public void ConditionalEvaluator_NonBoolCondition_ThrowsEvalException()
    {
        IEvaluator evaluator = new ConditionalEvaluator(Lit(new CedarLong(123)), Lit(new CedarLong(1)), Lit(new CedarLong(2)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- Contains error cases (from Go TestContainsNode) ---

    [Fact]
    public void ContainsEvaluator_NonSetOperand_ThrowsTypeError()
    {
        IEvaluator evaluator = new ContainsEvaluator(Lit(CedarBool.True), Lit(new CedarLong(0)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- ContainsAll error cases (from Go TestContainsAllNode) ---

    [Fact]
    public void ContainsAllEvaluator_LeftNonSet_ThrowsTypeError()
    {
        IEvaluator evaluator = new ContainsAllEvaluator(Lit(CedarBool.True), Lit(new CedarSet()));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsAllEvaluator_RightNonSet_ThrowsTypeError()
    {
        IEvaluator evaluator = new ContainsAllEvaluator(Lit(new CedarSet()), Lit(new CedarLong(0)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- ContainsAny error cases (from Go TestContainsAnyNode) ---

    [Fact]
    public void ContainsAnyEvaluator_LeftNonSet_ThrowsTypeError()
    {
        IEvaluator evaluator = new ContainsAnyEvaluator(Lit(CedarBool.True), Lit(new CedarSet()));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsAnyEvaluator_RightNonSet_ThrowsTypeError()
    {
        IEvaluator evaluator = new ContainsAnyEvaluator(Lit(new CedarSet()), Lit(new CedarLong(0)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- Detailed ContainsAll cases (from Go TestContainsAllNode) ---

    [Fact]
    public void ContainsAllEvaluator_EmptyContainsAllEmpty_ReturnsTrue()
    {
        IEvaluator evaluator = new ContainsAllEvaluator(Lit(new CedarSet()), Lit(new CedarSet()));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsAllEvaluator_NonEmptyContainsAllEmpty_ReturnsTrue()
    {
        CedarSet haystack = new(CedarBool.True, new CedarLong(1));
        IEvaluator evaluator = new ContainsAllEvaluator(Lit(haystack), Lit(new CedarSet()));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsAllEvaluator_SubsetTrue_ReturnsTrue()
    {
        CedarSet haystack = new(CedarBool.True, new CedarLong(1));
        CedarSet needle = new(CedarBool.True);
        IEvaluator evaluator = new ContainsAllEvaluator(Lit(haystack), Lit(needle));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsAllEvaluator_SupersetNeedle_ReturnsFalse()
    {
        CedarSet haystack = new(CedarBool.True);
        CedarSet needle = new(CedarBool.True, new CedarLong(1));
        IEvaluator evaluator = new ContainsAllEvaluator(Lit(haystack), Lit(needle));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    // --- Detailed ContainsAny cases (from Go TestContainsAnyNode) ---

    [Fact]
    public void ContainsAnyEvaluator_BothEmpty_ReturnsFalse()
    {
        IEvaluator evaluator = new ContainsAnyEvaluator(Lit(new CedarSet()), Lit(new CedarSet()));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsAnyEvaluator_DisjointSets_ReturnsFalse()
    {
        CedarSet lhs = new(new CedarLong(1), new CedarLong(2));
        CedarSet rhs = new(new CedarLong(3), new CedarLong(4));
        IEvaluator evaluator = new ContainsAnyEvaluator(Lit(lhs), Lit(rhs));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsAnyEvaluator_OverlappingSets_ReturnsTrue()
    {
        CedarSet lhs = new(new CedarLong(1), new CedarLong(2));
        CedarSet rhs = new(new CedarLong(2), new CedarLong(3));
        IEvaluator evaluator = new ContainsAnyEvaluator(Lit(lhs), Lit(rhs));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    // --- Contains detailed cases (from Go TestContainsNode) ---

    [Fact]
    public void ContainsEvaluator_EmptySet_ReturnsFalse()
    {
        IEvaluator evaluator = new ContainsEvaluator(Lit(new CedarSet()), Lit(CedarBool.True));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsEvaluator_NestedSetContainsInnerSet_ReturnsTrue()
    {
        CedarSet inner = new(CedarBool.True, new CedarLong(1));
        CedarSet outer = new(inner, CedarBool.False, new CedarLong(2));
        IEvaluator evaluator = new ContainsEvaluator(Lit(outer), Lit(inner));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ContainsEvaluator_NestedSetDoesNotContainElement_ReturnsFalse()
    {
        CedarSet inner = new(CedarBool.True, new CedarLong(1));
        CedarSet outer = new(inner, CedarBool.False, new CedarLong(2));
        // outer contains inner as a nested set, but not True as a top-level element
        IEvaluator evaluator = new ContainsEvaluator(Lit(outer), Lit(CedarBool.True));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    // --- SetLiteral additional cases (from Go TestSetLiteralNode) ---

    [Fact]
    public void SetLiteralEvaluator_EmptySet_ReturnsEmptySet()
    {
        IEvaluator evaluator = new SetLiteralEvaluator(System.Array.Empty<IEvaluator>());
        CedarSet set = Assert.IsType<CedarSet>(evaluator.Eval(MakeEnv()));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void SetLiteralEvaluator_NestedSets_PreservesStructure()
    {
        CedarSet innerSet = new(CedarBool.False, new CedarLong(1));
        IEvaluator evaluator = new SetLiteralEvaluator(new[]
        {
            Lit(CedarBool.True),
            Lit(innerSet),
            Lit(new CedarLong(10))
        });
        CedarSet result = Assert.IsType<CedarSet>(evaluator.Eval(MakeEnv()));
        Assert.Equal(3, result.Count);
    }

    // --- RecordLiteral additional cases (from Go TestRecordLiteralNode) ---

    [Fact]
    public void RecordLiteralEvaluator_EmptyRecord_ReturnsEmptyRecord()
    {
        IEvaluator evaluator = new RecordLiteralEvaluator(System.Array.Empty<KeyValuePair<CedarString, IEvaluator>>());
        CedarRecord record = Assert.IsType<CedarRecord>(evaluator.Eval(MakeEnv()));
        Assert.Equal(0, record.Count);
    }

    // --- Like pattern matching (from Go TestLikeNode) ---

    [Fact]
    public void LikeEvaluator_NonString_ThrowsEvalException()
    {
        CedarPattern pattern = CedarPattern.Parse("*");
        IEvaluator evaluator = new LikeEvaluator(Lit(CedarBool.True), pattern);
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Theory]
    [InlineData("eggs", "ham*", false)]
    [InlineData("eggs", "*ham", false)]
    [InlineData("eggs", "*ham*", false)]
    [InlineData("ham and eggs", "ham*", true)]
    [InlineData("ham and eggs", "*ham", false)]
    [InlineData("ham and eggs", "*ham*", true)]
    [InlineData("ham and eggs", "*h*a*m*", true)]
    [InlineData("eggs and ham", "ham*", false)]
    [InlineData("eggs and ham", "*ham", true)]
    [InlineData("eggs, ham, and spinach", "ham*", false)]
    [InlineData("eggs, ham, and spinach", "*ham", false)]
    [InlineData("eggs, ham, and spinach", "*ham*", true)]
    [InlineData("Gotham", "ham*", false)]
    [InlineData("Gotham", "*ham", true)]
    [InlineData("ham", "ham", true)]
    [InlineData("ham", "ham*", true)]
    [InlineData("ham", "*ham", true)]
    [InlineData("ham", "*h*a*m*", true)]
    [InlineData("ham and ham", "ham*", true)]
    [InlineData("ham and ham", "*ham", true)]
    [InlineData("ham", "*ham and eggs*", false)]
    public void LikeEvaluator_PatternMatchCases(string input, string patternStr, bool expected)
    {
        CedarPattern pattern = CedarPattern.Parse(patternStr);
        IEvaluator evaluator = new LikeEvaluator(Lit(new CedarString(input)), pattern);
        Assert.Equal(new CedarBool(expected), evaluator.Eval(MakeEnv()));
    }

    // --- Comparison type error cases (from Go TestComparableValueComparisonNodes) ---

    [Fact]
    public void LessThanEvaluator_CrossTypeDatetimeVsLong_ThrowsEvalException()
    {
        CedarDatetime dt = CedarDatetime.Parse("1970-01-01T00:00:00Z");
        IEvaluator evaluator = new LessThanEvaluator(Lit(dt), Lit(new CedarLong(-1)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void LessThanEvaluator_CrossTypeDurationVsDatetime_ThrowsEvalException()
    {
        CedarDuration dur = CedarDuration.Parse("1ms");
        CedarDatetime dt = CedarDatetime.Parse("1970-01-01T00:00:00Z");
        IEvaluator evaluator = new LessThanEvaluator(Lit(dur), Lit(dt));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void LessThanEvaluator_CrossTypeLongVsDuration_ThrowsEvalException()
    {
        CedarDuration dur = CedarDuration.Parse("1ms");
        IEvaluator evaluator = new LessThanEvaluator(Lit(new CedarLong(-1)), Lit(dur));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void GreaterThanEvaluator_Less_ReturnsFalse()
    {
        IEvaluator evaluator = new GreaterThanEvaluator(Lit(new CedarLong(1)), Lit(new CedarLong(5)));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void GreaterThanOrEqualEvaluator_Less_ReturnsFalse()
    {
        IEvaluator evaluator = new GreaterThanOrEqualEvaluator(Lit(new CedarLong(1)), Lit(new CedarLong(5)));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void LessThanOrEqualEvaluator_Greater_ReturnsFalse()
    {
        IEvaluator evaluator = new LessThanOrEqualEvaluator(Lit(new CedarLong(5)), Lit(new CedarLong(1)));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    // --- Datetime/Duration comparisons (from Go TestComparableValueComparisonNodes) ---

    [Fact]
    public void LessThanEvaluator_DatetimeLess_ReturnsTrue()
    {
        CedarDatetime past = CedarDatetime.Parse("1970-01-01T00:00:00Z");
        CedarDatetime future = CedarDatetime.Parse("1970-01-02T00:00:00Z");
        IEvaluator evaluator = new LessThanEvaluator(Lit(past), Lit(future));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void LessThanEvaluator_DatetimeEqual_ReturnsFalse()
    {
        CedarDatetime dt = CedarDatetime.Parse("1970-01-01T00:00:00Z");
        IEvaluator evaluator = new LessThanEvaluator(Lit(dt), Lit(dt));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void GreaterThanEvaluator_DatetimeGreater_ReturnsTrue()
    {
        CedarDatetime past = CedarDatetime.Parse("1970-01-01T00:00:00Z");
        CedarDatetime future = CedarDatetime.Parse("1970-01-02T00:00:00Z");
        IEvaluator evaluator = new GreaterThanEvaluator(Lit(future), Lit(past));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void LessThanEvaluator_DurationLess_ReturnsTrue()
    {
        CedarDuration small = CedarDuration.Parse("1ms");
        CedarDuration large = CedarDuration.Parse("1h");
        IEvaluator evaluator = new LessThanEvaluator(Lit(small), Lit(large));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void GreaterThanEvaluator_DurationGreater_ReturnsTrue()
    {
        CedarDuration small = CedarDuration.Parse("1ms");
        CedarDuration large = CedarDuration.Parse("1h");
        IEvaluator evaluator = new GreaterThanEvaluator(Lit(large), Lit(small));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    // --- Decimal comparisons via ComparableValues (from Go TestDecimalLessThanNode etc.) ---

    [Theory]
    [InlineData("-1.0", "-1.0", false)]
    [InlineData("-1.0", "0.0", true)]
    [InlineData("-1.0", "1.0", true)]
    [InlineData("0.0", "-1.0", false)]
    [InlineData("0.0", "0.0", false)]
    [InlineData("0.0", "1.0", true)]
    [InlineData("1.0", "-1.0", false)]
    [InlineData("1.0", "0.0", false)]
    [InlineData("1.0", "1.0", false)]
    public void LessThanEvaluator_DecimalComparisons(string lhsStr, string rhsStr, bool expected)
    {
        CedarDecimal lhs = CedarDecimal.Parse(lhsStr);
        CedarDecimal rhs = CedarDecimal.Parse(rhsStr);
        IEvaluator evaluator = new LessThanEvaluator(Lit(lhs), Lit(rhs));
        Assert.Equal(new CedarBool(expected), evaluator.Eval(MakeEnv()));
    }

    [Theory]
    [InlineData("-1.0", "-1.0", true)]
    [InlineData("-1.0", "0.0", true)]
    [InlineData("0.0", "-1.0", false)]
    [InlineData("0.0", "0.0", true)]
    [InlineData("1.0", "1.0", true)]
    public void LessThanOrEqualEvaluator_DecimalComparisons(string lhsStr, string rhsStr, bool expected)
    {
        CedarDecimal lhs = CedarDecimal.Parse(lhsStr);
        CedarDecimal rhs = CedarDecimal.Parse(rhsStr);
        IEvaluator evaluator = new LessThanOrEqualEvaluator(Lit(lhs), Lit(rhs));
        Assert.Equal(new CedarBool(expected), evaluator.Eval(MakeEnv()));
    }

    [Theory]
    [InlineData("-1.0", "-1.0", false)]
    [InlineData("0.0", "-1.0", true)]
    [InlineData("1.0", "-1.0", true)]
    [InlineData("0.0", "0.0", false)]
    [InlineData("1.0", "1.0", false)]
    public void GreaterThanEvaluator_DecimalComparisons(string lhsStr, string rhsStr, bool expected)
    {
        CedarDecimal lhs = CedarDecimal.Parse(lhsStr);
        CedarDecimal rhs = CedarDecimal.Parse(rhsStr);
        IEvaluator evaluator = new GreaterThanEvaluator(Lit(lhs), Lit(rhs));
        Assert.Equal(new CedarBool(expected), evaluator.Eval(MakeEnv()));
    }

    [Theory]
    [InlineData("-1.0", "-1.0", true)]
    [InlineData("0.0", "-1.0", true)]
    [InlineData("-1.0", "0.0", false)]
    [InlineData("0.0", "0.0", true)]
    [InlineData("1.0", "1.0", true)]
    public void GreaterThanOrEqualEvaluator_DecimalComparisons(string lhsStr, string rhsStr, bool expected)
    {
        CedarDecimal lhs = CedarDecimal.Parse(lhsStr);
        CedarDecimal rhs = CedarDecimal.Parse(rhsStr);
        IEvaluator evaluator = new GreaterThanOrEqualEvaluator(Lit(lhs), Lit(rhs));
        Assert.Equal(new CedarBool(expected), evaluator.Eval(MakeEnv()));
    }

    // --- Equal cross-type (from Go TestEqualNode typesNotEqual) ---

    [Fact]
    public void EqualEvaluator_DifferentTypes_ReturnsFalse()
    {
        IEvaluator evaluator = new EqualEvaluator(Lit(new CedarLong(1)), Lit(CedarBool.True));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    // --- In error cases (from Go TestInNode) ---

    [Fact]
    public void InEvaluator_LeftNonEntity_ThrowsEvalException()
    {
        IEvaluator evaluator = new InEvaluator(Lit(new CedarString("foo")), Lit(new CedarSet()));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void InEvaluator_RightNonEntityNonSet_ThrowsEvalException()
    {
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(new CedarString("foo")));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void InEvaluator_RightSetContainingNonEntity_ThrowsEvalException()
    {
        CedarSet bad = new(new CedarString("foo"));
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(bad));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void InEvaluator_Reflexive_ReturnsTrue()
    {
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(Alice));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void InEvaluator_TransitiveHierarchy_ReturnsTrue()
    {
        EntityUid species = new(new EntityType("species"), new CedarString("human"));
        EntityUid kingdom = new(new EntityType("kingdom"), new CedarString("animal"));
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { species }), new CedarRecord(), new CedarRecord());
        Entity speciesEntity = new(species, new EntityUidSet(new[] { kingdom }), new CedarRecord(), new CedarRecord());
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(kingdom));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv(aliceEntity, speciesEntity)));
    }

    [Fact]
    public void InEvaluator_TransitiveHierarchy_NoPath_ReturnsFalse()
    {
        EntityUid species = new(new EntityType("species"), new CedarString("human"));
        EntityUid kingdom = new(new EntityType("kingdom"), new CedarString("plant"));
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { species }), new CedarRecord(), new CedarRecord());
        Entity speciesEntity = new(species, new EntityUidSet(new[] { new EntityUid(new EntityType("kingdom"), new CedarString("animal")) }), new CedarRecord(), new CedarRecord());
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(kingdom));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv(aliceEntity, speciesEntity)));
    }

    // --- Is error cases (from Go TestIsNode) ---

    [Fact]
    public void IsEvaluator_NonEntity_ThrowsEvalException()
    {
        IEvaluator evaluator = new IsEvaluator(Lit(new CedarLong(42)), new CedarPath("User"));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- IsIn error cases (from Go TestIsInNode) ---

    [Fact]
    public void IsInEvaluator_LeftNonEntity_ThrowsEvalException()
    {
        IEvaluator evaluator = new IsInEvaluator(Lit(new CedarString("foo")), new CedarPath("User"), Lit(new CedarSet()));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void IsInEvaluator_WrongType_ReturnsFalse()
    {
        IEvaluator evaluator = new IsInEvaluator(Lit(Alice), new CedarPath("Admin"), Lit(Alice));
        // Alice is type "User", not "Admin", so isIn should return false
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    // --- HasEvaluator error cases (from Go TestHasNode) ---

    [Fact]
    public void HasEvaluator_NonRecordNonEntity_ThrowsEvalException()
    {
        IEvaluator evaluator = new HasEvaluator(Lit(CedarBool.True), new CedarString("foo"));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- AttributeAccess error cases (from Go TestAttributeAccessNode) ---

    [Fact]
    public void AttributeAccessEvaluator_NonRecordNonEntity_ThrowsEvalException()
    {
        IEvaluator evaluator = new AttributeAccessEvaluator(Lit(CedarBool.True), Lit(new CedarString("foo")));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- GetTag error cases (from Go TestGetTagNode) ---

    [Fact]
    public void GetTagEvaluator_ObjectTypeError_ThrowsEvalException()
    {
        IEvaluator evaluator = new GetTagEvaluator(Lit(CedarBool.True), Lit(new CedarString("tag")));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void GetTagEvaluator_SubjectTypeError_ThrowsEvalException()
    {
        IEvaluator evaluator = new GetTagEvaluator(Lit(Alice), Lit(new CedarLong(42)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void GetTagEvaluator_RecordNotAllowed_ThrowsEvalException()
    {
        CedarRecord rec = new();
        IEvaluator evaluator = new GetTagEvaluator(Lit(rec), Lit(new CedarString("tag")));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- HasTag error cases (from Go TestHasTagNode) ---

    [Fact]
    public void HasTagEvaluator_ObjectTypeError_ThrowsEvalException()
    {
        IEvaluator evaluator = new HasTagEvaluator(Lit(CedarBool.True), Lit(new CedarString("tag")));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void HasTagEvaluator_SubjectTypeError_ThrowsEvalException()
    {
        IEvaluator evaluator = new HasTagEvaluator(Lit(Alice), Lit(new CedarLong(42)));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void HasTagEvaluator_RecordNotAllowed_ThrowsEvalException()
    {
        CedarRecord rec = new();
        IEvaluator evaluator = new HasTagEvaluator(Lit(rec), Lit(new CedarString("tag")));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void HasTagEvaluator_UnknownEntity_ReturnsFalse()
    {
        EntityUid unknown = new(new EntityType("Unknown"), new CedarString("nope"));
        IEvaluator evaluator = new HasTagEvaluator(Lit(unknown), Lit(new CedarString("tag")));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    // --- Entity hierarchy cycle detection (from Go TestEntityIn/loopFalse) ---

    [Fact]
    public void InEvaluator_CyclicHierarchy_ReturnsFalse()
    {
        // Build a cycle: level1::a -> level2::a,b -> level3::a,b -> level1::a,b (back to start)
        EntityUid l1a = new(new EntityType("level1"), new CedarString("a"));
        EntityUid l1b = new(new EntityType("level1"), new CedarString("b"));
        EntityUid l2a = new(new EntityType("level2"), new CedarString("a"));
        EntityUid l2b = new(new EntityType("level2"), new CedarString("b"));
        EntityUid l3a = new(new EntityType("level3"), new CedarString("a"));
        EntityUid l3b = new(new EntityType("level3"), new CedarString("b"));
        EntityUid l3z = new(new EntityType("level3"), new CedarString("z"));

        Entity e1a = new(l1a, new EntityUidSet(new[] { l2a, l2b }), new CedarRecord(), new CedarRecord());
        Entity e1b = new(l1b, new EntityUidSet(new[] { l2a, l2b }), new CedarRecord(), new CedarRecord());
        Entity e2a = new(l2a, new EntityUidSet(new[] { l3a, l3b }), new CedarRecord(), new CedarRecord());
        Entity e2b = new(l2b, new EntityUidSet(new[] { l3a, l3b }), new CedarRecord(), new CedarRecord());
        Entity e3a = new(l3a, new EntityUidSet(new[] { l1a, l1b }), new CedarRecord(), new CedarRecord());
        Entity e3b = new(l3b, new EntityUidSet(new[] { l1a, l1b }), new CedarRecord(), new CedarRecord());

        IEvaluator evaluator = new InEvaluator(Lit(l1a), Lit(l3z));
        EvalEnv env = new(new EntityMap(new[] { e1a, e1b, e2a, e2b, e3a, e3b }), Alice, Action, Resource, new CedarRecord());
        Assert.Equal(CedarBool.False, evaluator.Eval(env));
    }

    // --- Entity hierarchy exponential caching (from Go TestEntityIn/exponentialWithoutCaching) ---

    [Fact]
    public void InEvaluator_DeepBinaryTreeHierarchy_CompletesQuickly()
    {
        // Build a binary tree of depth 100: each node at level i has two parents at level i+1.
        // Without caching, this would be O(2^100) — must complete quickly.
        Entity[] entities = new Entity[200];
        for (int i = 0; i < 100; i++)
        {
            EntityUid parent1 = new(new EntityType($"{i + 1}"), new CedarString("1"));
            EntityUid parent2 = new(new EntityType($"{i + 1}"), new CedarString("2"));
            EntityUidSet parents = new(new[] { parent1, parent2 });

            EntityUid uid1 = new(new EntityType($"{i}"), new CedarString("1"));
            EntityUid uid2 = new(new EntityType($"{i}"), new CedarString("2"));
            entities[i * 2] = new Entity(uid1, parents, new CedarRecord(), new CedarRecord());
            entities[i * 2 + 1] = new Entity(uid2, parents, new CedarRecord(), new CedarRecord());
        }

        EntityUid start = new(new EntityType("0"), new CedarString("1"));
        EntityUid target = new(new EntityType("0"), new CedarString("3")); // does not exist
        IEvaluator evaluator = new InEvaluator(Lit(start), Lit(target));
        EvalEnv env = new(new EntityMap(entities), Alice, Action, Resource, new CedarRecord());

        // This must complete quickly; if no caching, it would take O(2^100)
        Assert.Equal(CedarBool.False, evaluator.Eval(env));
    }

    // --- ContainsAny not quadratic (from Go TestContainsAnyNode/not quadratic) ---

    [Fact]
    public void ContainsAnyEvaluator_LargeDisjointSets_CompletesQuickly()
    {
        // Build two large disjoint sets of 200k items each
        // With quadratic containsAny this would take minutes; with hash-based it completes instantly
        int setSize = 200000;
        ICedarData[] set1Items = new ICedarData[setSize];
        ICedarData[] set2Items = new ICedarData[setSize];

        for (int i = 0; i < setSize; i++)
        {
            set1Items[i] = new CedarLong(i);
            set2Items[i] = new CedarLong(setSize + i);
        }

        CedarSet set1 = new(set1Items);
        CedarSet set2 = new(set2Items);
        IEvaluator evaluator = new ContainsAnyEvaluator(Lit(set1), Lit(set2));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv()));
    }

    // --- IsIn RhsTypeError2 (set containing non-entity) from Go TestIsInNode ---

    [Fact]
    public void IsInEvaluator_RightSetContainingNonEntity_ThrowsEvalException()
    {
        CedarSet bad = new(new CedarString("foo"));
        IEvaluator evaluator = new IsInEvaluator(Lit(Alice), new CedarPath("User"), Lit(bad));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- IsIn RhsTypeError1 (non-entity non-set RHS) from Go TestIsInNode ---

    [Fact]
    public void IsInEvaluator_RightNonEntityNonSet_ThrowsEvalException()
    {
        IEvaluator evaluator = new IsInEvaluator(Lit(Alice), new CedarPath("User"), Lit(new CedarString("foo")));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }

    // --- IsIn reflexive with entity match (from Go TestIsInNode/Reflexive1) ---

    [Fact]
    public void IsInEvaluator_Reflexive_TypeMatch_ReturnsTrue()
    {
        IEvaluator evaluator = new IsInEvaluator(Lit(Alice), new CedarPath("User"), Lit(Alice));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    // --- IsIn reflexive with set (from Go TestIsInNode/Reflexive2) ---

    [Fact]
    public void IsInEvaluator_Reflexive_InSet_ReturnsTrue()
    {
        CedarSet set = new(Alice);
        IEvaluator evaluator = new IsInEvaluator(Lit(Alice), new CedarPath("User"), Lit(set));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    // --- IsIn BasicTrue through hierarchy (from Go TestIsInNode/BasicTrue) ---

    [Fact]
    public void IsInEvaluator_TransitiveHierarchy_TypeMatch_ReturnsTrue()
    {
        EntityUid species = new(new EntityType("species"), new CedarString("human"));
        EntityUid kingdom = new(new EntityType("kingdom"), new CedarString("animal"));
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { species }), new CedarRecord(), new CedarRecord());
        Entity speciesEntity = new(species, new EntityUidSet(new[] { kingdom }), new CedarRecord(), new CedarRecord());
        IEvaluator evaluator = new IsInEvaluator(Lit(Alice), new CedarPath("User"), Lit(kingdom));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv(aliceEntity, speciesEntity)));
    }

    // --- IsIn BasicFalse (type matches but not in hierarchy) ---

    [Fact]
    public void IsInEvaluator_TransitiveHierarchy_NoPath_ReturnsFalse()
    {
        EntityUid species = new(new EntityType("species"), new CedarString("human"));
        EntityUid kingdom = new(new EntityType("kingdom"), new CedarString("plant"));
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { species }), new CedarRecord(), new CedarRecord());
        Entity speciesEntity = new(species, new EntityUidSet(new[] { new EntityUid(new EntityType("kingdom"), new CedarString("animal")) }), new CedarRecord(), new CedarRecord());
        IEvaluator evaluator = new IsInEvaluator(Lit(Alice), new CedarPath("User"), Lit(kingdom));
        Assert.Equal(CedarBool.False, evaluator.Eval(MakeEnv(aliceEntity, speciesEntity)));
    }

    // --- In multi-level hierarchy (from Go TestEntityIn/twoLevelTrue) ---

    [Fact]
    public void InEvaluator_TwoLevelHierarchy_ReturnsTrue()
    {
        EntityUid chess = new(new EntityType("club"), new CedarString("chess"));
        EntityUid game = new(new EntityType("category"), new CedarString("game"));
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { chess }), new CedarRecord(), new CedarRecord());
        Entity chessEntity = new(chess, new EntityUidSet(new[] { game }), new CedarRecord(), new CedarRecord());
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(game));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv(aliceEntity, chessEntity)));
    }

    // --- In one-level with multiple RHS entities (from Go TestEntityIn/oneLevelTrue) ---

    [Fact]
    public void InEvaluator_OneLevel_InSetOfEntities_ReturnsTrue()
    {
        EntityUid rowing = new(new EntityType("club"), new CedarString("rowing"));
        EntityUid running = new(new EntityType("club"), new CedarString("running"));
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { new EntityUid(new EntityType("club"), new CedarString("dancing")), rowing }), new CedarRecord(), new CedarRecord());
        CedarSet set = new(running, rowing);
        IEvaluator evaluator = new InEvaluator(Lit(Alice), Lit(set));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv(aliceEntity)));
    }

    // --- Conditional short-circuit: true branch does not eval else (from Go TestIfThenElseNode) ---

    [Fact]
    public void ConditionalEvaluator_TrueBranch_DoesNotEvalElse()
    {
        IEvaluator evaluator = new ConditionalEvaluator(Lit(CedarBool.True), Lit(new CedarLong(42)), new ThrowingEvaluator());
        Assert.Equal(new CedarLong(42), evaluator.Eval(MakeEnv()));
    }

    [Fact]
    public void ConditionalEvaluator_FalseBranch_DoesNotEvalThen()
    {
        IEvaluator evaluator = new ConditionalEvaluator(Lit(CedarBool.False), new ThrowingEvaluator(), Lit(new CedarLong(42)));
        Assert.Equal(new CedarLong(42), evaluator.Eval(MakeEnv()));
    }

    // --- GetTag with programmatic tag key (from Go TestGetTagNode/ProgrammaticTag) ---

    [Fact]
    public void GetTagEvaluator_ProgrammaticTagKey_ReturnsValue()
    {
        EntityUid knownEntity = new(new EntityType("knownType"), new CedarString("knownID"));
        Entity entity = new(knownEntity, new EntityUidSet(),
            new CedarRecord(new Dictionary<CedarString, ICedarData> { { new CedarString("knownAttr"), new CedarString("knownTag") } }),
            new CedarRecord(new Dictionary<CedarString, ICedarData> { { new CedarString("knownTag"), new CedarLong(42) } }));
        // Use attribute access as the tag key evaluator: entity.knownAttr -> "knownTag"
        IEvaluator tagKeyEvaluator = new AttributeAccessEvaluator(Lit(knownEntity), Lit(new CedarString("knownAttr")));
        IEvaluator evaluator = new GetTagEvaluator(Lit(knownEntity), tagKeyEvaluator);
        Assert.Equal(new CedarLong(42), evaluator.Eval(MakeEnv(entity)));
    }

    // --- HasTag with programmatic tag key (from Go TestHasTagNode/ProgrammaticTag) ---

    [Fact]
    public void HasTagEvaluator_ProgrammaticTagKey_ReturnsTrue()
    {
        EntityUid knownEntity = new(new EntityType("knownType"), new CedarString("knownID"));
        Entity entity = new(knownEntity, new EntityUidSet(),
            new CedarRecord(new Dictionary<CedarString, ICedarData> { { new CedarString("knownAttr"), new CedarString("knownTag") } }),
            new CedarRecord(new Dictionary<CedarString, ICedarData> { { new CedarString("knownTag"), new CedarLong(42) } }));
        IEvaluator tagKeyEvaluator = new AttributeAccessEvaluator(Lit(knownEntity), Lit(new CedarString("knownAttr")));
        IEvaluator evaluator = new HasTagEvaluator(Lit(knownEntity), tagKeyEvaluator);
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv(entity)));
    }

    // --- Like backslash patterns (from Go TestLikeNode cases 22-25) ---

    [Theory]
    [InlineData("\\afterslash", "\\\\*", true)]
    [InlineData("string\\with\\backslashes", "string\\\\with\\\\backslashes", true)]
    [InlineData("string\\with\\backslashes", "string*with*backslashes", true)]
    [InlineData("string*with*stars", "string\\*with\\*stars", true)]
    public void LikeEvaluator_BackslashPatterns(string input, string patternStr, bool expected)
    {
        CedarPattern pattern = CedarPattern.Parse(patternStr);
        IEvaluator evaluator = new LikeEvaluator(Lit(new CedarString(input)), pattern);
        Assert.Equal(new CedarBool(expected), evaluator.Eval(MakeEnv()));
    }

    // --- Equal cross-type entity (from Go TestEqualNode/typesNotEqual) ---

    [Fact]
    public void NotEqualEvaluator_DifferentTypes_ReturnsTrue()
    {
        IEvaluator evaluator = new NotEqualEvaluator(Lit(new CedarLong(1)), Lit(CedarBool.True));
        Assert.Equal(CedarBool.True, evaluator.Eval(MakeEnv()));
    }

    // --- AttributeAccess on unspecified entity (from Go TestAttributeAccessNode/UnspecifiedEntity) ---

    [Fact]
    public void AttributeAccessEvaluator_UnspecifiedEntity_ThrowsEvalException()
    {
        EntityUid unspecified = new(new EntityType(""), new CedarString(""));
        IEvaluator evaluator = new AttributeAccessEvaluator(Lit(unspecified), Lit(new CedarString("attr")));
        Assert.Throws<EvalException>(() => evaluator.Eval(MakeEnv()));
    }
}

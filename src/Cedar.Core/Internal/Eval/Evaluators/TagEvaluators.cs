using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class GetTagEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    private const string EmptyEntityTypeName = "__cedar::empty";

    public ICedarData Eval(EvalEnv env)
    {
        EntityUid entityUid = TypeConversion.ValueToEntity(left.Eval(env));

        if (IsUnspecifiedEntity(entityUid))
        {
            throw new EvalException($"cannot access tag of {EvalErrors.UnspecifiedEntity}");
        }

        string tag = TypeConversion.ValueToString(right.Eval(env));

        if (!env.Entities.TryGet(entityUid, out Entity entity))
        {
            throw new EvalException($"entity `{entityUid}` {EvalErrors.MissingEntity}");
        }

        if (entity.Tags.TryGetValue(new CedarString(tag), out ICedarData value))
        {
            return value;
        }

        throw new EvalException($"`{entityUid}` {EvalErrors.MissingTag} `{tag}`");
    }

    private static bool IsUnspecifiedEntity(EntityUid entityUid)
    {
        return entityUid.Type.Value == EmptyEntityTypeName;
    }
}

internal sealed class HasTagEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        EntityUid entityUid = TypeConversion.ValueToEntity(left.Eval(env));
        string tag = TypeConversion.ValueToString(right.Eval(env));
        return env.Entities.TryGet(entityUid, out Entity entity) && entity.Tags.TryGetValue(new CedarString(tag), out _) ? CedarBool.True : CedarBool.False;
    }
}

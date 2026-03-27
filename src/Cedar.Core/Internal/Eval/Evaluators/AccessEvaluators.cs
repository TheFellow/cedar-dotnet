using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class AttributeAccessEvaluator(IEvaluator value, IEvaluator attributeExpr) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        ICedarData source = value.Eval(env);
        ICedarData attrValue = attributeExpr.Eval(env);

        if (attrValue is not CedarString attribute)
        {
            throw new EvalException($"attribute key must be a string, got {EvalErrors.TypeName(attrValue)}");
        }

        return source switch
        {
            CedarRecord record => RecordAccess(record, attribute),
            EntityUid entityUid => EntityAccess(env.Entities, entityUid, attribute),
            _ => throw new EvalException($"expected record or entity, got {EvalErrors.TypeName(source)}")
        };
    }

    private static ICedarData RecordAccess(CedarRecord record, CedarString attribute)
    {
        if (record.TryGetValue(attribute, out ICedarData value))
        {
            return value;
        }

        throw new EvalException($"record {EvalErrors.MissingAttribute} `{attribute.Value}`");
    }

    private static ICedarData EntityAccess(IEntityGetter entities, EntityUid entityUid, CedarString attribute)
    {
        if (!entities.TryGet(entityUid, out Entity entity))
        {
            throw new EvalException($"entity `{entityUid}` {EvalErrors.MissingEntity}");
        }

        if (entity.Attributes.TryGetValue(attribute, out ICedarData value))
        {
            return value;
        }

        throw new EvalException($"`{entityUid}` {EvalErrors.MissingAttribute} `{attribute.Value}`");
    }
}

internal sealed class HasEvaluator(IEvaluator value, CedarString attribute) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        ICedarData source = value.Eval(env);

        return source switch
        {
            CedarRecord record => new CedarBool(record.TryGetValue(attribute, out _)),
            EntityUid entityUid => new CedarBool(env.Entities.TryGet(entityUid, out Entity entity) && entity.Attributes.TryGetValue(attribute, out _)),
            _ => throw new EvalException($"expected record or entity, got {EvalErrors.TypeName(source)}")
        };
    }
}

using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class InEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        EntityUid leftValue = TypeConversion.ValueToEntity(left.Eval(env));
        ICedarData rightValue = right.Eval(env);
        return InOperator.Contains(env, leftValue, rightValue) ? CedarBool.True : CedarBool.False;
    }
}

internal sealed class IsEvaluator(IEvaluator left, CedarPath entityType) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        EntityUid leftValue = TypeConversion.ValueToEntity(left.Eval(env));
        return string.Equals(leftValue.Type.Value, entityType.Value, System.StringComparison.Ordinal) ? CedarBool.True : CedarBool.False;
    }
}

internal sealed class IsInEvaluator(IEvaluator left, CedarPath entityType, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        EntityUid leftValue = TypeConversion.ValueToEntity(left.Eval(env));
        if (!string.Equals(leftValue.Type.Value, entityType.Value, System.StringComparison.Ordinal))
        {
            return CedarBool.False;
        }

        ICedarData rightValue = right.Eval(env);
        return InOperator.Contains(env, leftValue, rightValue) ? CedarBool.True : CedarBool.False;
    }
}

internal static class InOperator
{
    public static bool Contains(EvalEnv env, EntityUid entity, ICedarData query)
    {
        return query switch
        {
            EntityUid parent => EntityInOne(env.Entities, entity, parent),
            CedarSet set => EntityInSet(env.Entities, entity, set),
            _ => throw new EvalException($"expected set or entity, got {EvalErrors.TypeName(query)}")
        };
    }

    private static bool EntityInOne(IEntityGetter entities, EntityUid entity, EntityUid parent)
    {
        return EntityInEntity(entities, entity, parent);
    }

    private static bool EntityInSet(IEntityGetter entities, EntityUid entity, CedarSet set)
    {
        foreach (ICedarData candidate in set)
        {
            EntityUid parent = TypeConversion.ValueToEntity(candidate);
            if (EntityInOne(entities, entity, parent))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EntityInEntity(IEntityGetter entities, EntityUid entity, EntityUid parent)
    {
        if (entity.Equals(parent))
        {
            return true;
        }

        HashSet<EntityUid> seen = [];
        Stack<EntityUid> pending = new();
        pending.Push(entity);

        while (pending.Count > 0)
        {
            EntityUid current = pending.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            if (!entities.TryGet(current, out Entity found))
            {
                continue;
            }

            foreach (EntityUid currentParent in found.Parents)
            {
                if (currentParent.Equals(parent))
                {
                    return true;
                }

                if (!currentParent.Equals(entity))
                {
                    pending.Push(currentParent);
                }
            }
        }

        return false;
    }
}

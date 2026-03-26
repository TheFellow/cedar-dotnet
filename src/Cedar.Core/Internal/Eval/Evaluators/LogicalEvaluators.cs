using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class AndEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        ICedarData leftValue = left.Eval(env);
        if (!TypeConversion.ValueToBool(leftValue))
        {
            return leftValue;
        }

        ICedarData rightValue = right.Eval(env);
        _ = TypeConversion.ValueToBool(rightValue);
        return rightValue;
    }
}

internal sealed class OrEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        ICedarData leftValue = left.Eval(env);
        if (TypeConversion.ValueToBool(leftValue))
        {
            return leftValue;
        }

        ICedarData rightValue = right.Eval(env);
        _ = TypeConversion.ValueToBool(rightValue);
        return rightValue;
    }
}

internal sealed class NotEvaluator(IEvaluator inner) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        return new CedarBool(!TypeConversion.ValueToBool(inner.Eval(env)));
    }
}

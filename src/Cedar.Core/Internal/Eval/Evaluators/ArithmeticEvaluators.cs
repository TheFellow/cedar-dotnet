using System;
using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class AddEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        long leftValue = TypeConversion.ValueToLong(left.Eval(env));
        long rightValue = TypeConversion.ValueToLong(right.Eval(env));

        try
        {
            return new CedarLong(checked(leftValue + rightValue));
        }
        catch (OverflowException)
        {
            throw new EvalException($"{EvalErrors.Overflow} while attempting to add `{leftValue}` with `{rightValue}`");
        }
    }
}

internal sealed class SubEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        long leftValue = TypeConversion.ValueToLong(left.Eval(env));
        long rightValue = TypeConversion.ValueToLong(right.Eval(env));

        try
        {
            return new CedarLong(checked(leftValue - rightValue));
        }
        catch (OverflowException)
        {
            throw new EvalException($"{EvalErrors.Overflow} while attempting to subtract `{rightValue}` from `{leftValue}`");
        }
    }
}

internal sealed class MultEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        long leftValue = TypeConversion.ValueToLong(left.Eval(env));
        long rightValue = TypeConversion.ValueToLong(right.Eval(env));

        try
        {
            return new CedarLong(checked(leftValue * rightValue));
        }
        catch (OverflowException)
        {
            throw new EvalException($"{EvalErrors.Overflow} while attempting to multiply `{leftValue}` by `{rightValue}`");
        }
    }
}

internal sealed class NegateEvaluator(IEvaluator inner) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        long value = TypeConversion.ValueToLong(inner.Eval(env));

        try
        {
            return new CedarLong(checked(-value));
        }
        catch (OverflowException)
        {
            throw new EvalException($"{EvalErrors.Overflow} while attempting to negate `{value}`");
        }
    }
}

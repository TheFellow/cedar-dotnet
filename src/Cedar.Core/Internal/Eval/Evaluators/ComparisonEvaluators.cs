using System;
using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class EqualEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        return new CedarBool(left.Eval(env).Equals(right.Eval(env)));
    }
}

internal sealed class NotEqualEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        return new CedarBool(!left.Eval(env).Equals(right.Eval(env)));
    }
}

internal sealed class LessThanEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        return new CedarBool(ComparableValues.Compare(left.Eval(env), right.Eval(env)) < 0);
    }
}

internal sealed class LessThanOrEqualEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        return new CedarBool(ComparableValues.Compare(left.Eval(env), right.Eval(env)) <= 0);
    }
}

internal sealed class GreaterThanEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        return new CedarBool(ComparableValues.Compare(left.Eval(env), right.Eval(env)) > 0);
    }
}

internal sealed class GreaterThanOrEqualEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        return new CedarBool(ComparableValues.Compare(left.Eval(env), right.Eval(env)) >= 0);
    }
}

internal static class ComparableValues
{
    public static int Compare(ICedarData left, ICedarData right)
    {
        return (left, right) switch
        {
            (CedarLong leftLong, CedarLong rightLong) => leftLong.Value.CompareTo(rightLong.Value),
            (CedarString leftString, CedarString rightString) => StringComparer.Ordinal.Compare(leftString.Value, rightString.Value),
            (CedarDecimal leftDecimal, CedarDecimal rightDecimal) => leftDecimal.Value.CompareTo(rightDecimal.Value),
            (CedarDatetime leftDatetime, CedarDatetime rightDatetime) => leftDatetime.Value.CompareTo(rightDatetime.Value),
            (CedarDuration leftDuration, CedarDuration rightDuration) => leftDuration.Value.CompareTo(rightDuration.Value),
            _ => CompareFailure(left, right)
        };
    }

    private static int CompareFailure(ICedarData left, ICedarData right)
    {
        if (IsComparable(left) && IsComparable(right))
        {
            throw new EvalException($"cannot compare {EvalErrors.TypeName(left)} with {EvalErrors.TypeName(right)}");
        }

        throw new EvalException($"expected comparable value, got {EvalErrors.TypeName(!IsComparable(left) ? left : right)}");
    }

    private static bool IsComparable(ICedarData value)
    {
        return value is CedarLong or CedarString or CedarDecimal or CedarDatetime or CedarDuration;
    }
}

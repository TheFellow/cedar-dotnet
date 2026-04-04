using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class ContainsEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        CedarSet leftValue = TypeConversion.ValueToSet(left.Eval(env));
        ICedarData rightValue = right.Eval(env);
        return leftValue.Contains(rightValue) ? CedarBool.True : CedarBool.False;
    }
}

internal sealed class ContainsAllEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        CedarSet leftValue = TypeConversion.ValueToSet(left.Eval(env));
        CedarSet rightValue = TypeConversion.ValueToSet(right.Eval(env));

        foreach (ICedarData element in rightValue)
        {
            if (!leftValue.Contains(element))
            {
                return CedarBool.False;
            }
        }

        return CedarBool.True;
    }
}

internal sealed class ContainsAnyEvaluator(IEvaluator left, IEvaluator right) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        CedarSet leftValue = TypeConversion.ValueToSet(left.Eval(env));
        CedarSet rightValue = TypeConversion.ValueToSet(right.Eval(env));

        foreach (ICedarData element in rightValue)
        {
            if (leftValue.Contains(element))
            {
                return CedarBool.True;
            }
        }

        return CedarBool.False;
    }
}

internal sealed class IsEmptyEvaluator(IEvaluator inner) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        return TypeConversion.ValueToSet(inner.Eval(env)).Count == 0 ? CedarBool.True : CedarBool.False;
    }
}

internal sealed class SetLiteralEvaluator(IEvaluator[] elements) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        ICedarData[] values = new ICedarData[elements.Length];
        for (int index = 0; index < elements.Length; index++)
        {
            values[index] = elements[index].Eval(env);
        }

        return new CedarSet(values);
    }
}

internal sealed class RecordLiteralEvaluator(KeyValuePair<CedarString, IEvaluator>[] elements) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        Dictionary<CedarString, ICedarData> values = [];
        foreach (KeyValuePair<CedarString, IEvaluator> element in elements)
        {
            values[element.Key] = element.Value.Eval(env);
        }

        return new CedarRecord(values);
    }
}

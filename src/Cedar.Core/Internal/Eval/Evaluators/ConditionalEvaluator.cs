using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class ConditionalEvaluator(IEvaluator ifEvaluator, IEvaluator thenEvaluator, IEvaluator elseEvaluator) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        if (TypeConversion.ValueToBool(ifEvaluator.Eval(env)))
        {
            return thenEvaluator.Eval(env);
        }

        return elseEvaluator.Eval(env);
    }
}

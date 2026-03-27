using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class PartialErrorEvaluator(IEvaluator message) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        string value = TypeConversion.ValueToString(message.Eval(env));
        throw new EvalException(value);
    }
}

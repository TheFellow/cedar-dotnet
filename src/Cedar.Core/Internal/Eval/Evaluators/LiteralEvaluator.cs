using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class LiteralEvaluator(ICedarData value) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        return value;
    }
}

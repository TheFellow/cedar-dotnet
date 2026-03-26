using Cedar.Types;

namespace Cedar.Core.Internal.Eval;

internal sealed class BoolEvaluator(IEvaluator inner)
{
    public bool Eval(EvalEnv env)
    {
        ICedarData result = inner.Eval(env);
        if (result is CedarBool b)
        {
            return b.Value;
        }

        throw new EvalException("expected boolean value");
    }
}

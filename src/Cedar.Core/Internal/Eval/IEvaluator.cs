using Cedar.Types;

namespace Cedar.Core.Internal.Eval;

internal interface IEvaluator
{
    ICedarData Eval(EvalEnv env);
}

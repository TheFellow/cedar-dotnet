using Cedar.Core.Internal.Consts;
using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class VariableEvaluator(CedarString variableName) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        return variableName.Value switch
        {
            CedarConsts.Principal => env.Principal,
            CedarConsts.Action => env.Action,
            CedarConsts.Resource => env.Resource,
            CedarConsts.Context => env.Context ?? throw new EvalException("missing context"),
            _ => throw new EvalException($"unknown variable `{variableName.Value}`")
        };
    }
}

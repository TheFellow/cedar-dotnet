using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class LikeEvaluator(IEvaluator value, CedarPattern pattern) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        return pattern.Match(new CedarString(TypeConversion.ValueToString(value.Eval(env)))) ? CedarBool.True : CedarBool.False;
    }
}

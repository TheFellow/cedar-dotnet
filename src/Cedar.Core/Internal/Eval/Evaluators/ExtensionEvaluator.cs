using Cedar.Core.Internal.Extensions;
using Cedar.Types;

namespace Cedar.Core.Internal.Eval.Evaluators;

internal sealed class ExtensionEvaluator(string name, IEvaluator[] args) : IEvaluator
{
    public ICedarData Eval(EvalEnv env)
    {
        ICedarData[] values = new ICedarData[args.Length];
        for (int index = 0; index < args.Length; index++)
        {
            values[index] = args[index].Eval(env);
        }

        return ExtensionRegistry.Invoke(name, values);
    }
}

using Cedar.Core.Internal.Eval;
using Cedar.Types;

namespace Cedar.Experimental;

public sealed class EvalEnv
{
    public EvalEnv(
        IEntityGetter? entities = null,
        ICedarData? principal = null,
        ICedarData? action = null,
        ICedarData? resource = null,
        ICedarData? context = null)
    {
        Entities = entities ?? new EntityMap();
        Principal = principal ?? PartialEvaluator.Variable("principal");
        Action = action ?? PartialEvaluator.Variable("action");
        Resource = resource ?? PartialEvaluator.Variable("resource");
        Context = context ?? PartialEvaluator.Variable("context");
    }

    public IEntityGetter Entities { get; }

    public ICedarData Principal { get; }

    public ICedarData Action { get; }

    public ICedarData Resource { get; }

    public ICedarData Context { get; }

    internal Cedar.Core.Internal.Eval.EvalEnv ToInternal()
    {
        return new Cedar.Core.Internal.Eval.EvalEnv(Entities, Principal, Action, Resource, Context);
    }
}

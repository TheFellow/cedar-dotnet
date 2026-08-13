using Cedar.Types;

namespace Cedar.Core.Internal.Eval;

internal readonly record struct EvalEnv(IEntityGetter Entities, ICedarData Principal, ICedarData Action, ICedarData Resource, ICedarData? Context)
{
    public static EvalEnv FromRequest(IEntityGetter entities, Request request)
    {
        return new EvalEnv(entities, request.Principal, request.Action, request.Resource, request.Context);
    }
}

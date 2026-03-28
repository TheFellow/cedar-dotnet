using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Core.Internal.Eval;

internal sealed record EvalEnv(IEntityGetter Entities, ICedarData Principal, ICedarData Action, ICedarData Resource, ICedarData? Context)
{
    internal Dictionary<(EntityUid Lhs, EntityUid Rhs), bool> InCache { get; } = [];

    public static EvalEnv FromRequest(IEntityGetter entities, Request request)
    {
        return new EvalEnv(entities, request.Principal, request.Action, request.Resource, request.Context);
    }
}

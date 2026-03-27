using System;
using Cedar.Ast;
using Cedar.Core.Internal.Eval;
using Cedar.Types;

namespace Cedar.Experimental;

public static class NodeEvaluation
{
    public static ICedarData Evaluate(Node node, EvalEnv env)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(env);
        return Compiler.ToEval(node.Inner).Eval(env.ToInternal());
    }
}

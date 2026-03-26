using System;

namespace Cedar.Core.Internal.Eval;

internal sealed class EvalException : Exception
{
    public EvalException(string message)
        : base(message)
    {
    }
}

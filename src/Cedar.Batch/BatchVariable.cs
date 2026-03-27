using System;
using Cedar.Core.Internal.Eval;
using Cedar.Types;

namespace Cedar.Batch;

public static class BatchVariable
{
    public static EntityUid Variable(string name)
    {
        return PartialEvaluator.Variable(name);
    }

    public static EntityUid Ignore()
    {
        return PartialEvaluator.Ignore();
    }

    public static bool IsVariable(ICedarData value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return PartialEvaluator.IsVariable(value);
    }

    public static bool IsIgnore(ICedarData value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return PartialEvaluator.IsIgnore(value);
    }

    public static bool TryGetName(ICedarData value, out string name)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (PartialEvaluator.TryGetVariableName(value, out CedarString variableName))
        {
            name = variableName.Value;
            return true;
        }

        name = string.Empty;
        return false;
    }
}

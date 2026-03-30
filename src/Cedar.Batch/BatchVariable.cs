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
}

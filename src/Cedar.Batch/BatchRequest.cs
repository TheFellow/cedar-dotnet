using System.Collections.Generic;
using Cedar.Types;

namespace Cedar.Batch;

public sealed record BatchRequest(ICedarData? Principal, ICedarData? Action, ICedarData? Resource, ICedarData? Context)
{
    public IReadOnlyDictionary<string, IReadOnlyList<ICedarData>> Variables { get; init; }
        = new Dictionary<string, IReadOnlyList<ICedarData>>(System.StringComparer.Ordinal);
}

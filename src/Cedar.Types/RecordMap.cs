using System.Collections.Generic;

namespace Cedar.Types;

public sealed class RecordMap : Dictionary<CedarString, ICedarData>
{
    public RecordMap()
    {
    }

    public RecordMap(IDictionary<CedarString, ICedarData> dictionary)
        : base(dictionary)
    {
    }
}

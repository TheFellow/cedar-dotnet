using System.Collections.Generic;

namespace Cedar.Core;

public interface IPolicyIterator
{
    IEnumerable<Policy> Policies { get; }
}

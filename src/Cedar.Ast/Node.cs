using System;
using Cedar.Ast.Internal;

namespace Cedar.Ast;

public sealed class Node
{
    internal Node(INode inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        Inner = inner;
    }

    internal INode Inner { get; }
}

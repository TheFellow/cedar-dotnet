using Cedar.Ast.Internal;
using Cedar.Types;

namespace Cedar.Ast;

public static class Variables
{
    public static Node Principal()
    {
        return new Node(new NodeVariable(new CedarString("principal")));
    }

    public static Node Action()
    {
        return new Node(new NodeVariable(new CedarString("action")));
    }

    public static Node Resource()
    {
        return new Node(new NodeVariable(new CedarString("resource")));
    }

    public static Node Context()
    {
        return new Node(new NodeVariable(new CedarString("context")));
    }
}

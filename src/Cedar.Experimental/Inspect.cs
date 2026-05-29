using System;
using Cedar.Ast;
using Cedar.Ast.Internal;

namespace Cedar.Experimental;

public static class AstInspect
{
    internal static void Inspect(Node node, Func<INode, bool> fn)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(fn);

        InspectNode(node.Inner, fn);
    }

    private static void InspectNode(INode? node, Func<INode, bool> fn)
    {
        if (node is null)
        {
            return;
        }

        if (!fn(node))
        {
            return;
        }

        switch (node)
        {
            case NodeIfThenElse ifThenElse:
                InspectNode(ifThenElse.If, fn);
                InspectNode(ifThenElse.Then, fn);
                InspectNode(ifThenElse.Else, fn);
                break;
            case NodeOr or:
                InspectNode(or.Left, fn);
                InspectNode(or.Right, fn);
                break;
            case NodeAnd and:
                InspectNode(and.Left, fn);
                InspectNode(and.Right, fn);
                break;
            case NodeLessThan lessThan:
                InspectNode(lessThan.Left, fn);
                InspectNode(lessThan.Right, fn);
                break;
            case NodeLessThanOrEqual lessThanOrEqual:
                InspectNode(lessThanOrEqual.Left, fn);
                InspectNode(lessThanOrEqual.Right, fn);
                break;
            case NodeGreaterThan greaterThan:
                InspectNode(greaterThan.Left, fn);
                InspectNode(greaterThan.Right, fn);
                break;
            case NodeGreaterThanOrEqual greaterThanOrEqual:
                InspectNode(greaterThanOrEqual.Left, fn);
                InspectNode(greaterThanOrEqual.Right, fn);
                break;
            case NodeNotEquals notEquals:
                InspectNode(notEquals.Left, fn);
                InspectNode(notEquals.Right, fn);
                break;
            case NodeEquals equals:
                InspectNode(equals.Left, fn);
                InspectNode(equals.Right, fn);
                break;
            case NodeIn @in:
                InspectNode(@in.Left, fn);
                InspectNode(@in.Right, fn);
                break;
            case NodeHasTag hasTag:
                InspectNode(hasTag.Left, fn);
                InspectNode(hasTag.Right, fn);
                break;
            case NodeGetTag getTag:
                InspectNode(getTag.Left, fn);
                InspectNode(getTag.Right, fn);
                break;
            case NodeContains contains:
                InspectNode(contains.Left, fn);
                InspectNode(contains.Right, fn);
                break;
            case NodeContainsAll containsAll:
                InspectNode(containsAll.Left, fn);
                InspectNode(containsAll.Right, fn);
                break;
            case NodeContainsAny containsAny:
                InspectNode(containsAny.Left, fn);
                InspectNode(containsAny.Right, fn);
                break;
            case NodeAdd add:
                InspectNode(add.Left, fn);
                InspectNode(add.Right, fn);
                break;
            case NodeSub sub:
                InspectNode(sub.Left, fn);
                InspectNode(sub.Right, fn);
                break;
            case NodeMult mult:
                InspectNode(mult.Left, fn);
                InspectNode(mult.Right, fn);
                break;
            case NodeHas has:
                InspectNode(has.Arg, fn);
                break;
            case NodeAccess access:
                InspectNode(access.Arg, fn);
                InspectNode(access.Attribute, fn);
                break;
            case NodeLike like:
                InspectNode(like.Arg, fn);
                break;
            case NodeIs @is:
                InspectNode(@is.Left, fn);
                break;
            case NodeIsIn isIn:
                InspectNode(isIn.Left, fn);
                InspectNode(isIn.Entity, fn);
                break;
            case NodeNegate negate:
                InspectNode(negate.Arg, fn);
                break;
            case NodeNot not:
                InspectNode(not.Arg, fn);
                break;
            case NodeIsEmpty isEmpty:
                InspectNode(isEmpty.Arg, fn);
                break;
            case NodeExtensionCall extensionCall:
                foreach (INode arg in extensionCall.Args)
                {
                    InspectNode(arg, fn);
                }

                break;
            case NodeRecord record:
                foreach (NodeRecordElement element in record.Elements)
                {
                    InspectNode(element.Value, fn);
                }

                break;
            case NodeSet set:
                foreach (INode element in set.Elements)
                {
                    InspectNode(element, fn);
                }

                break;
            case NodeValue:
            case NodeVariable:
            default:
                break;
        }
    }
}

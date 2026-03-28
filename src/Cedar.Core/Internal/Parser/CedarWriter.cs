using System;
using System.Collections.Immutable;
using System.Text;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Core.Internal.Extensions;
using Cedar.Types;

namespace Cedar.Core.Internal.Parser;

public static class CedarWriter
{
    private const int PrecIf = 1;
    private const int PrecOr = 2;
    private const int PrecAnd = 3;
    private const int PrecRel = 4;
    private const int PrecAdd = 5;
    private const int PrecMult = 6;
    private const int PrecUnary = 7;
    private const int PrecAccess = 8;
    private const int PrecPrimary = 9;

    public static string Write(PolicyAst policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        StringBuilder builder = new();

        foreach (Annotation annotation in policy.Annotations)
        {
            builder.Append('@');
            builder.Append(annotation.Key.Value);
            builder.Append('(');
            builder.Append(annotation.Value.MarshalCedar());
            builder.Append(')');
            builder.AppendLine();
        }

        builder.Append(policy.Effect == Effect.Permit ? "permit" : "forbid");
        builder.Append('(');
        WriteScopeConstraint(builder, "principal", policy.PrincipalScope);
        builder.Append(", ");
        WriteScopeConstraint(builder, "action", policy.ActionScope);
        builder.Append(", ");
        WriteScopeConstraint(builder, "resource", policy.ResourceScope);
        builder.Append(')');

        foreach (INode condition in policy.Conditions)
        {
            builder.AppendLine();
            builder.Append("  ");

            if (condition is NodeNot not)
            {
                builder.Append("unless { ");
                WriteNode(builder, not.Arg);
                builder.Append(" }");
            }
            else
            {
                builder.Append("when { ");
                WriteNode(builder, condition);
                builder.Append(" }");
            }
        }

        builder.Append(';');
        return builder.ToString();
    }

    public static string Write(PolicyAst[] policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        StringBuilder builder = new();
        for (int index = 0; index < policies.Length; index++)
        {
            if (index > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(Write(policies[index]));
        }

        return builder.ToString();
    }

    private static void WriteScopeConstraint(StringBuilder builder, string variable, IScope scope)
    {
        builder.Append(variable);

        switch (scope)
        {
            case ScopeAll:
                return;
            case ScopeEq eq:
                builder.Append(" == ");
                builder.Append(eq.Entity.MarshalCedar());
                return;
            case ScopeIn @in:
                builder.Append(" in ");
                builder.Append(@in.Entity.MarshalCedar());
                return;
            case ScopeInSet inSet:
                builder.Append(" in [");
                for (int i = 0; i < inSet.Entities.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(inSet.Entities[i].MarshalCedar());
                }

                builder.Append(']');
                return;
            case ScopeIs isScope:
                builder.Append(" is ");
                builder.Append(isScope.Type.Value);
                return;
            case ScopeIsIn isIn:
                builder.Append(" is ");
                builder.Append(isIn.Type.Value);
                builder.Append(" in ");
                builder.Append(isIn.Entity.MarshalCedar());
                return;
            default:
                throw new InvalidOperationException($"Unsupported scope node: {scope.GetType().FullName}");
        }
    }

    private static void WriteNode(StringBuilder builder, INode node, int parentPrecedence = 0)
    {
        int precedence = GetPrecedence(node);
        bool wrap = precedence < parentPrecedence;

        if (wrap)
        {
            builder.Append('(');
        }

        switch (node)
        {
            case NodeIfThenElse @if:
                builder.Append("if ");
                WriteNode(builder, @if.If, PrecIf + 1);
                builder.Append(" then ");
                WriteNode(builder, @if.Then, PrecIf);
                builder.Append(" else ");
                WriteNode(builder, @if.Else, PrecIf);
                break;
            case NodeOr or:
                WriteNode(builder, or.Left, PrecOr);
                builder.Append(" || ");
                WriteNode(builder, or.Right, PrecOr + 1);
                break;
            case NodeAnd and:
                WriteNode(builder, and.Left, PrecAnd);
                builder.Append(" && ");
                WriteNode(builder, and.Right, PrecAnd + 1);
                break;
            case NodeLessThan lt:
                WriteNode(builder, lt.Left, PrecRel + 1);
                builder.Append(" < ");
                WriteNode(builder, lt.Right, PrecRel + 1);
                break;
            case NodeLessThanOrEqual lte:
                WriteNode(builder, lte.Left, PrecRel + 1);
                builder.Append(" <= ");
                WriteNode(builder, lte.Right, PrecRel + 1);
                break;
            case NodeGreaterThan gt:
                WriteNode(builder, gt.Left, PrecRel + 1);
                builder.Append(" > ");
                WriteNode(builder, gt.Right, PrecRel + 1);
                break;
            case NodeGreaterThanOrEqual gte:
                WriteNode(builder, gte.Left, PrecRel + 1);
                builder.Append(" >= ");
                WriteNode(builder, gte.Right, PrecRel + 1);
                break;
            case NodeEquals eq:
                WriteNode(builder, eq.Left, PrecRel + 1);
                builder.Append(" == ");
                WriteNode(builder, eq.Right, PrecRel + 1);
                break;
            case NodeNotEquals neq:
                WriteNode(builder, neq.Left, PrecRel + 1);
                builder.Append(" != ");
                WriteNode(builder, neq.Right, PrecRel + 1);
                break;
            case NodeIn @in:
                WriteNode(builder, @in.Left, PrecRel + 1);
                builder.Append(" in ");
                WriteNode(builder, @in.Right, PrecRel + 1);
                break;
            case NodeIs isNode:
                WriteNode(builder, isNode.Left, PrecRel + 1);
                builder.Append(" is ");
                builder.Append(isNode.EntityType.Value);
                break;
            case NodeIsIn isInNode:
                WriteNode(builder, isInNode.Left, PrecRel + 1);
                builder.Append(" is ");
                builder.Append(isInNode.EntityType.Value);
                builder.Append(" in ");
                WriteNode(builder, isInNode.Entity, PrecRel + 1);
                break;
            case NodeHas has:
                WriteNode(builder, has.Arg, PrecRel + 1);
                builder.Append(" has ");
                if (CanWriteIdentifier(has.Attribute.Value))
                {
                    builder.Append(has.Attribute.Value);
                }
                else
                {
                    builder.Append(has.Attribute.MarshalCedar());
                }

                break;
            case NodeLike like:
                WriteNode(builder, like.Arg, PrecRel + 1);
                builder.Append(" like ");
                builder.Append(like.Pattern.MarshalCedar());
                break;
            case NodeAdd add:
                WriteNode(builder, add.Left, PrecAdd);
                builder.Append(" + ");
                WriteNode(builder, add.Right, PrecAdd + 1);
                break;
            case NodeSub sub:
                WriteNode(builder, sub.Left, PrecAdd);
                builder.Append(" - ");
                WriteNode(builder, sub.Right, PrecAdd + 1);
                break;
            case NodeMult mult:
                WriteNode(builder, mult.Left, PrecMult);
                builder.Append(" * ");
                WriteNode(builder, mult.Right, PrecMult + 1);
                break;
            case NodeNot not:
                builder.Append('!');
                WriteNode(builder, not.Arg, PrecUnary);
                break;
            case NodeNegate negate:
                builder.Append('-');
                WriteNode(builder, negate.Arg, PrecUnary);
                break;
            case NodeAccess access:
                WriteNode(builder, access.Arg, PrecAccess);
                if (access.Attribute is NodeValue { Value: CedarString staticAttr })
                {
                    if (CanWriteIdentifier(staticAttr.Value))
                    {
                        builder.Append('.');
                        builder.Append(staticAttr.Value);
                    }
                    else
                    {
                        builder.Append('[');
                        builder.Append(staticAttr.MarshalCedar());
                        builder.Append(']');
                    }
                }
                else
                {
                    builder.Append('[');
                    WriteNode(builder, access.Attribute);
                    builder.Append(']');
                }

                break;
            case NodeGetTag getTag:
                WriteNode(builder, getTag.Left, PrecAccess);
                builder.Append(".getTag(");
                WriteNode(builder, getTag.Right);
                builder.Append(')');
                break;
            case NodeHasTag hasTag:
                WriteNode(builder, hasTag.Left, PrecAccess);
                builder.Append(".hasTag(");
                WriteNode(builder, hasTag.Right);
                builder.Append(')');
                break;
            case NodeContains contains:
                WriteNode(builder, contains.Left, PrecAccess);
                builder.Append(".contains(");
                WriteNode(builder, contains.Right);
                builder.Append(')');
                break;
            case NodeContainsAll containsAll:
                WriteNode(builder, containsAll.Left, PrecAccess);
                builder.Append(".containsAll(");
                WriteNode(builder, containsAll.Right);
                builder.Append(')');
                break;
            case NodeContainsAny containsAny:
                WriteNode(builder, containsAny.Left, PrecAccess);
                builder.Append(".containsAny(");
                WriteNode(builder, containsAny.Right);
                builder.Append(')');
                break;
            case NodeIsEmpty isEmpty:
                WriteNode(builder, isEmpty.Arg, PrecAccess);
                builder.Append(".isEmpty()");
                break;
            case NodeExtensionCall call:
                if (ExtensionRegistry.TryGet(call.Name.Value, out ExtensionDefinition definition) && definition.IsMethod && call.Args.Length > 0)
                {
                    WriteNode(builder, call.Args[0], PrecAccess);
                    builder.Append('.');
                    builder.Append(call.Name.Value);
                    builder.Append('(');
                    for (int i = 1; i < call.Args.Length; i++)
                    {
                        if (i > 1)
                        {
                            builder.Append(", ");
                        }

                        WriteNode(builder, call.Args[i]);
                    }

                    builder.Append(')');
                }
                else
                {
                    builder.Append(call.Name.Value);
                    builder.Append('(');
                    for (int i = 0; i < call.Args.Length; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(", ");
                        }

                        WriteNode(builder, call.Args[i]);
                    }

                    builder.Append(')');
                }

                break;
            case NodeValue value:
                builder.Append(value.Value.MarshalCedar());
                break;
            case NodeVariable variable:
                builder.Append(variable.Name.Value);
                break;
            case NodeRecord record:
                builder.Append('{');
                for (int i = 0; i < record.Elements.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    NodeRecordElement element = record.Elements[i];
                    if (CanWriteIdentifier(element.Key.Value))
                    {
                        builder.Append(element.Key.Value);
                    }
                    else
                    {
                        builder.Append(element.Key.MarshalCedar());
                    }

                    builder.Append(": ");
                    WriteNode(builder, element.Value);
                }

                builder.Append('}');
                break;
            case NodeSet set:
                builder.Append('[');
                for (int i = 0; i < set.Elements.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    WriteNode(builder, set.Elements[i]);
                }

                builder.Append(']');
                break;
            default:
                throw new InvalidOperationException($"Unsupported node type: {node.GetType().FullName}");
        }

        if (wrap)
        {
            builder.Append(')');
        }
    }

    private static int GetPrecedence(INode node)
    {
        return node switch
        {
            NodeIfThenElse => PrecIf,
            NodeOr => PrecOr,
            NodeAnd => PrecAnd,
            NodeLessThan or NodeLessThanOrEqual or NodeGreaterThan or NodeGreaterThanOrEqual or NodeEquals or NodeNotEquals or NodeIn or NodeIs or NodeIsIn or NodeHas or NodeLike => PrecRel,
            NodeAdd or NodeSub => PrecAdd,
            NodeMult => PrecMult,
            NodeNot or NodeNegate => PrecUnary,
            NodeAccess or NodeGetTag or NodeHasTag or NodeContains or NodeContainsAll or NodeContainsAny or NodeIsEmpty => PrecAccess,
            _ => PrecPrimary
        };
    }

    private static bool CanWriteIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || IsReservedKeyword(value))
        {
            return false;
        }

        if (!IsIdentifierStart(value[0]))
        {
            return false;
        }

        for (int index = 1; index < value.Length; index++)
        {
            if (!IsIdentifierPart(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsReservedKeyword(string value)
    {
        return value is "permit"
            or "forbid"
            or "when"
            or "unless"
            or "true"
            or "false"
            or "if"
            or "then"
            or "else"
            or "in"
            or "like"
            or "has"
            or "is"
            or "__cedar";
    }

    private static bool IsIdentifierStart(char value)
    {
        return value == '_' || value is >= 'A' and <= 'Z' || value is >= 'a' and <= 'z';
    }

    private static bool IsIdentifierPart(char value)
    {
        return IsIdentifierStart(value) || value is >= '0' and <= '9';
    }
}

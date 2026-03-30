using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Cedar.Ast.Internal;
using Cedar.Types;

namespace Cedar.Schema.Internal.Validate;

internal sealed class TypeChecker
{
    private readonly SchemaValidator _validator;

    internal TypeChecker(SchemaValidator validator)
    {
        _validator = validator;
    }

    internal (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors)
        TypeOfExpr(RequestEnvironment env, INode expr, CapabilitySet caps)
    {
        return expr switch
        {
            NodeValue nodeValue => TypeOfValue(nodeValue.Value, caps),
            NodeVariable nodeVariable => (TypeOfVariable(env, nodeVariable.Name), caps, []),
            NodeAnd nodeAnd => TypeOfAnd(env, nodeAnd, caps),
            NodeOr nodeOr => TypeOfOr(env, nodeOr, caps),
            NodeNot nodeNot => TypeOfNot(env, nodeNot, caps),
            NodeIfThenElse nodeIfThenElse => TypeOfIfThenElse(env, nodeIfThenElse, caps),
            NodeEquals nodeEquals => TypeOfEquality(env, nodeEquals.Left, nodeEquals.Right, negated: false, caps),
            NodeNotEquals nodeNotEquals => TypeOfEquality(env, nodeNotEquals.Left, nodeNotEquals.Right, negated: true, caps),
            NodeLessThan nodeLessThan => TypeOfComparison(env, nodeLessThan.Left, nodeLessThan.Right, caps, ExpectComparable, ExpectComparable),
            NodeLessThanOrEqual nodeLessThanOrEqual => TypeOfComparison(env, nodeLessThanOrEqual.Left, nodeLessThanOrEqual.Right, caps, ExpectComparable, ExpectComparable),
            NodeGreaterThan nodeGreaterThan => TypeOfComparison(env, nodeGreaterThan.Left, nodeGreaterThan.Right, caps, ExpectComparable, ExpectComparable),
            NodeGreaterThanOrEqual nodeGreaterThanOrEqual => TypeOfComparison(env, nodeGreaterThanOrEqual.Left, nodeGreaterThanOrEqual.Right, caps, ExpectComparable, ExpectComparable),
            NodeAdd nodeAdd => TypeOfArith(env, nodeAdd.Left, nodeAdd.Right, caps),
            NodeSub nodeSub => TypeOfArith(env, nodeSub.Left, nodeSub.Right, caps),
            NodeMult nodeMult => TypeOfArith(env, nodeMult.Left, nodeMult.Right, caps),
            NodeNegate nodeNegate => TypeOfNegate(env, nodeNegate, caps),
            NodeIn nodeIn => TypeOfIn(env, nodeIn, caps),
            NodeContains nodeContains => TypeOfContains(env, nodeContains, caps),
            NodeContainsAll nodeContainsAll => TypeOfContainsAllAny(env, nodeContainsAll.Left, nodeContainsAll.Right, caps),
            NodeContainsAny nodeContainsAny => TypeOfContainsAllAny(env, nodeContainsAny.Left, nodeContainsAny.Right, caps),
            NodeIsEmpty nodeIsEmpty => TypeOfIsEmpty(env, nodeIsEmpty, caps),
            NodeLike nodeLike => TypeOfLike(env, nodeLike, caps),
            NodeIs nodeIs => TypeOfIs(env, nodeIs, caps),
            NodeIsIn nodeIsIn => TypeOfIsIn(env, nodeIsIn, caps),
            NodeHas nodeHas => TypeOfHas(env, nodeHas, caps),
            NodeAccess nodeAccess => TypeOfAccess(env, nodeAccess, caps),
            NodeHasTag nodeHasTag => TypeOfHasTag(env, nodeHasTag, caps),
            NodeGetTag nodeGetTag => TypeOfGetTag(env, nodeGetTag, caps),
            NodeRecord nodeRecord => TypeOfRecord(env, nodeRecord, caps),
            NodeSet nodeSet => TypeOfSet(env, nodeSet, caps),
            NodeExtensionCall nodeExtensionCall => TypeOfExtensionCall(env, nodeExtensionCall, caps),
            _ => (null, caps, [new ValidationIssue($"unsupported node type {expr.GetType().FullName}")])
        };
    }

    internal List<ValidationIssue> TypecheckConditions(List<RequestEnvironment> envs, ImmutableArray<INode> conditions)
    {
        List<ValidationIssue> allIssues = [];

        foreach (INode condition in conditions)
        {
            Dictionary<string, (ValidationIssue Issue, int Count)> merged = new(StringComparer.Ordinal);
            List<string> order = [];
            Dictionary<string, Dictionary<EntityType, int>> principalTagByType = new(StringComparer.Ordinal);
            Dictionary<string, Dictionary<EntityType, int>> resourceTagByType = new(StringComparer.Ordinal);

            foreach (RequestEnvironment env in envs)
            {
                (CedarType? type, _, List<ValidationIssue> envIssues) = TypeOfExpr(env, condition, CapabilitySet.Create());
                List<ValidationIssue> issues = [.. envIssues];

                if (type is not null && !IsBoolType(type))
                {
                    issues.Add(new ValidationIssue($"unexpected type: expected Bool but saw {CedarTypeOps.CedarTypeName(type)}"));
                }

                Dictionary<string, (ValidationIssue Issue, int Count)> envCounts = new(StringComparer.Ordinal);
                foreach (ValidationIssue issue in issues)
                {
                    if (envCounts.TryGetValue(issue.Message, out (ValidationIssue Issue, int Count) existing))
                    {
                        envCounts[issue.Message] = (existing.Issue, existing.Count + 1);
                    }
                    else
                    {
                        envCounts[issue.Message] = (issue, 1);
                    }
                }

                foreach ((string message, (ValidationIssue issue, int count)) in envCounts)
                {
                    if (!merged.ContainsKey(message))
                    {
                        order.Add(message);
                        merged[message] = (issue, count);
                    }
                    else
                    {
                        merged[message] = (merged[message].Issue, Math.Max(merged[message].Count, count));
                    }

                    if (issue is UnsafeTagAccessIssue unsafeTag)
                    {
                        if (unsafeTag.UsesPrincipal)
                        {
                            UpdateUnsafeTagCounts(principalTagByType, message, env.PrincipalType, count);
                        }

                        if (unsafeTag.UsesResource)
                        {
                            UpdateUnsafeTagCounts(resourceTagByType, message, env.ResourceType, count);
                        }
                    }
                }
            }

            foreach (string message in order)
            {
                if (principalTagByType.TryGetValue(message, out Dictionary<EntityType, int>? principalCounts))
                {
                    merged[message] = (merged[message].Issue, principalCounts.Values.Sum());
                    continue;
                }

                if (resourceTagByType.TryGetValue(message, out Dictionary<EntityType, int>? resourceCounts))
                {
                    merged[message] = (merged[message].Issue, resourceCounts.Values.Sum());
                }
            }

            foreach (string message in order)
            {
                (ValidationIssue issue, int count) = merged[message];
                for (int index = 0; index < count; index++)
                {
                    allIssues.Add(issue);
                }
            }
        }

        return allIssues;
    }

    private static void UpdateUnsafeTagCounts(
        IDictionary<string, Dictionary<EntityType, int>> countsByMessage,
        string message,
        EntityType entityType,
        int count)
    {
        if (!countsByMessage.TryGetValue(message, out Dictionary<EntityType, int>? counts))
        {
            counts = [];
            countsByMessage[message] = counts;
        }

        counts[entityType] = Math.Max(counts.TryGetValue(entityType, out int existing) ? existing : 0, count);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfValue(ICedarData value, CapabilitySet caps)
    {
        return value switch
        {
            CedarBool boolean => (boolean.Value ? CedarTrueType.Instance : CedarFalseType.Instance, caps, []),
            CedarLong => (CedarLongType.Instance, caps, []),
            CedarString => (CedarStringType.Instance, caps, []),
            EntityUid entityUid => TypeOfEntityUid(entityUid, caps),
            CedarIpAddress => (new CedarExtType(new Ident("ipaddr")), caps, []),
            CedarDecimal => (new CedarExtType(new Ident("decimal")), caps, []),
            CedarDatetime => (new CedarExtType(new Ident("datetime")), caps, []),
            CedarDuration => (new CedarExtType(new Ident("duration")), caps, []),
            CedarSet set => TypeOfLiteralSet(set, caps),
            CedarRecord record => TypeOfLiteralRecord(record, caps),
            _ => (null, caps, [new ValidationIssue($"unsupported Cedar literal value {value.GetType().FullName}")])
        };
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfEntityUid(EntityUid uid, CapabilitySet caps)
    {
        EntityType entityType = uid.Type;
        if (_validator.IsKnownEntityType(entityType))
        {
            return (new CedarEntityType(EntityLub.Single(entityType)), caps, []);
        }

        if (CedarTypeOps.IsActionEntity(entityType))
        {
            if (_validator.Schema.Actions.ContainsKey(uid))
            {
                return (new CedarEntityType(EntityLub.Single(entityType)), caps, []);
            }

            if (_validator.Schema.Actions.Keys.Any(actionUid => actionUid.Type == entityType))
            {
                return (null, caps, [new ValidationIssue($"unrecognized action `{uid}`")]);
            }
        }

        return (null, caps, [new ValidationIssue($"unrecognized entity type `{entityType}`")]);
    }

    private static CedarType TypeOfVariable(RequestEnvironment env, CedarString name)
    {
        return name.Value switch
        {
            "principal" => new CedarEntityType(EntityLub.Single(env.PrincipalType)),
            "action" => new CedarEntityType(EntityLub.Single(env.ActionUid.Type)),
            "resource" => new CedarEntityType(EntityLub.Single(env.ResourceType)),
            _ => env.ContextType
        };
    }

    private static ValidationIssue UnexpectedType(string expected, CedarType actual)
    {
        return new ValidationIssue($"unexpected type: expected {expected} but saw {CedarTypeOps.CedarTypeName(actual)}");
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfAnd(RequestEnvironment env, NodeAnd node, CapabilitySet caps)
    {
        (CedarType? leftType, CapabilitySet leftCaps, List<ValidationIssue> leftErrors) = TypeOfExpr(env, node.Left, caps);
        if (leftErrors.Count > 0)
        {
            List<ValidationIssue> errors = [.. leftErrors];
            if (leftType is not null && !IsBoolType(leftType))
            {
                errors.Add(UnexpectedType("Bool", leftType));
            }

            if (leftType is CedarFalseType)
            {
                errors.AddRange(ValidateEntityRefs(node.Right));
                return (CedarFalseType.Instance, caps, errors);
            }

            (CedarType? rightTypeAfterError, _, List<ValidationIssue> rightErrors) = TypeOfExpr(env, node.Right, caps);
            errors.AddRange(rightErrors);
            if (rightTypeAfterError is CedarFalseType)
            {
                return (CedarFalseType.Instance, caps, errors);
            }

            return (CedarBoolType.Instance, caps, errors);
        }

        if (leftType is null || !IsBoolType(leftType))
        {
            return (null, caps, [UnexpectedType("Bool", leftType ?? CedarNeverType.Instance)]);
        }

        if (leftType is CedarFalseType)
        {
            return (CedarFalseType.Instance, caps, ValidateEntityRefs(node.Right));
        }

        (CedarType? rightType, CapabilitySet rightCaps, List<ValidationIssue> rightIssues) = TypeOfExpr(env, node.Right, caps.Merge(leftCaps));
        if (rightIssues.Count > 0)
        {
            return (rightType is CedarFalseType ? CedarFalseType.Instance : CedarBoolType.Instance, caps, rightIssues);
        }

        if (rightType is null || !IsBoolType(rightType))
        {
            return (null, caps, [UnexpectedType("Bool", rightType ?? CedarNeverType.Instance)]);
        }

        if (leftType is CedarTrueType)
        {
            return (rightType, rightCaps, []);
        }

        if (rightType is CedarFalseType)
        {
            return (CedarFalseType.Instance, rightCaps, []);
        }

        return (CedarBoolType.Instance, rightCaps, []);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfOr(RequestEnvironment env, NodeOr node, CapabilitySet caps)
    {
        (CedarType? leftType, CapabilitySet leftCaps, List<ValidationIssue> leftErrors) = TypeOfExpr(env, node.Left, caps);
        if (leftErrors.Count > 0)
        {
            List<ValidationIssue> errors = [.. leftErrors];
            if (leftType is not null && !IsBoolType(leftType))
            {
                errors.Add(UnexpectedType("Bool", leftType));
            }

            (_, _, List<ValidationIssue> rightErrors) = TypeOfExpr(env, node.Right, caps);
            errors.AddRange(rightErrors);
            return (null, caps, errors);
        }

        if (leftType is null || !IsBoolType(leftType))
        {
            return (null, caps, [UnexpectedType("Bool", leftType ?? CedarNeverType.Instance)]);
        }

        if (leftType is CedarTrueType)
        {
            return (CedarTrueType.Instance, leftCaps, ValidateEntityRefs(node.Right));
        }

        (CedarType? rightType, CapabilitySet rightCaps, List<ValidationIssue> rightIssues) = TypeOfExpr(env, node.Right, caps);
        if (rightIssues.Count > 0)
        {
            return (null, caps, rightIssues);
        }

        if (rightType is null || !IsBoolType(rightType))
        {
            return (null, caps, [UnexpectedType("Bool", rightType ?? CedarNeverType.Instance)]);
        }

        if (leftType is CedarFalseType)
        {
            return (rightType, rightCaps, []);
        }

        if (rightType is CedarTrueType)
        {
            return (CedarTrueType.Instance, rightCaps, []);
        }

        if (rightType is CedarFalseType)
        {
            return (leftType, leftCaps, []);
        }

        return (CedarBoolType.Instance, leftCaps.Intersect(rightCaps), []);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfNot(RequestEnvironment env, NodeNot node, CapabilitySet caps)
    {
        (CedarType? type, _, List<ValidationIssue> issues) = TypeOfExpr(env, node.Arg, caps);
        if (issues.Count > 0)
        {
            if (type is not null && !IsBoolType(type))
            {
                issues.Add(UnexpectedType("Bool", type));
            }

            return (null, caps, issues);
        }

        if (type is null || !IsBoolType(type))
        {
            return (null, caps, [UnexpectedType("Bool", type ?? CedarNeverType.Instance)]);
        }

        return type switch
        {
            CedarTrueType => (CedarFalseType.Instance, caps, []),
            CedarFalseType => (CedarTrueType.Instance, caps, []),
            _ => (CedarBoolType.Instance, caps, [])
        };
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfIfThenElse(RequestEnvironment env, NodeIfThenElse node, CapabilitySet caps)
    {
        (CedarType? conditionType, CapabilitySet conditionCaps, List<ValidationIssue> conditionIssues) = TypeOfExpr(env, node.If, caps);

        if (conditionIssues.Count > 0 || (conditionType is not null && !IsBoolType(conditionType)))
        {
            List<ValidationIssue> errors = [.. conditionIssues];
            if (conditionType is not null && !IsBoolType(conditionType))
            {
                errors.Add(UnexpectedType("Bool", conditionType));
            }

            if (conditionType is null || !IsBoolType(conditionType))
            {
                (CedarType? thenType, _, List<ValidationIssue> thenErrors) = TypeOfExpr(env, node.Then, caps);
                (CedarType? elseType, _, List<ValidationIssue> elseErrors) = TypeOfExpr(env, node.Else, caps);
                errors.AddRange(thenErrors);
                errors.AddRange(elseErrors);

                CedarType? resultType = null;
                if (thenType is not null && elseType is not null)
                {
                    (CedarType? lub, string? _) = CedarTypeOps.LeastUpperBound(thenType, elseType, _validator.IsStrict);
                    resultType = lub;
                }

                return (resultType, caps, errors);
            }
        }

        CapabilitySet thenCaps = caps.Merge(conditionCaps);

        if (conditionType is CedarFalseType)
        {
            List<ValidationIssue> skippedIssues = ValidateEntityRefs(node.Then);
            (CedarType? elseType, CapabilitySet elseCaps, List<ValidationIssue> elseIssues) = TypeOfExpr(env, node.Else, caps);
            skippedIssues.AddRange(conditionIssues);
            skippedIssues.AddRange(elseIssues);
            return (elseType, elseCaps, skippedIssues);
        }

        if (conditionType is CedarTrueType)
        {
            List<ValidationIssue> skippedIssues = ValidateEntityRefs(node.Else);
            (CedarType? thenType, CapabilitySet thenResultCaps, List<ValidationIssue> thenIssues) = TypeOfExpr(env, node.Then, thenCaps);
            skippedIssues.AddRange(conditionIssues);
            skippedIssues.AddRange(thenIssues);
            return (thenType, thenResultCaps, skippedIssues);
        }

        List<ValidationIssue> errorsNormal = [.. conditionIssues];
        (CedarType? thenBranchType, CapabilitySet thenBranchCaps, List<ValidationIssue> thenBranchIssues) = TypeOfExpr(env, node.Then, thenCaps);
        (CedarType? elseBranchType, CapabilitySet elseBranchCaps, List<ValidationIssue> elseBranchIssues) = TypeOfExpr(env, node.Else, caps);
        errorsNormal.AddRange(thenBranchIssues);
        errorsNormal.AddRange(elseBranchIssues);
        if (errorsNormal.Count > 0)
        {
            CedarType? resultType = null;
            if (thenBranchType is not null && elseBranchType is not null)
            {
                (CedarType? lub, string? _) = CedarTypeOps.LeastUpperBound(thenBranchType, elseBranchType, _validator.IsStrict);
                resultType = lub;
            }

            return (resultType, caps, errorsNormal);
        }

        if (thenBranchType is null || elseBranchType is null)
        {
            return (null, caps, []);
        }

        if (_validator.IsStrict && CedarTypeOps.CheckStrictEntityLUB(thenBranchType, elseBranchType) is not null)
        {
            return (null, caps, [new TypeIncompatIssue(CedarTypeOps.TypeIncompatErr(thenBranchType, elseBranchType))]);
        }

        (CedarType? result, string? error) = CedarTypeOps.LeastUpperBound(thenBranchType, elseBranchType, _validator.IsStrict);
        return error is null
            ? (result, thenBranchCaps.Intersect(elseBranchCaps), [])
            : (null, caps, [new TypeIncompatIssue(CedarTypeOps.TypeIncompatErr(thenBranchType, elseBranchType))]);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfEquality(
        RequestEnvironment env,
        INode left,
        INode right,
        bool negated,
        CapabilitySet caps)
    {
        List<ValidationIssue> issues = [];
        (CedarType? leftType, _, List<ValidationIssue> leftIssues) = TypeOfExpr(env, left, caps);
        issues.AddRange(leftIssues);
        (CedarType? rightType, _, List<ValidationIssue> rightIssues) = TypeOfExpr(env, right, caps);
        issues.AddRange(rightIssues);

        if (issues.Count == 0)
        {
            if (left is NodeVariable leftVar && right is NodeVariable rightVar && leftVar.Name == rightVar.Name)
            {
                return (negated ? CedarFalseType.Instance : CedarTrueType.Instance, caps, []);
            }

            if (EvalLiteralEquality(left, right) is bool literalResult)
            {
                bool result = negated ? !literalResult : literalResult;
                return (result ? CedarTrueType.Instance : CedarFalseType.Instance, caps, []);
            }

            if (leftType is not null && rightType is not null && AreTypesDisjoint(leftType, rightType))
            {
                return (negated ? CedarTrueType.Instance : CedarFalseType.Instance, caps, []);
            }
        }

        if (_validator.IsStrict && leftType is not null && rightType is not null && !AreTypesDisjoint(leftType, rightType))
        {
            (_, string? error) = CedarTypeOps.LeastUpperBound(leftType, rightType, _validator.IsStrict);
            if (error is not null)
            {
                issues.Add(new TypeIncompatIssue(CedarTypeOps.TypeIncompatErr(leftType, rightType)));
            }
        }

        return (CedarBoolType.Instance, caps, issues);
    }

    private delegate ValidationIssue? TypeExpectation(CedarType type);

    private static ValidationIssue? ExpectComparable(CedarType type)
    {
        if (type is CedarLongType)
        {
            return null;
        }

        if (type is CedarExtType extensionType && (extensionType.Name.Value == "datetime" || extensionType.Name.Value == "duration"))
        {
            return null;
        }

        return UnexpectedType("datetime, or duration, or Long", type);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfComparison(
        RequestEnvironment env,
        INode left,
        INode right,
        CapabilitySet caps,
        TypeExpectation expectLeft,
        TypeExpectation expectRight)
    {
        List<ValidationIssue> issues = [];
        (CedarType? leftType, _, List<ValidationIssue> leftIssues) = TypeOfExpr(env, left, caps);
        (CedarType? rightType, _, List<ValidationIssue> rightIssues) = TypeOfExpr(env, right, caps);
        issues.AddRange(leftIssues);
        issues.AddRange(rightIssues);
        if (leftType is not null && expectLeft(leftType) is ValidationIssue leftExpectation)
        {
            issues.Add(leftExpectation);
        }

        if (rightType is not null && expectRight(rightType) is ValidationIssue rightExpectation)
        {
            issues.Add(rightExpectation);
        }

        return (CedarBoolType.Instance, caps, issues);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfArith(RequestEnvironment env, INode left, INode right, CapabilitySet caps)
    {
        List<ValidationIssue> issues = [];
        (CedarType? leftType, _, List<ValidationIssue> leftIssues) = TypeOfExpr(env, left, caps);
        (CedarType? rightType, _, List<ValidationIssue> rightIssues) = TypeOfExpr(env, right, caps);
        issues.AddRange(leftIssues);
        issues.AddRange(rightIssues);
        if (leftIssues.Count == 0 && leftType is not CedarLongType)
        {
            issues.Add(UnexpectedType("Long", leftType ?? CedarNeverType.Instance));
        }

        if (rightIssues.Count == 0 && rightType is not CedarLongType)
        {
            issues.Add(UnexpectedType("Long", rightType ?? CedarNeverType.Instance));
        }

        return (CedarLongType.Instance, caps, issues);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfNegate(RequestEnvironment env, NodeNegate node, CapabilitySet caps)
    {
        (CedarType? type, _, List<ValidationIssue> issues) = TypeOfExpr(env, node.Arg, caps);
        if (type is not null && type is not CedarLongType)
        {
            issues.Add(UnexpectedType("Long", type));
        }

        return (CedarLongType.Instance, caps, issues);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfIn(RequestEnvironment env, NodeIn node, CapabilitySet caps)
    {
        List<ValidationIssue> issues = [];
        (CedarType? leftType, _, List<ValidationIssue> leftIssues) = TypeOfExpr(env, node.Left, caps);
        (CedarType? rightType, _, List<ValidationIssue> rightIssues) = TypeOfExpr(env, node.Right, caps);
        issues.AddRange(leftIssues);
        issues.AddRange(rightIssues);

        if (leftIssues.Count == 0 && (leftType is null || !IsEntityType(leftType)))
        {
            issues.Add(UnexpectedType("__cedar::internal::AnyEntity", leftType ?? CedarNeverType.Instance));
        }

        if (rightIssues.Count == 0 && (rightType is null || !IsEntityOrSetOfEntity(rightType)))
        {
            issues.Add(UnexpectedType("Set<__cedar::internal::AnyEntity>, or __cedar::internal::AnyEntity", rightType ?? CedarNeverType.Instance));
        }

        if (issues.Count > 0)
        {
            return (CedarBoolType.Instance, caps, issues);
        }

        EntityUid? leftAction = ExprToActionUid(env, node.Left);
        if (leftAction is not null)
        {
            EntityUid[]? rightActions = ExprToActionUids(env, node.Right);
            if (rightActions is not null)
            {
                EntityUid[] schemaActions = rightActions.Where(uid => _validator.Schema.Actions.ContainsKey(uid)).ToArray();
                if (schemaActions.Length > 0)
                {
                    return (IsActionInSet(leftAction, schemaActions) ? CedarTrueType.Instance : CedarFalseType.Instance, caps, []);
                }

                return (CedarFalseType.Instance, caps, []);
            }
        }

        if (leftType is CedarEntityType leftEntity)
        {
            EntityLub? rightLub = rightType switch
            {
                CedarEntityType rightEntity => rightEntity.Lub,
                CedarSetType { Element: CedarEntityType setEntity } => setEntity.Lub,
                _ => null
            };

            if (rightLub is not null && !CedarTypeOps.AnyEntityDescendantOf(leftEntity.Lub, rightLub, _validator.Schema))
            {
                return (CedarFalseType.Instance, caps, []);
            }
        }

        return (CedarBoolType.Instance, caps, []);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfContains(RequestEnvironment env, NodeContains node, CapabilitySet caps)
    {
        List<ValidationIssue> issues = [];
        (CedarType? leftType, _, List<ValidationIssue> leftIssues) = TypeOfExpr(env, node.Left, caps);
        (CedarType? rightType, _, List<ValidationIssue> rightIssues) = TypeOfExpr(env, node.Right, caps);
        issues.AddRange(leftIssues);
        issues.AddRange(rightIssues);
        if (issues.Count > 0)
        {
            return (CedarBoolType.Instance, caps, issues);
        }

        if (leftType is not CedarSetType setType)
        {
            return (null, caps, [UnexpectedType("Set<__cedar::internal::Any>", leftType ?? CedarNeverType.Instance)]);
        }

        if (_validator.IsStrict && setType.Element is not CedarNeverType && rightType is not null)
        {
            if (CedarTypeOps.CheckStrictEntityLUB(setType.Element, rightType) is not null)
            {
                return (null, caps, [new TypeIncompatIssue(CedarTypeOps.TypeIncompatErr(setType.Element, rightType))]);
            }

            (_, string? error) = CedarTypeOps.LeastUpperBound(setType.Element, rightType, _validator.IsStrict);
            if (error is not null)
            {
                return (null, caps, [new TypeIncompatIssue(CedarTypeOps.TypeIncompatErr(setType.Element, rightType))]);
            }
        }

        return (CedarBoolType.Instance, caps, []);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfContainsAllAny(
        RequestEnvironment env,
        INode left,
        INode right,
        CapabilitySet caps)
    {
        List<ValidationIssue> issues = [];
        (CedarType? leftType, _, List<ValidationIssue> leftIssues) = TypeOfExpr(env, left, caps);
        (CedarType? rightType, _, List<ValidationIssue> rightIssues) = TypeOfExpr(env, right, caps);
        issues.AddRange(leftIssues);
        issues.AddRange(rightIssues);

        CedarSetType? leftSet = leftType as CedarSetType;
        CedarSetType? rightSet = rightType as CedarSetType;
        if (issues.Count > 0)
        {
            if (_validator.IsStrict && leftSet is not null && rightSet is not null)
            {
                (_, string? error) = CedarTypeOps.LeastUpperBound(leftSet.Element, rightSet.Element, _validator.IsStrict);
                if (error is not null)
                {
                    issues.Add(new TypeIncompatIssue(CedarTypeOps.TypeIncompatErr(leftType!, rightType!)));
                }
            }

            return (CedarBoolType.Instance, caps, issues);
        }

        if (leftSet is null)
        {
            return (null, caps, [UnexpectedType("Set<__cedar::internal::Any>", leftType ?? CedarNeverType.Instance)]);
        }

        if (rightSet is null)
        {
            return (null, caps, [UnexpectedType("Set<__cedar::internal::Any>", rightType ?? CedarNeverType.Instance)]);
        }

        if (_validator.IsStrict)
        {
            (_, string? error) = CedarTypeOps.LeastUpperBound(leftSet.Element, rightSet.Element, _validator.IsStrict);
            if (error is not null)
            {
                return (null, caps, [new TypeIncompatIssue(CedarTypeOps.TypeIncompatErr(leftType!, rightType!))]);
            }
        }

        return (CedarBoolType.Instance, caps, []);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfIsEmpty(RequestEnvironment env, NodeIsEmpty node, CapabilitySet caps)
    {
        (CedarType? type, _, List<ValidationIssue> issues) = TypeOfExpr(env, node.Arg, caps);
        if (issues.Count > 0)
        {
            if (type is not null && type is not CedarSetType)
            {
                issues.Add(UnexpectedType("Set<__cedar::internal::Any>", type));
            }

            return (type is not null ? CedarBoolType.Instance : null, caps, issues);
        }

        return type is CedarSetType
            ? (CedarBoolType.Instance, caps, [])
            : (null, caps, [UnexpectedType("Set<__cedar::internal::Any>", type ?? CedarNeverType.Instance)]);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfLike(RequestEnvironment env, NodeLike node, CapabilitySet caps)
    {
        (CedarType? type, _, List<ValidationIssue> issues) = TypeOfExpr(env, node.Arg, caps);
        if (issues.Count > 0)
        {
            if (type is not null && type is not CedarStringType)
            {
                issues.Add(UnexpectedType("String", type));
            }

            return (CedarBoolType.Instance, caps, issues);
        }

        return type is CedarStringType
            ? (CedarBoolType.Instance, caps, [])
            : (null, caps, [UnexpectedType("String", type ?? CedarNeverType.Instance)]);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfIs(RequestEnvironment env, NodeIs node, CapabilitySet caps)
    {
        (CedarType? type, _, List<ValidationIssue> issues) = TypeOfExpr(env, node.Left, caps);
        if (issues.Count > 0)
        {
            if (type is not null && !IsEntityType(type))
            {
                issues.Add(UnexpectedType("__cedar::internal::AnyEntity", type));
            }

            return (CedarBoolType.Instance, caps, issues);
        }

        if (type is not CedarEntityType entityType)
        {
            return (null, caps, [UnexpectedType("__cedar::internal::AnyEntity", type ?? CedarNeverType.Instance)]);
        }

        EntityType expected = ScopeValidator.CedarPathToEntityType(node.EntityType);
        if (!entityType.Lub.Elements.Contains(expected))
        {
            return (CedarFalseType.Instance, caps, []);
        }

        return entityType.Lub.Elements.Length == 1 && entityType.Lub.Elements[0] == expected
            ? (CedarTrueType.Instance, caps, [])
            : (CedarBoolType.Instance, caps, []);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfIsIn(RequestEnvironment env, NodeIsIn node, CapabilitySet caps)
    {
        List<ValidationIssue> issues = [];
        (CedarType? leftType, _, List<ValidationIssue> leftIssues) = TypeOfExpr(env, node.Left, caps);
        (CedarType? rightType, _, List<ValidationIssue> rightIssues) = TypeOfExpr(env, node.Entity, caps);
        issues.AddRange(leftIssues);
        issues.AddRange(rightIssues);

        if (leftType is not null && !IsEntityType(leftType))
        {
            issues.Add(UnexpectedType("__cedar::internal::AnyEntity", leftType));
        }

        if (rightType is not null && !IsEntityOrSetOfEntity(rightType))
        {
            issues.Add(UnexpectedType("Set<__cedar::internal::AnyEntity>, or __cedar::internal::AnyEntity", rightType));
        }

        return (CedarBoolType.Instance, caps, issues);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfHas(RequestEnvironment env, NodeHas node, CapabilitySet caps)
    {
        (CedarType? type, _, List<ValidationIssue> issues) = TypeOfExpr(env, node.Arg, caps);
        if (issues.Count > 0)
        {
            return (null, caps, issues);
        }

        if (type is null || !IsEntityOrRecordType(type))
        {
            return (null, caps, [UnexpectedType("__cedar::internal::AnyEntity, or __cedar::internal::OpenRecord{}", type ?? CedarNeverType.Instance)]);
        }

        CedarType resultType = HasResultType(type, node.Attribute.Value);
        CapabilitySet newCaps = caps;
        string? varName = ExprVarName(node.Arg);
        if (varName is not null)
        {
            if (resultType is CedarBoolType && caps.Has(new Capability(varName, node.Attribute.Value)))
            {
                resultType = CedarTrueType.Instance;
            }

            newCaps = caps.Add(new Capability(varName, node.Attribute.Value));
        }

        return (resultType, newCaps, []);
    }

    private CedarType HasResultType(CedarType type, string attr)
    {
        if (type is CedarRecordType recordType)
        {
            if (!recordType.Attrs.TryGetValue(attr, out CedarAttr attribute))
            {
                return CedarFalseType.Instance;
            }

            return attribute.Required ? CedarTrueType.Instance : CedarBoolType.Instance;
        }

        return HasResultTypeEntity(((CedarEntityType)type).Lub, attr);
    }

    private CedarType HasResultTypeEntity(EntityLub lub, string attr)
    {
        bool anyHas = false;
        foreach (EntityType entityType in lub.Elements)
        {
            if (_validator.Schema.Entities.TryGetValue(entityType, out ResolvedEntity? entity)
                && entity.Shape.Attributes.ContainsKey(attr))
            {
                anyHas = true;
            }
        }

        return anyHas ? CedarBoolType.Instance : CedarFalseType.Instance;
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfAccess(RequestEnvironment env, NodeAccess node, CapabilitySet caps)
    {
        (CedarType? sourceType, _, List<ValidationIssue> sourceIssues) = TypeOfExpr(env, node.Arg, caps);
        if (sourceIssues.Count > 0 && sourceType is null)
        {
            return (null, caps, sourceIssues);
        }

        if (sourceType is null || !IsEntityOrRecordType(sourceType))
        {
            List<ValidationIssue> issues = [.. sourceIssues];
            issues.Add(UnexpectedType("__cedar::internal::AnyEntity, or __cedar::internal::OpenRecord{}", sourceType ?? CedarNeverType.Instance));
            return (null, caps, issues);
        }

        // Investigation note: the parser and Cedar JSON serializer only construct
        // NodeAccess.Attribute as NodeValue(CedarStringType), but AccessNode and
        // evaluators still allow arbitrary expressions. Treat non-literal access
        // as a diagnostic case instead of assuming it is impossible.
        if (!TryGetLiteralAttributeName(node.Attribute, out string? attr))
        {
            List<ValidationIssue> issues = [.. sourceIssues];
            (CedarType? attrType, _, List<ValidationIssue> attrIssues) = TypeOfExpr(env, node.Attribute, caps);
            issues.AddRange(attrIssues);
            if (attrType is not null && attrType is not CedarStringType)
            {
                issues.Add(UnexpectedType("String", attrType));
            }

            issues.Add(new ValidationIssue("attribute access requires a string literal attribute name"));
            return (CedarNeverType.Instance, caps, issues);
        }

        List<ValidationIssue> accessIssues = [.. sourceIssues];
        CedarAttr? attrTypeInfo = CedarTypeOps.LookupAttributeType(sourceType, attr!, _validator.Schema, _validator.IsStrict);
        if (attrTypeInfo is null)
        {
            accessIssues.Add(AttrNotFoundError(env, sourceType, attr!));
            return (null, caps, accessIssues);
        }

        if (!attrTypeInfo.Value.Required)
        {
            string? varName = ExprVarName(node.Arg);
            if (varName is null || !caps.Has(new Capability(varName, attr!)))
            {
                accessIssues.Add(UnsafeOptionalAccessError(env, sourceType, attr!, varName));
            }
        }

        CedarType result = attrTypeInfo.Value.Type;
        if (sourceType is CedarEntityType entitySource && result is CedarRecordType recordResult)
        {
            result = recordResult with { Source = new EntityAttrSource(entitySource.Lub, attr!) };
        }

        return (result, caps, accessIssues);
    }

    private ValidationIssue AttrNotFoundError(RequestEnvironment env, CedarType type, string attr)
    {
        if (type is CedarRecordType recordType)
        {
            if (recordType.Source is not null)
            {
                string fullPath = FormatEntityAttrPath(recordType.Source.Attr, attr);
                if (recordType.Source.Lub.Elements.Length == 1)
                {
                    return new ValidationIssue($"attribute `{fullPath}` on entity type `{recordType.Source.Lub.Elements[0]}` not found");
                }

                return new ValidationIssue($"attribute `{fullPath}` on entity types {JoinComma(recordType.Source.Lub.Elements.Select(static et => $"`{et}`"))} not found");
            }

            return new ValidationIssue($"attribute `{attr}` in context for {env.ActionUid} not found");
        }

        CedarEntityType entityType = (CedarEntityType)type;
        if (entityType.Lub.Elements.Length == 1)
        {
            return new ValidationIssue($"attribute `{attr}` on entity type `{entityType.Lub.Elements[0]}` not found");
        }

        return new ValidationIssue($"attribute `{attr}` on entity types {JoinComma(entityType.Lub.Elements.Select(static et => $"`{et}`"))} not found");
    }

    private ValidationIssue UnsafeOptionalAccessError(RequestEnvironment env, CedarType type, string attr, string? varName)
    {
        if (type is CedarRecordType)
        {
            string fullPath = attr;
            if (!string.IsNullOrEmpty(varName)
                && !string.Equals(varName, "context", StringComparison.Ordinal)
                && varName.StartsWith("context.", StringComparison.Ordinal))
            {
                fullPath = varName["context.".Length..] + "." + attr;
            }

            return new ValidationIssue($"unable to guarantee safety of access to optional attribute `{fullPath}` in context for {env.ActionUid}");
        }

        CedarEntityType entityType = (CedarEntityType)type;
        if (entityType.Lub.Elements.Length == 1)
        {
            return new ValidationIssue($"unable to guarantee safety of access to optional attribute `{attr}` on entity type `{entityType.Lub.Elements[0]}`");
        }

        return new ValidationIssue($"unable to guarantee safety of access to optional attribute `{attr}` on entity types {JoinComma(entityType.Lub.Elements.Select(static et => $"`{et}`"))}");
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfHasTag(RequestEnvironment env, NodeHasTag node, CapabilitySet caps)
    {
        List<ValidationIssue> issues = [];
        (CedarType? leftType, _, List<ValidationIssue> leftIssues) = TypeOfExpr(env, node.Left, caps);
        (CedarType? rightType, _, List<ValidationIssue> rightIssues) = TypeOfExpr(env, node.Right, caps);
        issues.AddRange(leftIssues);
        issues.AddRange(rightIssues);

        if (leftIssues.Count == 0 && (leftType is null || !IsEntityType(leftType)))
        {
            issues.Add(UnexpectedType("__cedar::internal::AnyEntity", leftType ?? CedarNeverType.Instance));
        }

        if (rightIssues.Count == 0 && rightType is not CedarStringType)
        {
            issues.Add(UnexpectedType("String", rightType ?? CedarNeverType.Instance));
        }

        if (issues.Count > 0)
        {
            return (CedarBoolType.Instance, caps, issues);
        }

        if (leftType is CedarEntityType entityType && !CedarTypeOps.EntityHasTags(entityType.Lub, _validator.Schema))
        {
            return (CedarFalseType.Instance, caps, []);
        }

        CapabilitySet newCaps = caps;
        string? varName = ExprVarName(node.Left);
        string? tagKey = TagCapabilityKey(node.Right);
        if (varName is not null && tagKey is not null)
        {
            newCaps = caps.Add(new Capability(varName, "__tag:" + tagKey));
        }

        return (CedarBoolType.Instance, newCaps, []);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfGetTag(RequestEnvironment env, NodeGetTag node, CapabilitySet caps)
    {
        List<ValidationIssue> issues = [];
        (CedarType? leftType, _, List<ValidationIssue> leftIssues) = TypeOfExpr(env, node.Left, caps);
        issues.AddRange(leftIssues);
        if (leftType is null)
        {
            return (null, caps, issues);
        }

        if (leftType is not CedarEntityType entityType)
        {
            issues.Add(UnexpectedType("__cedar::internal::AnyEntity", leftType));
            return (CedarStringType.Instance, caps, issues);
        }

        (CedarType? rightType, _, List<ValidationIssue> rightIssues) = TypeOfExpr(env, node.Right, caps);
        issues.AddRange(rightIssues);
        if (rightType is not null && rightType is not CedarStringType)
        {
            issues.Add(UnexpectedType("String", rightType));
        }

        (CedarType tagType, string? tagTypeError) = CedarTypeOps.EntityTagType(entityType.Lub, _validator.Schema, _validator.IsStrict);
        if (tagTypeError is not null)
        {
            issues.Add(new TypeIncompatIssue(tagTypeError));
            return (tagType, caps, issues);
        }

        string? varName = ExprVarName(node.Left);
        string? tagKey = TagCapabilityKey(node.Right);
        bool hasCapability = varName is not null && tagKey is not null && caps.Has(new Capability(varName, "__tag:" + tagKey));
        if (!hasCapability)
        {
            string tagExpr = FormatNodeForMessage(RewriteConstIte(node.Right));
            string entityTypeMessage = entityType.Lub.Elements.Length == 1 ? $" on entity type `{entityType.Lub.Elements[0]}`" : string.Empty;
            issues.Add(new UnsafeTagAccessIssue(
                $"unable to guarantee safety of access to tag `{tagExpr}`{entityTypeMessage}",
                ExprContainsVariable(node.Right, "principal"),
                ExprContainsVariable(node.Right, "resource")));
        }

        return issues.Count > 0 ? (null, caps, issues) : (tagType, caps, []);
    }

    private static INode RewriteConstIte(INode node)
    {
        if (node is not NodeIfThenElse ite)
        {
            return node;
        }

        INode condition = RewriteConstIte(ite.If);
        INode thenBranch = RewriteConstIte(ite.Then);
        INode elseBranch = RewriteConstIte(ite.Else);
        if (condition is NodeValue { Value: CedarBool boolean })
        {
            return boolean.Value
                ? new NodeIfThenElse(condition, thenBranch, thenBranch)
                : new NodeIfThenElse(condition, elseBranch, elseBranch);
        }

        return new NodeIfThenElse(condition, thenBranch, elseBranch);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfRecord(RequestEnvironment env, NodeRecord node, CapabilitySet caps)
    {
        Dictionary<string, CedarAttr> attrs = new(StringComparer.Ordinal);
        List<ValidationIssue> issues = [];
        bool allTyped = true;

        foreach (NodeRecordElement element in node.Elements)
        {
            (CedarType? elemType, _, List<ValidationIssue> elemIssues) = TypeOfExpr(env, element.Value, caps);
            issues.AddRange(elemIssues);
            if (elemType is not null)
            {
                attrs[element.Key.Value] = new CedarAttr(elemType, true);
            }
            else
            {
                allTyped = false;
            }
        }

        if (issues.Count > 0)
        {
            return allTyped ? (new CedarRecordType(attrs), caps, issues) : (null, caps, issues);
        }

        return (new CedarRecordType(attrs), caps, []);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfSet(RequestEnvironment env, NodeSet node, CapabilitySet caps)
    {
        if (_validator.IsStrict && node.Elements.Length == 0)
        {
            return (null, caps, [new ValidationIssue("empty set literals are forbidden in policies")]);
        }

        List<ValidationIssue> issues = [];
        List<CedarType> elementTypes = [];
        foreach (INode element in node.Elements)
        {
            (CedarType? elementType, _, List<ValidationIssue> elementIssues) = TypeOfExpr(env, element, caps);
            issues.AddRange(elementIssues);
            if (elementType is not null)
            {
                elementTypes.Add(elementType);
            }
        }

        if (issues.Count > 0)
        {
            if (elementTypes.Count < node.Elements.Length)
            {
                return (null, caps, issues);
            }

            CedarType elementType = CedarNeverType.Instance;
            foreach (CedarType current in elementTypes)
            {
                if (_validator.IsStrict && CedarTypeOps.CheckStrictEntityLUB(elementType, current) is not null)
                {
                    issues.Add(new TypeIncompatIssue(elementTypes.Count > 2 ? CedarTypeOps.TypeIncompatErrMulti(elementTypes) : CedarTypeOps.TypeIncompatErr(elementType, current)));
                    break;
                }

                (CedarType? lub, string? error) = CedarTypeOps.LeastUpperBound(elementType, current, _validator.IsStrict);
                if (error is not null)
                {
                    issues.Add(new TypeIncompatIssue(elementTypes.Count > 2 ? CedarTypeOps.TypeIncompatErrMulti(elementTypes) : CedarTypeOps.TypeIncompatErr(elementType, current)));
                    break;
                }

                elementType = lub!;
            }

            return (new CedarSetType(elementType), caps, issues);
        }

        CedarType resultType = CedarNeverType.Instance;
        foreach (CedarType current in elementTypes)
        {
            if (_validator.IsStrict && CedarTypeOps.CheckStrictEntityLUB(resultType, current) is not null)
            {
                return (null, caps, [new TypeIncompatIssue(elementTypes.Count > 2 ? CedarTypeOps.TypeIncompatErrMulti(elementTypes) : CedarTypeOps.TypeIncompatErr(resultType, current))]);
            }

            (CedarType? lub, string? error) = CedarTypeOps.LeastUpperBound(resultType, current, _validator.IsStrict);
            if (error is not null)
            {
                return (null, caps, [new TypeIncompatIssue(elementTypes.Count > 2 ? CedarTypeOps.TypeIncompatErrMulti(elementTypes) : CedarTypeOps.TypeIncompatErr(resultType, current))]);
            }

            resultType = lub!;
        }

        return (new CedarSetType(resultType), caps, []);
    }

    private (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfExtensionCall(RequestEnvironment env, NodeExtensionCall node, CapabilitySet caps)
    {
        ExtFuncSig signature = ExtensionFunctions.All[node.Name.Value];
        List<ValidationIssue> issues = [];

        if (node.Args.Length != signature.ArgTypes.Count)
        {
            foreach (INode arg in node.Args)
            {
                (_, _, List<ValidationIssue> argIssues) = TypeOfExpr(env, arg, caps);
                issues.AddRange(argIssues);
            }

            if (signature.IsConstructor && _validator.IsStrict && node.Args.Length > 0 && node.Args[0] is not NodeValue)
            {
                issues.Add(new ValidationIssue("extension constructors may not be called with non-literal expressions"));
            }

            issues.Add(new ValidationIssue($"wrong number of arguments in extension function application. Expected {signature.ArgTypes.Count}, got {node.Args.Length}"));
            return (signature.ReturnType, caps, issues);
        }

        if (signature.IsConstructor && _validator.IsStrict && node.Args.Length == 1 && node.Args[0] is not NodeValue)
        {
            (_, _, List<ValidationIssue> argIssues) = TypeOfExpr(env, node.Args[0], caps);
            issues.AddRange(argIssues);
            issues.Add(new ValidationIssue("extension constructors may not be called with non-literal expressions"));
            return (signature.ReturnType, caps, issues);
        }

        for (int index = 0; index < node.Args.Length; index++)
        {
            (CedarType? argType, _, List<ValidationIssue> argIssues) = TypeOfExpr(env, node.Args[index], caps);
            if (argIssues.Count > 0)
            {
                issues.AddRange(argIssues);
                continue;
            }

            if (argType is not null && !CedarTypeOps.IsSubtype(argType, signature.ArgTypes[index]))
            {
                issues.Add(UnexpectedType(CedarTypeOps.CedarTypeName(signature.ArgTypes[index]), argType));
            }
        }

        if (signature.IsConstructor && node.Args.Length == 1 && node.Args[0] is NodeValue { Value: CedarString stringValue })
        {
            ValidationIssue? validationError = ValidateExtensionValue(node.Name.Value, stringValue.Value);
            if (validationError is not null)
            {
                issues.Add(validationError);
            }
        }

        return (signature.ReturnType, caps, issues);
    }

    private static ValidationIssue? ValidateExtensionValue(string functionName, string value)
    {
        try
        {
            object? parsed = functionName switch
            {
                "ip" => CedarIpAddress.Parse(value),
                "decimal" => CedarDecimal.Parse(value),
                "datetime" => CedarDatetime.Parse(value),
                "duration" => CedarDuration.Parse(value),
                _ => null
            };
            _ = parsed;

            return null;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException or OverflowException)
        {
            return functionName switch
            {
                "ip" => new ValidationIssue($"error during extension function argument validation: Failed to parse as IP address: `{new CedarString(value).MarshalCedar()}`"),
                "decimal" => new ValidationIssue($"error during extension function argument validation: Failed to parse as a decimal value: `{new CedarString(value).MarshalCedar()}`"),
                "datetime" => new ValidationIssue($"error during extension function argument validation: Failed to parse as a datetime value: `{new CedarString(value).MarshalCedar()}`"),
                "duration" => new ValidationIssue($"error during extension function argument validation: Failed to parse as a duration value: `{new CedarString(value).MarshalCedar()}`"),
                _ => null
            };
        }
    }

    private static bool IsBoolType(CedarType type)
    {
        return type is CedarBoolType or CedarTrueType or CedarFalseType;
    }

    private static bool IsEntityType(CedarType type)
    {
        return type is CedarEntityType;
    }

    private static bool IsEntityOrRecordType(CedarType type)
    {
        return type is CedarEntityType or CedarRecordType;
    }

    private static bool IsEntityOrSetOfEntity(CedarType type)
    {
        return type switch
        {
            CedarEntityType => true,
            CedarSetType { Element: CedarNeverType } => true,
            CedarSetType { Element: CedarEntityType } => true,
            _ => false
        };
    }

    private static string? ExprVarName(INode node)
    {
        return node switch
        {
            NodeVariable nodeVariable => nodeVariable.Name.Value,
            NodeAccess access when TryGetLiteralAttributeName(access.Attribute, out string? attr) && ExprVarName(access.Arg) is string parent => parent + "." + attr,
            _ => null
        };
    }

    private static bool ExprContainsVariable(INode node, string target)
    {
        return node switch
        {
            NodeVariable nodeVariable => string.Equals(nodeVariable.Name.Value, target, StringComparison.Ordinal),
            NodeIfThenElse ite => ExprContainsVariable(ite.If, target) || ExprContainsVariable(ite.Then, target) || ExprContainsVariable(ite.Else, target),
            NodeExtensionCall call => call.Args.Any(arg => ExprContainsVariable(arg, target)),
            NodeRecord record => record.Elements.Any(element => ExprContainsVariable(element.Value, target)),
            NodeSet set => set.Elements.Any(element => ExprContainsVariable(element, target)),
            NodeAnd and => ExprContainsVariable(and.Left, target) || ExprContainsVariable(and.Right, target),
            NodeOr or => ExprContainsVariable(or.Left, target) || ExprContainsVariable(or.Right, target),
            NodeEquals equals => ExprContainsVariable(equals.Left, target) || ExprContainsVariable(equals.Right, target),
            NodeNotEquals notEquals => ExprContainsVariable(notEquals.Left, target) || ExprContainsVariable(notEquals.Right, target),
            NodeLessThan lessThan => ExprContainsVariable(lessThan.Left, target) || ExprContainsVariable(lessThan.Right, target),
            NodeLessThanOrEqual lessThanOrEqual => ExprContainsVariable(lessThanOrEqual.Left, target) || ExprContainsVariable(lessThanOrEqual.Right, target),
            NodeGreaterThan greaterThan => ExprContainsVariable(greaterThan.Left, target) || ExprContainsVariable(greaterThan.Right, target),
            NodeGreaterThanOrEqual greaterThanOrEqual => ExprContainsVariable(greaterThanOrEqual.Left, target) || ExprContainsVariable(greaterThanOrEqual.Right, target),
            NodeAdd add => ExprContainsVariable(add.Left, target) || ExprContainsVariable(add.Right, target),
            NodeSub sub => ExprContainsVariable(sub.Left, target) || ExprContainsVariable(sub.Right, target),
            NodeMult mult => ExprContainsVariable(mult.Left, target) || ExprContainsVariable(mult.Right, target),
            NodeIn inNode => ExprContainsVariable(inNode.Left, target) || ExprContainsVariable(inNode.Right, target),
            NodeContains contains => ExprContainsVariable(contains.Left, target) || ExprContainsVariable(contains.Right, target),
            NodeContainsAll containsAll => ExprContainsVariable(containsAll.Left, target) || ExprContainsVariable(containsAll.Right, target),
            NodeContainsAny containsAny => ExprContainsVariable(containsAny.Left, target) || ExprContainsVariable(containsAny.Right, target),
            NodeHasTag hasTag => ExprContainsVariable(hasTag.Left, target) || ExprContainsVariable(hasTag.Right, target),
            NodeGetTag getTag => ExprContainsVariable(getTag.Left, target) || ExprContainsVariable(getTag.Right, target),
            NodeNegate negate => ExprContainsVariable(negate.Arg, target),
            NodeNot not => ExprContainsVariable(not.Arg, target),
            NodeIsEmpty isEmpty => ExprContainsVariable(isEmpty.Arg, target),
            NodeHas has => ExprContainsVariable(has.Arg, target),
            NodeAccess access => ExprContainsVariable(access.Arg, target) || ExprContainsVariable(access.Attribute, target),
            NodeLike like => ExprContainsVariable(like.Arg, target),
            NodeIs isNode => ExprContainsVariable(isNode.Left, target),
            NodeIsIn isIn => ExprContainsVariable(isIn.Left, target) || ExprContainsVariable(isIn.Entity, target),
            _ => false
        };
    }

    private List<ValidationIssue> ValidateEntityRefs(INode node)
    {
        return node switch
        {
            NodeValue { Value: EntityUid uid } => TypeOfEntityUid(uid, CapabilitySet.Create()).Errors,
            NodeIfThenElse ite => [.. ValidateEntityRefs(ite.If), .. ValidateEntityRefs(ite.Then), .. ValidateEntityRefs(ite.Else)],
            NodeExtensionCall call => call.Args.SelectMany(ValidateEntityRefs).ToList(),
            NodeRecord record => record.Elements.SelectMany(element => ValidateEntityRefs(element.Value)).ToList(),
            NodeSet set => set.Elements.SelectMany(ValidateEntityRefs).ToList(),
            NodeAnd and => [.. ValidateEntityRefs(and.Left), .. ValidateEntityRefs(and.Right)],
            NodeOr or => [.. ValidateEntityRefs(or.Left), .. ValidateEntityRefs(or.Right)],
            NodeEquals equals => [.. ValidateEntityRefs(equals.Left), .. ValidateEntityRefs(equals.Right)],
            NodeNotEquals notEquals => [.. ValidateEntityRefs(notEquals.Left), .. ValidateEntityRefs(notEquals.Right)],
            NodeLessThan lessThan => [.. ValidateEntityRefs(lessThan.Left), .. ValidateEntityRefs(lessThan.Right)],
            NodeLessThanOrEqual lessThanOrEqual => [.. ValidateEntityRefs(lessThanOrEqual.Left), .. ValidateEntityRefs(lessThanOrEqual.Right)],
            NodeGreaterThan greaterThan => [.. ValidateEntityRefs(greaterThan.Left), .. ValidateEntityRefs(greaterThan.Right)],
            NodeGreaterThanOrEqual greaterThanOrEqual => [.. ValidateEntityRefs(greaterThanOrEqual.Left), .. ValidateEntityRefs(greaterThanOrEqual.Right)],
            NodeAdd add => [.. ValidateEntityRefs(add.Left), .. ValidateEntityRefs(add.Right)],
            NodeSub sub => [.. ValidateEntityRefs(sub.Left), .. ValidateEntityRefs(sub.Right)],
            NodeMult mult => [.. ValidateEntityRefs(mult.Left), .. ValidateEntityRefs(mult.Right)],
            NodeIn inNode => [.. ValidateEntityRefs(inNode.Left), .. ValidateEntityRefs(inNode.Right)],
            NodeContains contains => [.. ValidateEntityRefs(contains.Left), .. ValidateEntityRefs(contains.Right)],
            NodeContainsAll containsAll => [.. ValidateEntityRefs(containsAll.Left), .. ValidateEntityRefs(containsAll.Right)],
            NodeContainsAny containsAny => [.. ValidateEntityRefs(containsAny.Left), .. ValidateEntityRefs(containsAny.Right)],
            NodeHasTag hasTag => [.. ValidateEntityRefs(hasTag.Left), .. ValidateEntityRefs(hasTag.Right)],
            NodeGetTag getTag => [.. ValidateEntityRefs(getTag.Left), .. ValidateEntityRefs(getTag.Right)],
            NodeNegate negate => ValidateEntityRefs(negate.Arg),
            NodeNot not => ValidateEntityRefs(not.Arg),
            NodeIsEmpty isEmpty => ValidateEntityRefs(isEmpty.Arg),
            NodeHas has => ValidateEntityRefs(has.Arg),
            NodeAccess access => [.. ValidateEntityRefs(access.Arg), .. ValidateEntityRefs(access.Attribute)],
            NodeLike like => ValidateEntityRefs(like.Arg),
            NodeIs isNode => ValidateEntityRefs(isNode.Left),
            NodeIsIn isIn => [.. ValidateEntityRefs(isIn.Left), .. ValidateEntityRefs(isIn.Entity)],
            _ => []
        };
    }

    private static bool? EvalLiteralEquality(INode left, INode right)
    {
        return left is NodeValue leftValue && right is NodeValue rightValue
            ? object.Equals(leftValue.Value, rightValue.Value)
            : null;
    }

    private EntityUid? ExprToActionUid(RequestEnvironment env, INode node)
    {
        if (node is NodeVariable nodeVariable && nodeVariable.Name.Value == "action")
        {
            return env.ActionUid;
        }

        if (node is NodeValue { Value: EntityUid uid } && _validator.Schema.Actions.ContainsKey(uid))
        {
            return uid;
        }

        return null;
    }

    private EntityUid[]? ExprToActionUids(RequestEnvironment env, INode node)
    {
        EntityUid? single = ExprToActionUid(env, node);
        if (single is not null)
        {
            return [single];
        }

        if (node is NodeSet set)
        {
            List<EntityUid> uids = [];
            foreach (INode element in set.Elements)
            {
                EntityUid? uid = ExprToActionUid(env, element);
                if (uid is not null)
                {
                    uids.Add(uid);
                    continue;
                }

                if (element is NodeValue { Value: EntityUid literalUid })
                {
                    uids.Add(literalUid);
                    continue;
                }

                return null;
            }

            return uids.ToArray();
        }

        return null;
    }

    private bool IsActionInSet(EntityUid action, IEnumerable<EntityUid> targets)
    {
        return ScopeValidator.GetActionsInSet(targets, _validator).Contains(action);
    }

    private static string? TagCapabilityKey(INode node)
    {
        return node is NodeValue { Value: CedarString value } ? value.Value : null;
    }

    private static bool AreTypesDisjoint(CedarType left, CedarType right)
    {
        return left is CedarEntityType leftEntity
               && right is CedarEntityType rightEntity
               && leftEntity.Lub.IsDisjoint(rightEntity.Lub);
    }

    private static (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfLiteralSet(CedarSet set, CapabilitySet caps)
    {
        CedarType elementType = CedarNeverType.Instance;
        List<CedarType> elementTypes = [];
        foreach (ICedarData element in set)
        {
            CedarType currentType = LiteralTypeOfValue(element);
            elementTypes.Add(currentType);

            (CedarType? lub, string? error) = CedarTypeOps.LeastUpperBound(elementType, currentType, strict: false);
            if (error is not null)
            {
                return (null, caps, [new TypeIncompatIssue(
                    elementTypes.Count > 2
                        ? CedarTypeOps.TypeIncompatErrMulti(elementTypes)
                        : CedarTypeOps.TypeIncompatErr(elementType, currentType))]);
            }

            elementType = lub!;
        }

        return (new CedarSetType(elementType), caps, []);
    }

    private static (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) TypeOfLiteralRecord(CedarRecord record, CapabilitySet caps)
    {
        Dictionary<string, CedarAttr> attrs = new(StringComparer.Ordinal);
        foreach (KeyValuePair<CedarString, ICedarData> entry in record)
        {
            attrs[entry.Key.Value] = new CedarAttr(LiteralTypeOfValue(entry.Value), true);
        }

        return (new CedarRecordType(attrs), caps, []);
    }

    private static CedarType LiteralTypeOfValue(ICedarData value)
    {
        return value switch
        {
            CedarBool boolean => boolean.Value ? CedarTrueType.Instance : CedarFalseType.Instance,
            CedarLong => CedarLongType.Instance,
            CedarString => CedarStringType.Instance,
            EntityUid uid => new CedarEntityType(EntityLub.Single(uid.Type)),
            CedarIpAddress => new CedarExtType(new Ident("ipaddr")),
            CedarDecimal => new CedarExtType(new Ident("decimal")),
            CedarDatetime => new CedarExtType(new Ident("datetime")),
            CedarDuration => new CedarExtType(new Ident("duration")),
            _ => CedarNeverType.Instance
        };
    }

    private static bool TryGetLiteralAttributeName(INode node, out string? attribute)
    {
        if (node is NodeValue { Value: CedarString value })
        {
            attribute = value.Value;
            return true;
        }

        attribute = null;
        return false;
    }

    private static string FormatEntityAttrPath(string parent, string child)
    {
        string parentText = IsValidCedarIdent(parent)
            ? parent
            : "[" + new CedarString(parent).MarshalCedar() + "]";
        string childText = IsValidCedarIdent(child)
            ? "." + child
            : "[" + new CedarString(child).MarshalCedar() + "]";
        return parentText + childText;
    }

    private static bool IsValidCedarIdent(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (index == 0 && !IsIdentStart(character))
            {
                return false;
            }

            if (index > 0 && !IsIdentContinue(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentStart(char character)
    {
        return char.IsAsciiLetter(character) || character == '_';
    }

    private static bool IsIdentContinue(char character)
    {
        return IsIdentStart(character) || char.IsAsciiDigit(character);
    }

    private static string JoinComma(IEnumerable<string> parts)
    {
        return string.Join(", ", parts);
    }

    private static string FormatNodeForMessage(INode node)
    {
        return node switch
        {
            NodeValue { Value: CedarString value } => value.Value,
            NodeValue { Value: CedarLong value } => value.Value.ToString(CultureInfo.InvariantCulture),
            NodeValue { Value: CedarBool value } => value.Value ? "true" : "false",
            NodeVariable variable => variable.Name.Value,
            NodeAccess access when TryGetLiteralAttributeName(access.Attribute, out string? attr) => FormatNodeForMessage(access.Arg) + "." + attr,
            NodeIfThenElse ite => $"if {FormatNodeForMessage(ite.If)} then {FormatNodeForMessage(ite.Then)} else {FormatNodeForMessage(ite.Else)}",
            _ => node.ToString() ?? "<expr>"
        };
    }
}

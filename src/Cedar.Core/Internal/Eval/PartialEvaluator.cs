using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Core.Internal.Eval.Evaluators;
using Cedar.Types;

namespace Cedar.Core.Internal.Eval;

internal static class PartialEvaluator
{
    private const string VariableEntityTypeName = "__cedar::variable";
    private const string IgnoreEntityTypeName = "__cedar::ignore";
    public const string PartialErrorExtensionName = "__cedar::partialError";

    private static readonly EntityType VariableEntityType = new(VariableEntityTypeName);
    private static readonly EntityType IgnoreEntityType = new(IgnoreEntityTypeName);
    private static readonly EvalEnv EmptyEnv = new(
        new EntityMap(),
        new EntityUid(new EntityType("__cedar::empty"), new CedarString("principal")),
        new EntityUid(new EntityType("__cedar::empty"), new CedarString("action")),
        new EntityUid(new EntityType("__cedar::empty"), new CedarString("resource")),
        new CedarRecord());

    public static EntityUid Variable(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return Variable(new CedarString(name));
    }

    public static EntityUid Variable(CedarString name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new EntityUid(VariableEntityType, name);
    }

    public static EntityUid Ignore()
    {
        return new EntityUid(IgnoreEntityType, new CedarString(string.Empty));
    }

    public static bool IsVariable(ICedarData value)
    {
        return value is EntityUid entityUid && entityUid.Type == VariableEntityType;
    }

    public static bool TryGetVariableName(ICedarData value, out CedarString name)
    {
        if (value is EntityUid entityUid && entityUid.Type == VariableEntityType)
        {
            name = entityUid.Id;
            return true;
        }

        name = default!;
        return false;
    }

    public static bool IsIgnore(ICedarData value)
    {
        return value is EntityUid entityUid && entityUid.Type == IgnoreEntityType;
    }

    public static PolicyAst? PartialPolicy(EvalEnv env, PolicyAst policy, out bool keep, Effect ignoreBias = Effect.Permit)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(policy);

        if (!TryPartialScope(env, env.Principal, policy.PrincipalScope, out IScope principalScope))
        {
            keep = false;
            return null;
        }

        if (!TryPartialScope(env, env.Action, policy.ActionScope, out IScope actionScope))
        {
            keep = false;
            return null;
        }

        if (!TryPartialScope(env, env.Resource, policy.ResourceScope, out IScope resourceScope))
        {
            keep = false;
            return null;
        }

        ImmutableArray<INode>.Builder conditions = ImmutableArray.CreateBuilder<INode>(policy.Conditions.Length);
        foreach (INode condition in policy.Conditions)
        {
            try
            {
                INode body = Partial(env, condition);
                if (body is NodeValue nodeValue)
                {
                    if (nodeValue.Value is CedarBool boolean)
                    {
                        if (!boolean.Value)
                        {
                            keep = false;
                            return null;
                        }

                        continue;
                    }

                    conditions.Add(PartialError("condition expected bool"));
                    keep = true;
                    return CreatePolicy(policy, principalScope, actionScope, resourceScope, conditions);
                }

                conditions.Add(body);
            }
            catch (PartialSentinelException exception) when (exception.Sentinel == PartialSentinel.Variable)
            {
                conditions.Add(condition);
            }
            catch (PartialSentinelException exception) when (exception.Sentinel == PartialSentinel.Ignore)
            {
                if (policy.Effect == ignoreBias)
                {
                    continue;
                }

                keep = false;
                return null;
            }
            catch (EvalException exception)
            {
                conditions.Add(PartialError(exception.Message));
                keep = true;
                return CreatePolicy(policy, principalScope, actionScope, resourceScope, conditions);
            }
        }

        keep = true;
        return CreatePolicy(policy, principalScope, actionScope, resourceScope, conditions);
    }

    public static INode PolicyToNode(PolicyAst policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return ScopeCompiler.CompilePolicy(policy);
    }

    public static NodeExtensionCall PartialError(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        return new NodeExtensionCall(new CedarPath(PartialErrorExtensionName), ImmutableArray.Create<INode>(new NodeValue(new CedarString(message))));
    }

    public static bool TryGetPartialError(INode node, out string message)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node is not NodeExtensionCall extensionCall || extensionCall.Name.Value != PartialErrorExtensionName || extensionCall.Args.Length != 1)
        {
            message = string.Empty;
            return false;
        }

        try
        {
            ICedarData value = Compiler.ToEval(extensionCall.Args[0]).Eval(EmptyEnv);
            message = TypeConversion.ValueToString(value);
            return true;
        }
        catch (EvalException)
        {
            message = string.Empty;
            return false;
        }
    }

    private static PolicyAst CreatePolicy(
        PolicyAst policy,
        IScope principalScope,
        IScope actionScope,
        IScope resourceScope,
        ImmutableArray<INode>.Builder conditions)
    {
        return policy with
        {
            PrincipalScope = principalScope,
            ActionScope = actionScope,
            ResourceScope = resourceScope,
            Conditions = conditions.ToImmutable()
        };
    }

    private static bool TryPartialScope(EvalEnv env, ICedarData value, IScope scope, out IScope partialScope)
    {
        (bool evaluated, bool result) = TryEvaluateScope(env, value, scope);
        if (!evaluated)
        {
            partialScope = scope;
            return true;
        }

        if (result)
        {
            partialScope = new ScopeAll();
            return true;
        }

        partialScope = scope;
        return false;
    }

    private static (bool Evaluated, bool Result) TryEvaluateScope(EvalEnv env, ICedarData value, IScope scope)
    {
        if (IsVariable(value))
        {
            return (false, false);
        }

        if (IsIgnore(value))
        {
            return (true, true);
        }

        if (value is not EntityUid entity)
        {
            return (false, false);
        }

        return scope switch
        {
            ScopeAll => (true, true),
            ScopeEq equals => (true, entity.Equals(equals.Entity)),
            ScopeIn contains => (true, InOperator.Contains(env, entity, contains.Entity)),
            ScopeInSet set => (true, EntityInSet(env.Entities, entity, set.Entities)),
            ScopeIs isScope => (true, string.Equals(entity.Type.Value, isScope.Type.Value, System.StringComparison.Ordinal)),
            ScopeIsIn isIn => (true, string.Equals(entity.Type.Value, isIn.Type.Value, System.StringComparison.Ordinal) && InOperator.Contains(env, entity, isIn.Entity)),
            _ => throw new EvalException($"unsupported scope type `{scope.GetType().Name}`")
        };
    }

    private static bool EntityInSet(IEntityGetter entities, EntityUid entity, IEnumerable<EntityUid> candidates)
    {
        EvalEnv env = new(entities, entity, entity, entity, new CedarRecord());

        foreach (EntityUid candidate in candidates)
        {
            if (InOperator.Contains(env, entity, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static INode Partial(EvalEnv env, INode node)
    {
        return node switch
        {
            NodeAccess access => TryPartialBinary(
                env,
                access.Arg,
                access.Attribute,
                static (left, right) => new AttributeAccessEvaluator(left, right),
                static (left, right) => new NodeAccess(left, right)),
            NodeHas has => TryPartial(
                env,
                [has.Arg],
                values => new PartialHasEvaluator(new LiteralEvaluator(values[0]), has.Attribute),
                nodes => new NodeHas(nodes[0], has.Attribute)),
            NodeGetTag getTag => TryPartialBinary(
                env,
                getTag.Left,
                getTag.Right,
                static (left, right) => throw new EvalException("fold.GetTag"),
                static (left, right) => new NodeGetTag(left, right)),
            NodeHasTag hasTag => TryPartialBinary(
                env,
                hasTag.Left,
                hasTag.Right,
                static (left, right) => throw new EvalException("fold.HasTag"),
                static (left, right) => new NodeHasTag(left, right)),
            NodeLike like => TryPartial(
                env,
                [like.Arg],
                values => new LikeEvaluator(new LiteralEvaluator(values[0]), like.Pattern),
                nodes => new NodeLike(nodes[0], like.Pattern)),
            NodeIfThenElse conditional => PartialIfThenElse(env, conditional),
            NodeIs isNode => TryPartial(
                env,
                [isNode.Left],
                values => new IsEvaluator(new LiteralEvaluator(values[0]), isNode.EntityType),
                nodes => new NodeIs(nodes[0], isNode.EntityType)),
            NodeIsIn isIn => TryPartial(
                env,
                [isIn.Left, isIn.Entity],
                values => new IsInEvaluator(new LiteralEvaluator(values[0]), isIn.EntityType, new LiteralEvaluator(values[1])),
                nodes => new NodeIsIn(nodes[0], isIn.EntityType, nodes[1])),
            NodeExtensionCall call => TryPartial(
                env,
                [.. call.Args],
                values => new ExtensionEvaluator(call.Name.Value, ToLiteralEvaluators(values)),
                nodes => new NodeExtensionCall(call.Name, [.. nodes])),
            NodeValue => node,
            NodeRecord record => PartialRecord(env, record),
            NodeSet set => TryPartial(
                env,
                [.. set.Elements],
                values => new SetLiteralEvaluator(ToLiteralEvaluators(values)),
                nodes => new NodeSet([.. nodes])),
            NodeNegate negate => TryPartial(
                env,
                [negate.Arg],
                static values => new NegateEvaluator(new LiteralEvaluator(values[0])),
                nodes => new NodeNegate(nodes[0])),
            NodeNot not => TryPartial(
                env,
                [not.Arg],
                static values => new NotEvaluator(new LiteralEvaluator(values[0])),
                nodes => new NodeNot(nodes[0])),
            NodeVariable variable => TryPartial(
                env,
                [],
                _ => new VariableEvaluator(variable.Name),
                _ => new NodeVariable(variable.Name)),
            NodeIn contains => TryPartialBinary(
                env,
                contains.Left,
                contains.Right,
                static (left, right) => new InEvaluator(left, right),
                static (left, right) => new NodeIn(left, right)),
            NodeAnd andNode => PartialAnd(env, andNode),
            NodeOr orNode => PartialOr(env, orNode),
            NodeEquals equals => TryPartialBinary(
                env,
                equals.Left,
                equals.Right,
                static (left, right) => new EqualEvaluator(left, right),
                static (left, right) => new NodeEquals(left, right)),
            NodeNotEquals notEquals => TryPartialBinary(
                env,
                notEquals.Left,
                notEquals.Right,
                static (left, right) => new NotEqualEvaluator(left, right),
                static (left, right) => new NodeNotEquals(left, right)),
            NodeGreaterThan greaterThan => TryPartialBinary(
                env,
                greaterThan.Left,
                greaterThan.Right,
                static (left, right) => new GreaterThanEvaluator(left, right),
                static (left, right) => new NodeGreaterThan(left, right)),
            NodeGreaterThanOrEqual greaterThanOrEqual => TryPartialBinary(
                env,
                greaterThanOrEqual.Left,
                greaterThanOrEqual.Right,
                static (left, right) => new GreaterThanOrEqualEvaluator(left, right),
                static (left, right) => new NodeGreaterThanOrEqual(left, right)),
            NodeLessThan lessThan => TryPartialBinary(
                env,
                lessThan.Left,
                lessThan.Right,
                static (left, right) => new LessThanEvaluator(left, right),
                static (left, right) => new NodeLessThan(left, right)),
            NodeLessThanOrEqual lessThanOrEqual => TryPartialBinary(
                env,
                lessThanOrEqual.Left,
                lessThanOrEqual.Right,
                static (left, right) => new LessThanOrEqualEvaluator(left, right),
                static (left, right) => new NodeLessThanOrEqual(left, right)),
            NodeSub sub => TryPartialBinary(
                env,
                sub.Left,
                sub.Right,
                static (left, right) => new SubEvaluator(left, right),
                static (left, right) => new NodeSub(left, right)),
            NodeAdd add => TryPartialBinary(
                env,
                add.Left,
                add.Right,
                static (left, right) => new AddEvaluator(left, right),
                static (left, right) => new NodeAdd(left, right)),
            NodeMult mult => TryPartialBinary(
                env,
                mult.Left,
                mult.Right,
                static (left, right) => new MultEvaluator(left, right),
                static (left, right) => new NodeMult(left, right)),
            NodeContains contains => TryPartialBinary(
                env,
                contains.Left,
                contains.Right,
                static (left, right) => new ContainsEvaluator(left, right),
                static (left, right) => new NodeContains(left, right)),
            NodeContainsAll containsAll => TryPartialBinary(
                env,
                containsAll.Left,
                containsAll.Right,
                static (left, right) => new ContainsAllEvaluator(left, right),
                static (left, right) => new NodeContainsAll(left, right)),
            NodeContainsAny containsAny => TryPartialBinary(
                env,
                containsAny.Left,
                containsAny.Right,
                static (left, right) => new ContainsAnyEvaluator(left, right),
                static (left, right) => new NodeContainsAny(left, right)),
            NodeIsEmpty isEmpty => TryPartial(
                env,
                [isEmpty.Arg],
                static values => new IsEmptyEvaluator(new LiteralEvaluator(values[0])),
                nodes => new NodeIsEmpty(nodes[0])),
            _ => throw new EvalException($"unknown node type `{node.GetType().Name}`")
        };
    }

    private static INode PartialRecord(EvalEnv env, NodeRecord record)
    {
        INode[] values = new INode[record.Elements.Length];
        for (int index = 0; index < record.Elements.Length; index++)
        {
            values[index] = record.Elements[index].Value;
        }

        return TryPartial(
            env,
            values,
            compiledValues =>
            {
                KeyValuePair<CedarString, IEvaluator>[] evaluators = new KeyValuePair<CedarString, IEvaluator>[compiledValues.Length];
                for (int index = 0; index < compiledValues.Length; index++)
                {
                    evaluators[index] = new KeyValuePair<CedarString, IEvaluator>(
                        record.Elements[index].Key,
                        new LiteralEvaluator(compiledValues[index]));
                }

                return new RecordLiteralEvaluator(evaluators);
            },
            nodes =>
            {
                ImmutableArray<NodeRecordElement>.Builder builder = ImmutableArray.CreateBuilder<NodeRecordElement>(nodes.Length);
                for (int index = 0; index < nodes.Length; index++)
                {
                    builder.Add(new NodeRecordElement(record.Elements[index].Key, nodes[index]));
                }

                return new NodeRecord(builder.ToImmutable());
            });
    }

    private static INode TryPartialBinary(
        EvalEnv env,
        INode leftNode,
        INode rightNode,
        Func<IEvaluator, IEvaluator, IEvaluator> createEvaluator,
        Func<INode, INode, INode> createNode)
    {
        return TryPartial(
            env,
            [leftNode, rightNode],
            values => createEvaluator(new LiteralEvaluator(values[0]), new LiteralEvaluator(values[1])),
            nodes => createNode(nodes[0], nodes[1]));
    }

    private static INode TryPartial(
        EvalEnv env,
        INode[] nodes,
        Func<ICedarData[], IEvaluator> createEvaluator,
        Func<INode[], INode> createNode)
    {
        List<ICedarData> values = [];
        bool fullyEvaluable = true;

        for (int index = 0; index < nodes.Length; index++)
        {
            try
            {
                INode partialNode = Partial(env, nodes[index]);
                nodes[index] = partialNode;
                if (!fullyEvaluable)
                {
                    continue;
                }

                if (partialNode is NodeValue nodeValue)
                {
                    values.Add(nodeValue.Value);
                }
                else
                {
                    fullyEvaluable = false;
                }
            }
            catch (PartialSentinelException exception) when (exception.Sentinel == PartialSentinel.Variable)
            {
                fullyEvaluable = false;
            }
        }

        if (!fullyEvaluable)
        {
            return createNode(nodes);
        }

        ICedarData value = createEvaluator(values.ToArray()).Eval(env);
        if (IsVariable(value))
        {
            throw new PartialSentinelException(PartialSentinel.Variable);
        }

        if (IsIgnore(value))
        {
            throw new PartialSentinelException(PartialSentinel.Ignore);
        }

        return new NodeValue(value);
    }

    private static IEvaluator[] ToLiteralEvaluators(ICedarData[] values)
    {
        IEvaluator[] evaluators = new IEvaluator[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            evaluators[index] = new LiteralEvaluator(values[index]);
        }

        return evaluators;
    }

    private static INode PartialIfThenElse(EvalEnv env, NodeIfThenElse conditional)
    {
        INode ifNode;
        try
        {
            ifNode = Partial(env, conditional.If);
            if (IsNonBooleanValue(ifNode))
            {
                throw new EvalException("ifThenElse expected bool");
            }

            if (IsTrue(ifNode))
            {
                return Partial(env, conditional.Then);
            }

            if (IsFalse(ifNode))
            {
                return Partial(env, conditional.Else);
            }
        }
        catch (PartialSentinelException exception) when (exception.Sentinel == PartialSentinel.Variable)
        {
            ifNode = conditional.If;
        }

        INode thenNode = PartialBranch(env, conditional.Then);
        INode elseNode = PartialBranch(env, conditional.Else);
        return new NodeIfThenElse(ifNode, thenNode, elseNode);
    }

    private static INode PartialAnd(EvalEnv env, NodeAnd andNode)
    {
        INode leftNode;
        try
        {
            leftNode = Partial(env, andNode.Left);
            if (IsNonBooleanValue(leftNode))
            {
                throw new EvalException("and expected bool");
            }

            if (IsFalse(leftNode))
            {
                return new NodeValue(CedarBool.False);
            }

            if (IsTrue(leftNode))
            {
                return TryPartialBinary(
                    env,
                    new NodeValue(CedarBool.True),
                    andNode.Right,
                    static (left, right) => new AndEvaluator(left, right),
                    static (left, right) => new NodeAnd(left, right));
            }
        }
        catch (PartialSentinelException exception) when (exception.Sentinel == PartialSentinel.Variable)
        {
            leftNode = andNode.Left;
        }

        INode rightNode = PartialBranch(env, andNode.Right);
        return new NodeAnd(leftNode, rightNode);
    }

    private static INode PartialOr(EvalEnv env, NodeOr orNode)
    {
        INode leftNode;
        try
        {
            leftNode = Partial(env, orNode.Left);
            if (IsNonBooleanValue(leftNode))
            {
                throw new EvalException("or expected bool");
            }

            if (IsTrue(leftNode))
            {
                return new NodeValue(CedarBool.True);
            }

            if (IsFalse(leftNode))
            {
                return TryPartialBinary(
                    env,
                    new NodeValue(CedarBool.False),
                    orNode.Right,
                    static (left, right) => new OrEvaluator(left, right),
                    static (left, right) => new NodeOr(left, right));
            }
        }
        catch (PartialSentinelException exception) when (exception.Sentinel == PartialSentinel.Variable)
        {
            leftNode = orNode.Left;
        }

        INode rightNode = PartialBranch(env, orNode.Right);
        return new NodeOr(leftNode, rightNode);
    }

    private static INode PartialBranch(EvalEnv env, INode node)
    {
        try
        {
            return Partial(env, node);
        }
        catch (PartialSentinelException exception) when (exception.Sentinel == PartialSentinel.Variable)
        {
            return node;
        }
        catch (EvalException exception)
        {
            return PartialError(exception.Message);
        }
    }

    private static bool IsNonBooleanValue(INode node)
    {
        return node is NodeValue { Value: not CedarBool };
    }

    private static bool IsTrue(INode node)
    {
        return node is NodeValue { Value: CedarBool { Value: true } };
    }

    private static bool IsFalse(INode node)
    {
        return node is NodeValue { Value: CedarBool { Value: false } };
    }

    private enum PartialSentinel
    {
        Variable,
        Ignore
    }

    private sealed class PartialSentinelException(PartialSentinel sentinel) : Exception
    {
        public PartialSentinel Sentinel { get; } = sentinel;
    }

    private sealed class PartialHasEvaluator(IEvaluator value, CedarString attribute) : IEvaluator
    {
        public ICedarData Eval(EvalEnv env)
        {
            ICedarData source = value.Eval(env);
            return source switch
            {
                CedarRecord record => EvaluateRecord(record),
                EntityUid entityUid => EvaluateEntity(env.Entities, entityUid),
                _ => throw new EvalException($"expected record or entity, got {EvalErrors.TypeName(source)}")
            };
        }

        private ICedarData EvaluateRecord(CedarRecord record)
        {
            if (!record.TryGetValue(attribute, out ICedarData recordValue))
            {
                return CedarBool.False;
            }

            if (IsIgnore(recordValue))
            {
                throw new PartialSentinelException(PartialSentinel.Ignore);
            }

            return CedarBool.True;
        }

        private ICedarData EvaluateEntity(IEntityGetter entities, EntityUid entityUid)
        {
            if (!entities.TryGet(entityUid, out Entity entity) || !entity.Attributes.TryGetValue(attribute, out ICedarData value))
            {
                return CedarBool.False;
            }

            if (IsIgnore(value))
            {
                throw new PartialSentinelException(PartialSentinel.Ignore);
            }

            return CedarBool.True;
        }
    }
}

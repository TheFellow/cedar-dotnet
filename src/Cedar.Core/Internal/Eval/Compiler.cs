using System.Collections.Generic;
using Cedar.Ast.Internal;
using Cedar.Core.Internal.Eval.Evaluators;
using Cedar.Types;

namespace Cedar.Core.Internal.Eval;

internal static class Compiler
{
    public static BoolEvaluator Compile(PolicyAst policy)
    {
        PolicyAst folded = ConstantFolder.FoldPolicy(policy);
        return new BoolEvaluator(ToEval(ScopeCompiler.CompilePolicy(folded)));
    }

    public static IEvaluator ToEval(INode node)
    {
        return node switch
        {
            NodeAccess access => new AttributeAccessEvaluator(ToEval(access.Arg), access.Attribute),
            NodeHas has => new HasEvaluator(ToEval(has.Arg), has.Attribute),
            NodeGetTag getTag => new GetTagEvaluator(ToEval(getTag.Left), ToEval(getTag.Right)),
            NodeHasTag hasTag => new HasTagEvaluator(ToEval(hasTag.Left), ToEval(hasTag.Right)),
            NodeLike like => new LikeEvaluator(ToEval(like.Arg), like.Pattern),
            NodeIfThenElse conditional => new ConditionalEvaluator(ToEval(conditional.If), ToEval(conditional.Then), ToEval(conditional.Else)),
            NodeIs isNode => new IsEvaluator(ToEval(isNode.Left), isNode.EntityType),
            NodeIsIn isIn => new IsInEvaluator(ToEval(isIn.Left), isIn.EntityType, ToEval(isIn.Entity)),
            NodeExtensionCall call => new ExtensionEvaluator(call.Name, CompileArgs(call.Args)),
            NodeValue value => new LiteralEvaluator(value.Value),
            NodeRecord record => new RecordLiteralEvaluator(CompileRecordElements(record.Elements)),
            NodeSet set => new SetLiteralEvaluator(CompileElements(set.Elements)),
            NodeNegate negate => new NegateEvaluator(ToEval(negate.Arg)),
            NodeNot not => new NotEvaluator(ToEval(not.Arg)),
            NodeVariable variable => new VariableEvaluator(variable.Name),
            NodeIn contains => new InEvaluator(ToEval(contains.Left), ToEval(contains.Right)),
            NodeAnd andNode => new AndEvaluator(ToEval(andNode.Left), ToEval(andNode.Right)),
            NodeOr orNode => new OrEvaluator(ToEval(orNode.Left), ToEval(orNode.Right)),
            NodeEquals equals => new EqualEvaluator(ToEval(equals.Left), ToEval(equals.Right)),
            NodeNotEquals notEquals => new NotEqualEvaluator(ToEval(notEquals.Left), ToEval(notEquals.Right)),
            NodeGreaterThan greaterThan => new GreaterThanEvaluator(ToEval(greaterThan.Left), ToEval(greaterThan.Right)),
            NodeGreaterThanOrEqual greaterThanOrEqual => new GreaterThanOrEqualEvaluator(ToEval(greaterThanOrEqual.Left), ToEval(greaterThanOrEqual.Right)),
            NodeLessThan lessThan => new LessThanEvaluator(ToEval(lessThan.Left), ToEval(lessThan.Right)),
            NodeLessThanOrEqual lessThanOrEqual => new LessThanOrEqualEvaluator(ToEval(lessThanOrEqual.Left), ToEval(lessThanOrEqual.Right)),
            NodeSub sub => new SubEvaluator(ToEval(sub.Left), ToEval(sub.Right)),
            NodeAdd add => new AddEvaluator(ToEval(add.Left), ToEval(add.Right)),
            NodeMult mult => new MultEvaluator(ToEval(mult.Left), ToEval(mult.Right)),
            NodeContains contains => new ContainsEvaluator(ToEval(contains.Left), ToEval(contains.Right)),
            NodeContainsAll containsAll => new ContainsAllEvaluator(ToEval(containsAll.Left), ToEval(containsAll.Right)),
            NodeContainsAny containsAny => new ContainsAnyEvaluator(ToEval(containsAny.Left), ToEval(containsAny.Right)),
            NodeIsEmpty isEmpty => new IsEmptyEvaluator(ToEval(isEmpty.Arg)),
            _ => throw new EvalException($"unknown node type `{node.GetType().Name}`")
        };
    }

    private static IEvaluator[] CompileArgs(System.Collections.Immutable.ImmutableArray<INode> args)
    {
        IEvaluator[] evaluators = new IEvaluator[args.Length];
        for (int index = 0; index < args.Length; index++)
        {
            evaluators[index] = ToEval(args[index]);
        }

        return evaluators;
    }

    private static KeyValuePair<CedarString, IEvaluator>[] CompileRecordElements(System.Collections.Immutable.ImmutableArray<NodeRecordElement> elements)
    {
        KeyValuePair<CedarString, IEvaluator>[] evaluators = new KeyValuePair<CedarString, IEvaluator>[elements.Length];
        for (int index = 0; index < elements.Length; index++)
        {
            NodeRecordElement element = elements[index];
            evaluators[index] = new KeyValuePair<CedarString, IEvaluator>(element.Key, ToEval(element.Value));
        }

        return evaluators;
    }

    private static IEvaluator[] CompileElements(System.Collections.Immutable.ImmutableArray<INode> elements)
    {
        IEvaluator[] evaluators = new IEvaluator[elements.Length];
        for (int index = 0; index < elements.Length; index++)
        {
            evaluators[index] = ToEval(elements[index]);
        }

        return evaluators;
    }
}

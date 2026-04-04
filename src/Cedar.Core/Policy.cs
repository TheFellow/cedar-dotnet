using System;
using System.Threading;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Core.Internal.Eval;
using Cedar.Core.Internal.Json;
using Cedar.Core.Internal.Parser;

namespace Cedar.Core;

public sealed class Policy
{
    private BoolEvaluator? _compiledEvaluator;

    internal Policy(PolicyAst ast)
    {
        ArgumentNullException.ThrowIfNull(ast);
        Ast = ast;
    }

    public Effect Effect => Ast.Effect;

    public Annotations Annotations => new(Ast.Annotations);

    public Position Position => Ast.Position;

    internal PolicyAst Ast { get; }

    internal BoolEvaluator CompiledEvaluator
    {
        get
        {
            BoolEvaluator? evaluator = Volatile.Read(ref _compiledEvaluator);
            if (evaluator is not null)
            {
                return evaluator;
            }

            evaluator = Compiler.Compile(Ast);
            Volatile.Write(ref _compiledEvaluator, evaluator);
            return evaluator;
        }
    }

    public static Policy UnmarshalCedar(string cedarText)
    {
        ArgumentException.ThrowIfNullOrEmpty(cedarText);

        PolicyAst[] astPolicies = CedarParser.ParsePolicies(cedarText);
        if (astPolicies.Length != 1)
        {
            throw new ArgumentException($"Expected exactly one policy, got {astPolicies.Length}.", nameof(cedarText));
        }

        return new Policy(astPolicies[0]);
    }

    public static Policy[] UnmarshalCedarList(string cedarText)
    {
        ArgumentException.ThrowIfNullOrEmpty(cedarText);

        PolicyAst[] astPolicies = CedarParser.ParsePolicies(cedarText);
        Policy[] policies = new Policy[astPolicies.Length];
        for (int i = 0; i < astPolicies.Length; i++)
        {
            policies[i] = new Policy(astPolicies[i]);
        }

        return policies;
    }

    public string MarshalCedar()
    {
        return CedarWriter.Write(Ast);
    }

    public static Policy UnmarshalJson(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);

        return new Policy(PolicyJsonUnmarshal.Unmarshal(json));
    }

    public static Policy FromAst(PolicyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return new Policy(builder.Ast);
    }

    public string MarshalJson()
    {
        return PolicyJsonMarshal.Marshal(Ast);
    }
}

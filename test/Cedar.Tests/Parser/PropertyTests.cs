using System;
using System.Text;
using Cedar.Ast.Internal;
using Cedar.Core.Internal.Parser;
using FsCheck.Xunit;
using Xunit;

namespace Cedar.Tests.Parser;

public sealed class PropertyTests
{
    [Property(MaxTest = 1000)]
    public void ParseWriteParse_PreservesAst(int seedA, int seedB, int seedC, bool useForbidEffect, bool includeUnless)
    {
        string source = BuildPolicy(seedA, seedB, seedC, useForbidEffect, includeUnless);

        PolicyAst[] first = CedarParser.ParsePolicies(source);
        string written = CedarWriter.Write(first);

        PolicyAst[] second = CedarParser.ParsePolicies(written);
        string rewritten = CedarWriter.Write(second);

        Assert.Equal(first.Length, second.Length);
        Assert.Equal(written, rewritten);
    }

    private static string BuildPolicy(int seedA, int seedB, int seedC, bool useForbidEffect, bool includeUnless)
    {
        string effect = useForbidEffect ? "forbid" : "permit";
        string principalScope = Pick(seedA, "principal", "principal == User::\"alice\"", "principal in Team::\"eng\"");
        string actionScope = Pick(seedB, "action", "action == Action::\"read\"", "action in [Action::\"read\", Action::\"write\"]");
        string resourceScope = Pick(seedC, "resource", "resource == Document::\"doc1\"", "resource is Document", "resource is Document in Folder::\"fin\"");

        int conditionCount = 1 + (Positive(seedA + seedB + seedC) % 3);

        StringBuilder builder = new();
        builder.Append(effect);
        builder.Append('(');
        builder.Append(principalScope);
        builder.Append(", ");
        builder.Append(actionScope);
        builder.Append(", ");
        builder.Append(resourceScope);
        builder.Append(')');

        for (int index = 0; index < conditionCount; index++)
        {
            int localSeed = Mix(seedA, seedB, seedC, index);
            string keyword = includeUnless && index == conditionCount - 1 ? "unless" : "when";
            string expression = BuildExpression(localSeed, 3);
            builder.Append('\n');
            builder.Append("  ");
            builder.Append(keyword);
            builder.Append(" { ");
            builder.Append(expression);
            builder.Append(" }");
        }

        builder.Append(';');
        return builder.ToString();
    }

    private static string BuildExpression(int seed, int depth)
    {
        if (depth <= 0)
        {
            return Pick(seed,
                "true",
                "false",
                "1",
                "-1",
                "\"text\"",
                "principal",
                "action",
                "resource",
                "context",
                "User::\"alice\"",
                "decimal(\"1.23\")",
                "ip(\"1.2.3.4\")",
                "datetime(\"2024-01-01T00:00:00Z\")",
                "duration(\"1d\")");
        }

        int branch = Positive(seed) % 9;
        return branch switch
        {
            0 => $"!({BuildExpression(seed + 13, depth - 1)})",
            1 => $"-({BuildExpression(seed + 29, depth - 1)})",
            2 => $"({BuildExpression(seed + 3, depth - 1)} {Pick(seed, "+", "-", "*", "==", "!=", "<", "<=", ">", ">=", "&&", "||", "in")} {BuildExpression(seed + 7, depth - 1)})",
            3 => $"if {BuildExpression(seed + 11, depth - 1)} then {BuildExpression(seed + 17, depth - 1)} else {BuildExpression(seed + 23, depth - 1)}",
            4 => $"[{BuildExpression(seed + 31, depth - 1)}, {BuildExpression(seed + 37, depth - 1)}]",
            5 => $"{{x: {BuildExpression(seed + 41, depth - 1)}, y: {BuildExpression(seed + 43, depth - 1)}}}",
            6 => $"[{BuildExpression(seed + 47, depth - 1)}, {BuildExpression(seed + 53, depth - 1)}].contains({BuildExpression(seed + 59, depth - 1)})",
            7 => $"ip(\"1.2.3.4\").isInRange(ip(\"1.2.3.0/24\"))",
            _ => $"decimal(\"1.0\").lessThan(decimal(\"2.0\"))"
        };
    }

    private static int Mix(int first, int second, int third, int index)
    {
        unchecked
        {
            return (first * 397) ^ (second * 37) ^ (third * 11) ^ index;
        }
    }

    private static string Pick(int seed, params string[] values)
    {
        return values[Positive(seed) % values.Length];
    }

    private static int Positive(int value)
    {
        return value == int.MinValue ? int.MaxValue : Math.Abs(value);
    }
}

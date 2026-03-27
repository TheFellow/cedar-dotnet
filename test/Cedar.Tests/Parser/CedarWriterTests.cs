using System.Collections.Immutable;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Core.Internal.Parser;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Parser;

public sealed class CedarWriterTests
{
    [Fact]
    public void WriteSimplePolicy()
    {
        PolicyAst policy = new(
            Effect.Permit,
            new ScopeAll(),
            new ScopeAll(),
            new ScopeAll(),
            ImmutableArray<INode>.Empty,
            ImmutableArray<Annotation>.Empty,
            new Position(string.Empty, 0, 1, 1));

        Assert.Equal("permit(principal, action, resource);", CedarWriter.Write(policy));
    }

    [Fact]
    public void WritePolicyWithAnnotations()
    {
        PolicyAst policy = new(
            Effect.Permit,
            new ScopeAll(),
            new ScopeAll(),
            new ScopeAll(),
            ImmutableArray<INode>.Empty,
            ImmutableArray.Create(new Annotation(new Ident("env"), new CedarString("prod"))),
            new Position(string.Empty, 0, 1, 1));

        Assert.Equal("@env(\"prod\")\npermit(principal, action, resource);", CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteScopeVariants()
    {
        PolicyAst eq = ParseSingle("permit(principal == User::\"a\", action, resource);");
        PolicyAst @in = ParseSingle("permit(principal, action in Action::\"read\", resource);");
        PolicyAst inSet = ParseSingle("permit(principal, action in [Action::\"read\", Action::\"write\"], resource);");
        PolicyAst isScope = ParseSingle("permit(principal, action, resource is Doc);");
        PolicyAst isIn = ParseSingle("permit(principal, action, resource is Doc in Folder::\"x\");");

        Assert.Equal("permit(principal == User::\"a\", action, resource);", CedarWriter.Write(eq));
        Assert.Equal("permit(principal, action in Action::\"read\", resource);", CedarWriter.Write(@in));
        Assert.Equal("permit(principal, action in [Action::\"read\", Action::\"write\"], resource);", CedarWriter.Write(inSet));
        Assert.Equal("permit(principal, action, resource is Doc);", CedarWriter.Write(isScope));
        Assert.Equal("permit(principal, action, resource is Doc in Folder::\"x\");", CedarWriter.Write(isIn));
    }

    [Fact]
    public void WriteConditionsUseTwoSpaceIndentation()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { true } unless { false };");

        Assert.Equal(
            "permit(principal, action, resource)\n  when { true }\n  unless { false };",
            CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteParenthesizesWhenNeededForSubtraction()
    {
        INode expression = new NodeSub(new NodeValue(new CedarLong(1)), new NodeSub(new NodeValue(new CedarLong(2)), new NodeValue(new CedarLong(3))));
        PolicyAst policy = BuildPolicy(expression);

        Assert.Equal(
            "permit(principal, action, resource)\n  when { 1 - (2 - 3) };",
            CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteEntityUidExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { User::\"alice\" };");

        Assert.Equal(
            "permit(principal, action, resource)\n  when { User::\"alice\" };",
            CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteAccessWithQuotedAttribute()
    {
        INode expression = new NodeAccess(new NodeVariable(new CedarString("context")), new CedarString("not valid"));
        PolicyAst policy = BuildPolicy(expression);

        Assert.Equal(
            "permit(principal, action, resource)\n  when { context[\"not valid\"] };",
            CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteHasWithQuotedAttribute()
    {
        INode expression = new NodeHas(new NodeVariable(new CedarString("context")), new CedarString("if"));
        PolicyAst policy = BuildPolicy(expression);

        Assert.Equal(
            "permit(principal, action, resource)\n  when { context has \"if\" };",
            CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteAccessWithReservedKeyword()
    {
        INode expression = new NodeAccess(new NodeVariable(new CedarString("context")), new CedarString("true"));
        PolicyAst policy = BuildPolicy(expression);

        Assert.Equal(
            "permit(principal, action, resource)\n  when { context[\"true\"] };",
            CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteAccessWithEmptyString()
    {
        INode expression = new NodeAccess(new NodeVariable(new CedarString("context")), new CedarString(string.Empty));
        PolicyAst policy = BuildPolicy(expression);

        Assert.Equal(
            "permit(principal, action, resource)\n  when { context[\"\"] };",
            CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteMultiplePolicies()
    {
        PolicyAst first = ParseSingle("permit(principal, action, resource);");
        PolicyAst second = ParseSingle("forbid(principal, action, resource);");

        Assert.Equal(
            "permit(principal, action, resource);\n\nforbid(principal, action, resource);",
            CedarWriter.Write([first, second]));
    }

    [Fact]
    public void WriteExtensionCall()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { f(1, true) };");

        Assert.Equal(
            "permit(principal, action, resource)\n  when { f(1, true) };",
            CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteParenthesizesRightOperandOfOrForAssociativity()
    {
        INode expression = new NodeOr(
            new NodeValue(new CedarBool(true)),
            new NodeOr(
                new NodeValue(new CedarBool(false)),
                new NodeValue(new CedarBool(true))));
        PolicyAst policy = BuildPolicy(expression);

        Assert.Equal(
            "permit(principal, action, resource)\n  when { true || (false || true) };",
            CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteParenthesizesRightOperandOfAndForAssociativity()
    {
        INode expression = new NodeAnd(
            new NodeValue(new CedarBool(true)),
            new NodeAnd(
                new NodeValue(new CedarBool(false)),
                new NodeValue(new CedarBool(true))));
        PolicyAst policy = BuildPolicy(expression);

        Assert.Equal(
            "permit(principal, action, resource)\n  when { true && (false && true) };",
            CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteParenthesizesRightOperandOfMultForAssociativity()
    {
        INode expression = new NodeMult(
            new NodeValue(new CedarLong(1)),
            new NodeMult(
                new NodeValue(new CedarLong(2)),
                new NodeValue(new CedarLong(3))));
        PolicyAst policy = BuildPolicy(expression);

        Assert.Equal(
            "permit(principal, action, resource)\n  when { 1 * (2 * 3) };",
            CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteParenthesizesRightOperandOfAddForAssociativity()
    {
        INode expression = new NodeAdd(
            new NodeValue(new CedarLong(1)),
            new NodeAdd(
                new NodeValue(new CedarLong(2)),
                new NodeValue(new CedarLong(3))));
        PolicyAst policy = BuildPolicy(expression);

        Assert.Equal(
            "permit(principal, action, resource)\n  when { 1 + (2 + 3) };",
            CedarWriter.Write(policy));
    }

    [Fact]
    public void WriteAddWithSubOnRightParenthesizes()
    {
        INode expression = new NodeAdd(
            new NodeValue(new CedarLong(1)),
            new NodeSub(
                new NodeValue(new CedarLong(2)),
                new NodeValue(new CedarLong(3))));
        PolicyAst policy = BuildPolicy(expression);

        Assert.Equal(
            "permit(principal, action, resource)\n  when { 1 + (2 - 3) };",
            CedarWriter.Write(policy));
    }

    private static PolicyAst ParseSingle(string source)
    {
        PolicyAst[] policies = CedarParser.ParsePolicies(source);
        return Assert.Single(policies);
    }

    private static PolicyAst BuildPolicy(INode expression)
    {
        return new PolicyAst(
            Effect.Permit,
            new ScopeAll(),
            new ScopeAll(),
            new ScopeAll(),
            ImmutableArray.Create(expression),
            ImmutableArray<Annotation>.Empty,
            new Position(string.Empty, 0, 1, 1));
    }
}

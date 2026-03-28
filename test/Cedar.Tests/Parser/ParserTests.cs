using System;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Core.Internal.Parser;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Parser;

public sealed class ParserTests
{
    [Fact]
    public void ParsePermitWithUnconstrainedScopes()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource);");

        Assert.Equal(Effect.Permit, policy.Effect);
        Assert.IsType<ScopeAll>(policy.PrincipalScope);
        Assert.IsType<ScopeAll>(policy.ActionScope);
        Assert.IsType<ScopeAll>(policy.ResourceScope);
        Assert.Empty(policy.Conditions);
    }

    [Fact]
    public void ParseForbidPolicy()
    {
        PolicyAst policy = ParseSingle("forbid(principal, action, resource);");

        Assert.Equal(Effect.Forbid, policy.Effect);
    }

    [Fact]
    public void ParsePrincipalEqScope()
    {
        PolicyAst policy = ParseSingle("permit(principal == User::\"alice\", action, resource);");

        ScopeEq scope = Assert.IsType<ScopeEq>(policy.PrincipalScope);
        Assert.Equal(new EntityType("User"), scope.Entity.Type);
        Assert.Equal(new CedarString("alice"), scope.Entity.Id);
    }

    [Fact]
    public void ParseActionInScope()
    {
        PolicyAst policy = ParseSingle("permit(principal, action in Action::\"read\", resource);");

        ScopeIn scope = Assert.IsType<ScopeIn>(policy.ActionScope);
        Assert.Equal("Action", scope.Entity.Type.Value);
        Assert.Equal("read", scope.Entity.Id.Value);
    }

    [Fact]
    public void ParseActionInSetScope()
    {
        PolicyAst policy = ParseSingle("permit(principal, action in [Action::\"read\", Action::\"write\"], resource);");

        ScopeInSet scope = Assert.IsType<ScopeInSet>(policy.ActionScope);
        Assert.Equal(2, scope.Entities.Length);
        Assert.Equal("read", scope.Entities[0].Id.Value);
        Assert.Equal("write", scope.Entities[1].Id.Value);
    }

    [Fact]
    public void ParseResourceIsScope()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource is Doc::Type);");

        ScopeIs scope = Assert.IsType<ScopeIs>(policy.ResourceScope);
        Assert.Equal("Doc::Type", scope.Type.Value);
    }

    [Fact]
    public void ParseResourceIsInScope()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource is Doc in Folder::\"f1\");");

        ScopeIsIn scope = Assert.IsType<ScopeIsIn>(policy.ResourceScope);
        Assert.Equal("Doc", scope.Type.Value);
        Assert.Equal("Folder", scope.Entity.Type.Value);
        Assert.Equal("f1", scope.Entity.Id.Value);
    }

    [Fact]
    public void ParseWhenCondition()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { true };");

        NodeValue value = Assert.IsType<NodeValue>(Assert.Single(policy.Conditions));
        Assert.Equal(CedarBool.True, value.Value);
    }

    [Fact]
    public void ParseUnlessConditionAsNodeNot()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) unless { principal };");

        NodeNot not = Assert.IsType<NodeNot>(Assert.Single(policy.Conditions));
        NodeVariable variable = Assert.IsType<NodeVariable>(not.Arg);
        Assert.Equal("principal", variable.Name.Value);
    }

    [Fact]
    public void ParseMultipleConditions()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { true } unless { false };");

        Assert.Equal(2, policy.Conditions.Length);
        Assert.IsType<NodeValue>(policy.Conditions[0]);
        Assert.IsType<NodeNot>(policy.Conditions[1]);
    }

    [Fact]
    public void ParseCollapsedAnnotation()
    {
        PolicyAst policy = ParseSingle("@id(\"abc\") permit(principal, action, resource);");

        Annotation annotation = Assert.Single(policy.Annotations);
        Assert.Equal("id", annotation.Key.Value);
        Assert.Equal("abc", annotation.Value.Value);
    }

    [Fact]
    public void ParseInlineAnnotationTokens()
    {
        PolicyAst policy = ParseSingle("@ id ( \"abc\" ) permit(principal, action, resource);");

        Annotation annotation = Assert.Single(policy.Annotations);
        Assert.Equal("id", annotation.Key.Value);
        Assert.Equal("abc", annotation.Value.Value);
    }

    [Fact]
    public void ParseCollapsedAnnotationWithReservedKeywordKey()
    {
        PolicyAst policy = ParseSingle("@is(\"bar\") permit(principal, action, resource);");

        Annotation annotation = Assert.Single(policy.Annotations);
        Assert.Equal("is", annotation.Key.Value);
        Assert.Equal("bar", annotation.Value.Value);
    }

    [Fact]
    public void ParseInlineAnnotationWithReservedKeywordKey()
    {
        PolicyAst policy = ParseSingle("@ if ( \"bar\" ) permit(principal, action, resource);");

        Annotation annotation = Assert.Single(policy.Annotations);
        Assert.Equal("if", annotation.Key.Value);
        Assert.Equal("bar", annotation.Value.Value);
    }

    [Fact]
    public void ParseMultiplePolicies()
    {
        PolicyAst[] policies = CedarParser.ParsePolicies("permit(principal, action, resource); forbid(principal, action, resource);");

        Assert.Equal(2, policies.Length);
        Assert.Equal(Effect.Permit, policies[0].Effect);
        Assert.Equal(Effect.Forbid, policies[1].Effect);
    }

    [Fact]
    public void ParseIfThenElseExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { if true then 1 else 2 };");

        NodeIfThenElse node = Assert.IsType<NodeIfThenElse>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeValue>(node.If);
        Assert.IsType<NodeValue>(node.Then);
        Assert.IsType<NodeValue>(node.Else);
    }

    [Fact]
    public void ParseOperatorPrecedence()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 1 + 2 * 3 };");

        NodeAdd add = Assert.IsType<NodeAdd>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeValue>(add.Left);
        Assert.IsType<NodeMult>(add.Right);
    }

    [Fact]
    public void ParseUnaryAndNegativeLiteral()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { !-1 };");

        NodeNot not = Assert.IsType<NodeNot>(Assert.Single(policy.Conditions));
        NodeValue value = Assert.IsType<NodeValue>(not.Arg);
        Assert.Equal(new CedarLong(-1), value.Value);
    }

    [Fact]
    public void ParseNegateOfVariable()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { -context };");

        NodeNegate negate = Assert.IsType<NodeNegate>(Assert.Single(policy.Conditions));
        NodeVariable variable = Assert.IsType<NodeVariable>(negate.Arg);
        Assert.Equal("context", variable.Name.Value);
    }

    [Fact]
    public void ParseDoubleNegateWithLiteralFolding()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { !--1 };");

        NodeNot not = Assert.IsType<NodeNot>(Assert.Single(policy.Conditions));
        NodeNegate negate = Assert.IsType<NodeNegate>(not.Arg);
        NodeValue value = Assert.IsType<NodeValue>(negate.Arg);
        Assert.Equal(new CedarLong(-1), value.Value);
    }

    [Fact]
    public void ParseNegativeLiteralInMultiplication()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { -2 * 3 == -6 };");

        NodeEquals equals = Assert.IsType<NodeEquals>(Assert.Single(policy.Conditions));
        NodeMult mult = Assert.IsType<NodeMult>(equals.Left);
        NodeValue left = Assert.IsType<NodeValue>(mult.Left);
        NodeValue right = Assert.IsType<NodeValue>(mult.Right);
        NodeValue equalsRight = Assert.IsType<NodeValue>(equals.Right);

        Assert.Equal(new CedarLong(-2), left.Value);
        Assert.Equal(new CedarLong(3), right.Value);
        Assert.Equal(new CedarLong(-6), equalsRight.Value);
    }

    [Fact]
    public void ParseNegateOfGroupedExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { -(2 + 3) == -5 };");

        NodeEquals equals = Assert.IsType<NodeEquals>(Assert.Single(policy.Conditions));
        NodeNegate negate = Assert.IsType<NodeNegate>(equals.Left);
        NodeAdd add = Assert.IsType<NodeAdd>(negate.Arg);
        NodeValue addLeft = Assert.IsType<NodeValue>(add.Left);
        NodeValue addRight = Assert.IsType<NodeValue>(add.Right);
        NodeValue equalsRight = Assert.IsType<NodeValue>(equals.Right);

        Assert.Equal(new CedarLong(2), addLeft.Value);
        Assert.Equal(new CedarLong(3), addRight.Value);
        Assert.Equal(new CedarLong(-5), equalsRight.Value);
    }

    [Fact]
    public void ParseDoubleNot()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { !!true };");

        NodeNot outer = Assert.IsType<NodeNot>(Assert.Single(policy.Conditions));
        NodeNot inner = Assert.IsType<NodeNot>(outer.Arg);
        NodeValue value = Assert.IsType<NodeValue>(inner.Arg);
        Assert.Equal(CedarBool.True, value.Value);
    }

    [Fact]
    public void ParseChainedMultiplication()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 42 * 2 * 1 };");

        NodeMult outer = Assert.IsType<NodeMult>(Assert.Single(policy.Conditions));
        NodeMult inner = Assert.IsType<NodeMult>(outer.Left);
        NodeValue innerLeft = Assert.IsType<NodeValue>(inner.Left);
        NodeValue innerRight = Assert.IsType<NodeValue>(inner.Right);
        NodeValue outerRight = Assert.IsType<NodeValue>(outer.Right);

        Assert.Equal(new CedarLong(42), innerLeft.Value);
        Assert.Equal(new CedarLong(2), innerRight.Value);
        Assert.Equal(new CedarLong(1), outerRight.Value);
    }

    [Fact]
    public void ParseChainedAddition()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 42 + 2 + 1 };");

        NodeAdd outer = Assert.IsType<NodeAdd>(Assert.Single(policy.Conditions));
        NodeAdd inner = Assert.IsType<NodeAdd>(outer.Left);
        NodeValue innerLeft = Assert.IsType<NodeValue>(inner.Left);
        NodeValue innerRight = Assert.IsType<NodeValue>(inner.Right);
        NodeValue outerRight = Assert.IsType<NodeValue>(outer.Right);

        Assert.Equal(new CedarLong(42), innerLeft.Value);
        Assert.Equal(new CedarLong(2), innerRight.Value);
        Assert.Equal(new CedarLong(1), outerRight.Value);
    }

    [Fact]
    public void ParseChainedSubtraction()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 42 - 2 - 1 };");

        NodeSub outer = Assert.IsType<NodeSub>(Assert.Single(policy.Conditions));
        NodeSub inner = Assert.IsType<NodeSub>(outer.Left);
        NodeValue innerLeft = Assert.IsType<NodeValue>(inner.Left);
        NodeValue innerRight = Assert.IsType<NodeValue>(inner.Right);
        NodeValue outerRight = Assert.IsType<NodeValue>(outer.Right);

        Assert.Equal(new CedarLong(42), innerLeft.Value);
        Assert.Equal(new CedarLong(2), innerRight.Value);
        Assert.Equal(new CedarLong(1), outerRight.Value);
    }

    [Fact]
    public void ParseMixedAddAndSubtract()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 42 - 2 + 1 };");

        NodeAdd outer = Assert.IsType<NodeAdd>(Assert.Single(policy.Conditions));
        NodeSub inner = Assert.IsType<NodeSub>(outer.Left);
        NodeValue innerLeft = Assert.IsType<NodeValue>(inner.Left);
        NodeValue innerRight = Assert.IsType<NodeValue>(inner.Right);
        NodeValue outerRight = Assert.IsType<NodeValue>(outer.Right);

        Assert.Equal(new CedarLong(42), innerLeft.Value);
        Assert.Equal(new CedarLong(2), innerRight.Value);
        Assert.Equal(new CedarLong(1), outerRight.Value);
    }

    [Fact]
    public void ParseChainedAnd()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { true && false && true };");

        NodeAnd outer = Assert.IsType<NodeAnd>(Assert.Single(policy.Conditions));
        NodeAnd inner = Assert.IsType<NodeAnd>(outer.Left);
        NodeValue innerLeft = Assert.IsType<NodeValue>(inner.Left);
        NodeValue innerRight = Assert.IsType<NodeValue>(inner.Right);
        NodeValue outerRight = Assert.IsType<NodeValue>(outer.Right);

        Assert.Equal(CedarBool.True, innerLeft.Value);
        Assert.Equal(CedarBool.False, innerRight.Value);
        Assert.Equal(CedarBool.True, outerRight.Value);
    }

    [Fact]
    public void ParseChainedOr()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { true || false || true };");

        NodeOr outer = Assert.IsType<NodeOr>(Assert.Single(policy.Conditions));
        NodeOr inner = Assert.IsType<NodeOr>(outer.Left);
        NodeValue innerLeft = Assert.IsType<NodeValue>(inner.Left);
        NodeValue innerRight = Assert.IsType<NodeValue>(inner.Right);
        NodeValue outerRight = Assert.IsType<NodeValue>(outer.Right);

        Assert.Equal(CedarBool.True, innerLeft.Value);
        Assert.Equal(CedarBool.False, innerRight.Value);
        Assert.Equal(CedarBool.True, outerRight.Value);
    }

    [Fact]
    public void ParseParenthesizedMultiAddThenMultiply()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { (2 + 3 + 4) * 5 == 18 };");

        NodeEquals equals = Assert.IsType<NodeEquals>(Assert.Single(policy.Conditions));
        NodeMult mult = Assert.IsType<NodeMult>(equals.Left);
        NodeAdd outerAdd = Assert.IsType<NodeAdd>(mult.Left);
        NodeAdd innerAdd = Assert.IsType<NodeAdd>(outerAdd.Left);
        NodeValue innerLeft = Assert.IsType<NodeValue>(innerAdd.Left);
        NodeValue innerRight = Assert.IsType<NodeValue>(innerAdd.Right);
        NodeValue outerAddRight = Assert.IsType<NodeValue>(outerAdd.Right);
        NodeValue multRight = Assert.IsType<NodeValue>(mult.Right);
        NodeValue equalsRight = Assert.IsType<NodeValue>(equals.Right);

        Assert.Equal(new CedarLong(2), innerLeft.Value);
        Assert.Equal(new CedarLong(3), innerRight.Value);
        Assert.Equal(new CedarLong(4), outerAddRight.Value);
        Assert.Equal(new CedarLong(5), multRight.Value);
        Assert.Equal(new CedarLong(18), equalsRight.Value);
    }

    [Fact]
    public void ParseEntityReferencePrimary()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { User::\"alice\" };");

        NodeValue value = Assert.IsType<NodeValue>(Assert.Single(policy.Conditions));
        EntityUid uid = Assert.IsType<EntityUid>(value.Value);
        Assert.Equal("User", uid.Type.Value);
        Assert.Equal("alice", uid.Id.Value);
    }

    [Fact]
    public void ParseVariablesAsNodeVariable()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal && action && resource && context };");

        Assert.IsType<NodeAnd>(Assert.Single(policy.Conditions));
        Assert.Contains("principal", CedarWriter.Write(policy), StringComparison.Ordinal);
        Assert.Contains("action", CedarWriter.Write(policy), StringComparison.Ordinal);
        Assert.Contains("resource", CedarWriter.Write(policy), StringComparison.Ordinal);
        Assert.Contains("context", CedarWriter.Write(policy), StringComparison.Ordinal);
    }

    [Fact]
    public void ParseSetLiteral()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { [1, 2, 3] };");

        NodeSet set = Assert.IsType<NodeSet>(Assert.Single(policy.Conditions));
        Assert.Equal(3, set.Elements.Length);
    }

    [Fact]
    public void ParseRecordLiteral()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { {a: 1, \"b\": 2} };");

        NodeRecord record = Assert.IsType<NodeRecord>(Assert.Single(policy.Conditions));
        Assert.Equal(2, record.Elements.Length);
        Assert.Equal("a", record.Elements[0].Key.Value);
        Assert.Equal("b", record.Elements[1].Key.Value);
    }

    [Fact]
    public void ParseMemberAccessAndMethodCall()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { resource.tags.contains(\"blue\") };");

        NodeContains contains = Assert.IsType<NodeContains>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeAccess>(contains.Left);
        Assert.IsType<NodeValue>(contains.Right);
    }

    [Fact]
    public void ParseBracketAccessAsAttributeAccess()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { resource[\"env\"] };");

        NodeAccess access = Assert.IsType<NodeAccess>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeVariable>(access.Arg);
        NodeValue attribute = Assert.IsType<NodeValue>(access.Attribute);
        CedarString value = Assert.IsType<CedarString>(attribute.Value);
        Assert.Equal("env", value.Value);
    }

    [Fact]
    public void ParseExtensionFunctionCall()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { decimal(\"1.0\") };");

        NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Conditions));
        Assert.Equal("decimal", call.Name.Value);
        Assert.Single(call.Args);
    }

    [Fact]
    public void WriteExtensionMethodCall_UsesMethodSyntax()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { context.ip.isIpv4() };");

        string cedar = CedarWriter.Write(policy);

        Assert.Contains("context.ip.isIpv4()", cedar, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteExtensionFunctionCall_UsesFunctionSyntax()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { ip(\"1.2.3.4\") };");

        string cedar = CedarWriter.Write(policy);

        Assert.Contains("ip(\"1.2.3.4\")", cedar, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteRecordLiteral_RoundTrips()
    {
        const string source = "permit(principal, action, resource) when { {a: 1, \"b\": 2} };";

        PolicyAst policy = ParseSingle(source);
        string cedar = CedarWriter.Write(policy);

        Assert.Contains("when { {a: 1, b: 2} }", cedar, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseLikePattern()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { context.name like \"ab*\\*cd\" };");

        NodeLike like = Assert.IsType<NodeLike>(Assert.Single(policy.Conditions));
        Assert.Equal("ab*\\*cd", like.Pattern.ToPatternText());
    }

    [Fact]
    public void ParseExtendedHasChain()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { context has user.name };");

        NodeAnd and = Assert.IsType<NodeAnd>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeHas>(and.Left);
        Assert.IsType<NodeHas>(and.Right);
    }

    [Fact]
    public void ParseExtendedHasThreeLevelChain()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal has a.b.c };");

        NodeAnd outer = Assert.IsType<NodeAnd>(Assert.Single(policy.Conditions));
        NodeAnd inner = Assert.IsType<NodeAnd>(outer.Left);

        NodeHas hasA = Assert.IsType<NodeHas>(inner.Left);
        NodeVariable principal = Assert.IsType<NodeVariable>(hasA.Arg);
        Assert.Equal("principal", principal.Name.Value);
        Assert.Equal("a", hasA.Attribute.Value);

        NodeHas hasB = Assert.IsType<NodeHas>(inner.Right);
        NodeAccess accessA = Assert.IsType<NodeAccess>(hasB.Arg);
        NodeVariable accessAPrincipal = Assert.IsType<NodeVariable>(accessA.Arg);
        NodeValue accessAAttribute = Assert.IsType<NodeValue>(accessA.Attribute);
        CedarString accessAAttributeValue = Assert.IsType<CedarString>(accessAAttribute.Value);
        Assert.Equal("principal", accessAPrincipal.Name.Value);
        Assert.Equal("a", accessAAttributeValue.Value);
        Assert.Equal("b", hasB.Attribute.Value);

        NodeHas hasC = Assert.IsType<NodeHas>(outer.Right);
        NodeAccess accessAB = Assert.IsType<NodeAccess>(hasC.Arg);
        NodeAccess accessABPrefix = Assert.IsType<NodeAccess>(accessAB.Arg);
        NodeVariable accessABPrincipal = Assert.IsType<NodeVariable>(accessABPrefix.Arg);
        NodeValue accessABPrefixAttribute = Assert.IsType<NodeValue>(accessABPrefix.Attribute);
        CedarString accessABPrefixAttributeValue = Assert.IsType<CedarString>(accessABPrefixAttribute.Value);
        NodeValue accessABAttribute = Assert.IsType<NodeValue>(accessAB.Attribute);
        CedarString accessABAttributeValue = Assert.IsType<CedarString>(accessABAttribute.Value);
        Assert.Equal("principal", accessABPrincipal.Name.Value);
        Assert.Equal("a", accessABPrefixAttributeValue.Value);
        Assert.Equal("b", accessABAttributeValue.Value);
        Assert.Equal("c", hasC.Attribute.Value);
    }

    [Fact]
    public void ParseIsInExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal is User in Team::\"eng\" };");

        NodeIsIn node = Assert.IsType<NodeIsIn>(Assert.Single(policy.Conditions));
        Assert.Equal("User", node.EntityType.Value);
        Assert.IsType<NodeValue>(node.Entity);
    }

    [Fact]
    public void ParseTrailingCommaInScopeTuple()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource,);");

        Assert.IsType<ScopeAll>(policy.PrincipalScope);
        Assert.IsType<ScopeAll>(policy.ActionScope);
        Assert.IsType<ScopeAll>(policy.ResourceScope);
    }

    [Fact]
    public void ParseTrailingCommaInSetLiteral()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { [1, 2,].isEmpty() };");

        NodeIsEmpty node = Assert.IsType<NodeIsEmpty>(Assert.Single(policy.Conditions));
        NodeSet set = Assert.IsType<NodeSet>(node.Arg);
        Assert.Equal(2, set.Elements.Length);
        Assert.All(set.Elements, static element => Assert.IsType<NodeValue>(element));
    }

    [Fact]
    public void ParseTrailingCommaInRecordLiteral()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { {\"key\": 1,} has key };");

        NodeHas node = Assert.IsType<NodeHas>(Assert.Single(policy.Conditions));
        NodeRecord record = Assert.IsType<NodeRecord>(node.Arg);
        NodeRecordElement element = Assert.Single(record.Elements);
        Assert.Equal(new CedarString("key"), element.Key);
        Assert.IsType<NodeValue>(element.Value);
        Assert.Equal(new CedarString("key"), node.Attribute);
    }

    [Fact]
    public void ParseTrailingCommaInEntityListExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { User::\"alice\" in [User::\"bob\",] };");

        NodeIn node = Assert.IsType<NodeIn>(Assert.Single(policy.Conditions));
        NodeValue left = Assert.IsType<NodeValue>(node.Left);
        Assert.Equal(new EntityUid(new EntityType("User"), new CedarString("alice")), left.Value);

        NodeSet set = Assert.IsType<NodeSet>(node.Right);
        NodeValue element = Assert.IsType<NodeValue>(Assert.Single(set.Elements));
        Assert.Equal(new EntityUid(new EntityType("User"), new CedarString("bob")), element.Value);
    }

    [Fact]
    public void ParseNestedParenthesizedExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { (((1 + 2))) };");

        Assert.IsType<NodeAdd>(Assert.Single(policy.Conditions));
    }

    private static PolicyAst ParseSingle(string source)
    {
        PolicyAst[] policies = CedarParser.ParsePolicies(source);
        return Assert.Single(policies);
    }
}

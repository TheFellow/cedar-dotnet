using System;
using System.Collections.Generic;
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
    public void ParseCollapsedAnnotationWithSpacesAndReservedKeywordKey()
    {
        PolicyAst policy = ParseSingle("@ if ( \"bar\" ) permit(principal, action, resource);");

        Annotation annotation = Assert.Single(policy.Annotations);
        Assert.Equal("if", annotation.Key.Value);
        Assert.Equal("bar", annotation.Value.Value);
    }

    [Fact]
    public void ParseBareAnnotation()
    {
        PolicyAst policy = ParseSingle("@foo\npermit(principal, action, resource);");

        Annotation annotation = Assert.Single(policy.Annotations);
        Assert.Equal("foo", annotation.Key.Value);
        Assert.Equal(string.Empty, annotation.Value.Value);
    }

    [Fact]
    public void ParseBareReservedKeywordAnnotation()
    {
        PolicyAst policy = ParseSingle("@is\npermit(principal, action, resource);");

        Annotation annotation = Assert.Single(policy.Annotations);
        Assert.Equal("is", annotation.Key.Value);
        Assert.Equal(string.Empty, annotation.Value.Value);
    }

    [Fact]
    public void ParseBareThenValuedAnnotations()
    {
        PolicyAst policy = ParseSingle("@foo\n@baz(\"quux\")\npermit(principal, action, resource);");

        Assert.Equal(2, policy.Annotations.Length);
        Assert.Equal("foo", policy.Annotations[0].Key.Value);
        Assert.Equal(string.Empty, policy.Annotations[0].Value.Value);
        Assert.Equal("baz", policy.Annotations[1].Key.Value);
        Assert.Equal("quux", policy.Annotations[1].Value.Value);
    }

    [Fact]
    public void ParseTwoBareAnnotations()
    {
        PolicyAst policy = ParseSingle("@foo\n@bar\npermit(principal, action, resource);");

        Assert.Equal(2, policy.Annotations.Length);
        Assert.Equal("foo", policy.Annotations[0].Key.Value);
        Assert.Equal(string.Empty, policy.Annotations[0].Value.Value);
        Assert.Equal("bar", policy.Annotations[1].Key.Value);
        Assert.Equal(string.Empty, policy.Annotations[1].Value.Value);
    }

    [Fact]
    public void BareAnnotationEquivalentToExplicitEmptyValue()
    {
        PolicyAst bare = ParseSingle("@foo\npermit(principal, action, resource);");
        PolicyAst explicitEmpty = ParseSingle("@foo(\"\")\npermit(principal, action, resource);");

        Assert.Equal<IReadOnlyList<Annotation>>(bare.Annotations, explicitEmpty.Annotations);
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

    [Fact]
    public void ParseEmptyPolicySetReturnsEmptyArray()
    {
        PolicyAst[] policies = CedarParser.ParsePolicies(string.Empty);

        Assert.Empty(policies);
    }

    [Fact]
    public void ParseMultiSegmentEntityType()
    {
        PolicyAst policy = ParseSingle("permit(principal == Org::Team::User::\"alice\", action, resource);");

        ScopeEq scope = Assert.IsType<ScopeEq>(policy.PrincipalScope);
        Assert.Equal("Org::Team::User", scope.Entity.Type.Value);
        Assert.Equal("alice", scope.Entity.Id.Value);
    }

    [Fact]
    public void ParseHasTagExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { resource.hasTag(\"blue\") };");

        NodeHasTag hasTag = Assert.IsType<NodeHasTag>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeVariable>(hasTag.Left);
        NodeValue tagValue = Assert.IsType<NodeValue>(hasTag.Right);
        Assert.Equal(new CedarString("blue"), tagValue.Value);
    }

    [Fact]
    public void ParseGetTagExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { resource.getTag(\"blue\") };");

        NodeGetTag getTag = Assert.IsType<NodeGetTag>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeVariable>(getTag.Left);
        NodeValue tagValue = Assert.IsType<NodeValue>(getTag.Right);
        Assert.Equal(new CedarString("blue"), tagValue.Value);
    }

    [Fact]
    public void ParseHasTagFromContextExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal.hasTag(context.color) };");

        NodeHasTag hasTag = Assert.IsType<NodeHasTag>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeVariable>(hasTag.Left);
        NodeAccess access = Assert.IsType<NodeAccess>(hasTag.Right);
        Assert.IsType<NodeVariable>(access.Arg);
    }

    [Fact]
    public void ParseMultipleWhenUnlessClauses()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal } unless { action } when { resource } unless { context };");

        Assert.Equal(4, policy.Conditions.Length);
    }

    [Fact]
    public void ParseComparisonOperators()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 1 < 2 };");
        Assert.IsType<NodeLessThan>(Assert.Single(policy.Conditions));

        policy = ParseSingle("permit(principal, action, resource) when { 1 <= 2 };");
        Assert.IsType<NodeLessThanOrEqual>(Assert.Single(policy.Conditions));

        policy = ParseSingle("permit(principal, action, resource) when { 2 > 1 };");
        Assert.IsType<NodeGreaterThan>(Assert.Single(policy.Conditions));

        policy = ParseSingle("permit(principal, action, resource) when { 2 >= 1 };");
        Assert.IsType<NodeGreaterThanOrEqual>(Assert.Single(policy.Conditions));

        policy = ParseSingle("permit(principal, action, resource) when { 1 != 2 };");
        Assert.IsType<NodeNotEquals>(Assert.Single(policy.Conditions));

        policy = ParseSingle("permit(principal, action, resource) when { 1 == 1 };");
        Assert.IsType<NodeEquals>(Assert.Single(policy.Conditions));
    }

    [Fact]
    public void ParseInExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal in Team::\"eng\" };");

        NodeIn node = Assert.IsType<NodeIn>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeVariable>(node.Left);
        Assert.IsType<NodeValue>(node.Right);
    }

    [Fact]
    public void ParseHasExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal has firstName };");

        NodeHas node = Assert.IsType<NodeHas>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeVariable>(node.Arg);
        Assert.Equal(new CedarString("firstName"), node.Attribute);
    }

    [Fact]
    public void ParseHasWithQuotedString()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal has \"1stName\" };");

        NodeHas node = Assert.IsType<NodeHas>(Assert.Single(policy.Conditions));
        Assert.Equal(new CedarString("1stName"), node.Attribute);
    }

    [Fact]
    public void ParseIsExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal is User };");

        NodeIs node = Assert.IsType<NodeIs>(Assert.Single(policy.Conditions));
        Assert.Equal("User", node.EntityType.Value);
    }

    [Fact]
    public void ParseIsWithLongPath()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal is X::Y };");

        NodeIs node = Assert.IsType<NodeIs>(Assert.Single(policy.Conditions));
        Assert.Equal("X::Y", node.EntityType.Value);
    }

    [Fact]
    public void ParseLikeNoWildcards()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal.firstName like \"johnny\" };");

        NodeLike node = Assert.IsType<NodeLike>(Assert.Single(policy.Conditions));
        Assert.True(node.Pattern.Match(new CedarString("johnny")));
        Assert.False(node.Pattern.Match(new CedarString("john")));
    }

    [Fact]
    public void ParseLikeWildcard()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal.name like \"*\" };");

        NodeLike node = Assert.IsType<NodeLike>(Assert.Single(policy.Conditions));
        Assert.True(node.Pattern.Match(new CedarString("anything")));
    }

    [Fact]
    public void ParseLikeEscapedAsterisk()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { \"f*o\" like \"f\\*o\" };");

        NodeLike node = Assert.IsType<NodeLike>(Assert.Single(policy.Conditions));
        Assert.True(node.Pattern.Match(new CedarString("f*o")));
        Assert.False(node.Pattern.Match(new CedarString("foo")));
    }

    [Fact]
    public void ParseLikeWithUnicodeEscapePattern()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal.name like \"\\u{210d}*\" };");

        NodeLike node = Assert.IsType<NodeLike>(Assert.Single(policy.Conditions));
        Assert.True(node.Pattern.Match(new CedarString("\u210dello")));
        Assert.False(node.Pattern.Match(new CedarString("Hello")));
    }

    [Fact]
    public void ParseMostPositiveLong()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 9223372036854775807 == -(-9223372036854775807) };");

        Assert.IsType<NodeEquals>(Assert.Single(policy.Conditions));
    }

    [Fact]
    public void ParseMostNegativeLong()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { -9223372036854775808 == -9223372036854775808 };");

        Assert.IsType<NodeEquals>(Assert.Single(policy.Conditions));
    }

    [Fact]
    public void ParsePrincipalIsScope()
    {
        PolicyAst policy = ParseSingle("permit(principal is X, action, resource);");

        ScopeIs scope = Assert.IsType<ScopeIs>(policy.PrincipalScope);
        Assert.Equal("X", scope.Type.Value);
    }

    [Fact]
    public void ParsePrincipalIsLongScope()
    {
        PolicyAst policy = ParseSingle("permit(principal is X::Y, action, resource);");

        ScopeIs scope = Assert.IsType<ScopeIs>(policy.PrincipalScope);
        Assert.Equal("X::Y", scope.Type.Value);
    }

    [Fact]
    public void ParsePrincipalIsInScope()
    {
        PolicyAst policy = ParseSingle("permit(principal is X in X::\"z\", action, resource);");

        ScopeIsIn scope = Assert.IsType<ScopeIsIn>(policy.PrincipalScope);
        Assert.Equal("X", scope.Type.Value);
        Assert.Equal("X", scope.Entity.Type.Value);
        Assert.Equal("z", scope.Entity.Id.Value);
    }

    [Fact]
    public void ParsePrincipalInScope()
    {
        PolicyAst policy = ParseSingle("permit(principal in Group::\"admins\", action, resource);");

        ScopeIn scope = Assert.IsType<ScopeIn>(policy.PrincipalScope);
        Assert.Equal("Group", scope.Entity.Type.Value);
        Assert.Equal("admins", scope.Entity.Id.Value);
    }

    [Fact]
    public void ParseActionEqScope()
    {
        PolicyAst policy = ParseSingle("permit(principal, action == Action::\"sow\", resource);");

        ScopeEq scope = Assert.IsType<ScopeEq>(policy.ActionScope);
        Assert.Equal("Action", scope.Entity.Type.Value);
        Assert.Equal("sow", scope.Entity.Id.Value);
    }

    [Fact]
    public void ParseResourceEqScope()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource == Crop::\"apple\");");

        ScopeEq scope = Assert.IsType<ScopeEq>(policy.ResourceScope);
        Assert.Equal("Crop", scope.Entity.Type.Value);
        Assert.Equal("apple", scope.Entity.Id.Value);
    }

    [Fact]
    public void ParseResourceInScope()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource in Genus::\"malus\");");

        ScopeIn scope = Assert.IsType<ScopeIn>(policy.ResourceScope);
        Assert.Equal("Genus", scope.Entity.Type.Value);
        Assert.Equal("malus", scope.Entity.Id.Value);
    }

    [Fact]
    public void ParseTrailingCommaInExtensionCall()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { ip(\"1.2.3.4\",) };");

        NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Conditions));
        Assert.Equal("ip", call.Name.Value);
        Assert.Single(call.Args);
    }

    [Fact]
    public void ParseExtensionFunctionNoArgs()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { ip() };");

        NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Conditions));
        Assert.Equal("ip", call.Name.Value);
        Assert.Empty(call.Args);
    }

    [Fact]
    public void ParseExtensionFunctionWithContextArg()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { ip(context.someString) };");

        NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Conditions));
        Assert.Equal("ip", call.Name.Value);
        Assert.Single(call.Args);
        Assert.IsType<NodeAccess>(call.Args[0]);
    }

    [Fact]
    public void ParseContainsAllMethodCall()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { context.strings.containsAll([\"foo\"]) };");

        NodeContainsAll node = Assert.IsType<NodeContainsAll>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeAccess>(node.Left);
        Assert.IsType<NodeSet>(node.Right);
    }

    [Fact]
    public void ParseContainsAnyMethodCall()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { context.strings.containsAny([\"foo\"]) };");

        NodeContainsAny node = Assert.IsType<NodeContainsAny>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeAccess>(node.Left);
        Assert.IsType<NodeSet>(node.Right);
    }

    [Fact]
    public void ParseIsEmptyMethodCall()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { context.strings.isEmpty() };");

        NodeIsEmpty node = Assert.IsType<NodeIsEmpty>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeAccess>(node.Arg);
    }

    [Fact]
    public void ParseAndOverOrPrecedence()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { true && false || true && true };");

        NodeOr or = Assert.IsType<NodeOr>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeAnd>(or.Left);
        Assert.IsType<NodeAnd>(or.Right);
    }

    [Fact]
    public void ParseRelOverAndPrecedence()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 1 < 2 && true };");

        NodeAnd and = Assert.IsType<NodeAnd>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeLessThan>(and.Left);
        Assert.IsType<NodeValue>(and.Right);
    }

    [Fact]
    public void ParseAddOverRelPrecedence()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 1 + 1 < 3 };");

        NodeLessThan lt = Assert.IsType<NodeLessThan>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeAdd>(lt.Left);
        Assert.IsType<NodeValue>(lt.Right);
    }

    [Fact]
    public void ParseMultOverAddPrecedenceRhsAdd()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 2 * 3 + 4 == 10 };");

        NodeEquals equals = Assert.IsType<NodeEquals>(Assert.Single(policy.Conditions));
        NodeAdd add = Assert.IsType<NodeAdd>(equals.Left);
        Assert.IsType<NodeMult>(add.Left);
        Assert.IsType<NodeValue>(add.Right);
    }

    [Fact]
    public void ParseMultOverAddPrecedenceLhsAdd()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 2 + 3 * 4 == 14 };");

        NodeEquals equals = Assert.IsType<NodeEquals>(Assert.Single(policy.Conditions));
        NodeAdd add = Assert.IsType<NodeAdd>(equals.Left);
        Assert.IsType<NodeValue>(add.Left);
        Assert.IsType<NodeMult>(add.Right);
    }

    [Fact]
    public void ParseMemberOverUnaryPrecedence()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { -context.num };");

        NodeNegate negate = Assert.IsType<NodeNegate>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeAccess>(negate.Arg);
    }

    [Fact]
    public void ParseParenthesizedIfThenElse()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { (if true then 2 else 3 * 4) == 2 };");

        NodeEquals equals = Assert.IsType<NodeEquals>(Assert.Single(policy.Conditions));
        NodeIfThenElse ite = Assert.IsType<NodeIfThenElse>(equals.Left);
        Assert.IsType<NodeValue>(ite.If);
        Assert.IsType<NodeValue>(ite.Then);
        Assert.IsType<NodeMult>(ite.Else);
    }

    [Fact]
    public void ParseParenthesizedIfWithTrailingMult()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { (if true then 2 else 3) * 4 == 8 };");

        NodeEquals equals = Assert.IsType<NodeEquals>(Assert.Single(policy.Conditions));
        NodeMult mult = Assert.IsType<NodeMult>(equals.Left);
        Assert.IsType<NodeIfThenElse>(mult.Left);
        Assert.IsType<NodeValue>(mult.Right);
    }

    [Fact]
    public void ParseEmptySetLiteral()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { [] };");

        NodeSet set = Assert.IsType<NodeSet>(Assert.Single(policy.Conditions));
        Assert.Empty(set.Elements);
    }

    [Fact]
    public void ParseEmptyRecordLiteral()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { {} };");

        NodeRecord record = Assert.IsType<NodeRecord>(Assert.Single(policy.Conditions));
        Assert.Empty(record.Elements);
    }

    [Fact]
    public void ParseExtensionMethodCallIsIpv4()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { context.sourceIP.isIpv4() };");

        NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Conditions));
        Assert.Equal("isIpv4", call.Name.Value);
    }

    [Fact]
    public void ParseIpEqualityWithTwoExtensionCalls()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { ip(\"1.2.3.4\") == ip(\"2.3.4.5\") };");

        NodeEquals equals = Assert.IsType<NodeEquals>(Assert.Single(policy.Conditions));
        NodeExtensionCall left = Assert.IsType<NodeExtensionCall>(equals.Left);
        NodeExtensionCall right = Assert.IsType<NodeExtensionCall>(equals.Right);
        Assert.Equal("ip", left.Name.Value);
        Assert.Equal("ip", right.Name.Value);
    }

    [Fact]
    public void ParseDecimalEquality()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { decimal(\"12.34\") == decimal(\"23.45\") };");

        NodeEquals equals = Assert.IsType<NodeEquals>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeExtensionCall>(equals.Left);
        Assert.IsType<NodeExtensionCall>(equals.Right);
    }

    [Fact]
    public void ParsePolicyPositions()
    {
        string input = "// idk a comment\n@blah(\"asdf\")\npermit( principal, action, resource );\n\n\n// later on\n  permit (principal, action, resource) ;\n\n// annotation indent\n @test(\"1234\") permit (principal, action, resource );";

        PolicyAst[] policies = CedarParser.ParsePolicies(input);

        Assert.Equal(3, policies.Length);
        Assert.Equal(2, policies[0].Position.Line);
        Assert.Equal(1, policies[0].Position.Column);
        Assert.Equal(7, policies[1].Position.Line);
        Assert.Equal(3, policies[1].Position.Column);
        Assert.Equal(10, policies[2].Position.Line);
        Assert.Equal(2, policies[2].Position.Column);
    }

    [Fact]
    public void ParseMostNegativeLongComparison()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { -9223372036854775808 < -9223372036854775807 };");

        Assert.IsType<NodeLessThan>(Assert.Single(policy.Conditions));
    }

    [Fact]
    public void ParseTwoAnnotations()
    {
        PolicyAst policy = ParseSingle("@foo(\"bar\") @baz(\"quux\") permit(principal, action, resource);");

        Assert.Equal(2, policy.Annotations.Length);
        Assert.Equal("foo", policy.Annotations[0].Key.Value);
        Assert.Equal("bar", policy.Annotations[0].Value.Value);
        Assert.Equal("baz", policy.Annotations[1].Key.Value);
        Assert.Equal("quux", policy.Annotations[1].Value.Value);
    }

    [Fact]
    public void ParseMultiplePoliciesWithInScopes()
    {
        string input = "permit(principal in Team::\"eng\", action in [PhotoflashRole::\"admin\"], resource in Album::\"jane_vacation\"); permit(principal in Team::\"eng\", action in [PhotoflashRole::\"admin\", PhotoflashRole::\"operator\"], resource in Album::\"jane_vacation\");";

        PolicyAst[] policies = CedarParser.ParsePolicies(input);

        Assert.Equal(2, policies.Length);
        ScopeInSet actionScope0 = Assert.IsType<ScopeInSet>(policies[0].ActionScope);
        Assert.Single(actionScope0.Entities);
        ScopeInSet actionScope1 = Assert.IsType<ScopeInSet>(policies[1].ActionScope);
        Assert.Equal(2, actionScope1.Entities.Length);
    }

    [Fact]
    public void ParseTagsWithGetTag()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { resource.getTag(context.color) };");

        NodeGetTag getTag = Assert.IsType<NodeGetTag>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeVariable>(getTag.Left);
        NodeAccess access = Assert.IsType<NodeAccess>(getTag.Right);
        Assert.IsType<NodeVariable>(access.Arg);
    }

    [Fact]
    public void ParseContainsMethodCall()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { context.strings.contains(\"foo\") };");

        NodeContains node = Assert.IsType<NodeContains>(Assert.Single(policy.Conditions));
        Assert.IsType<NodeAccess>(node.Left);
        Assert.IsType<NodeValue>(node.Right);
    }

    [Fact]
    public void ParseMultiplication()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 42 * 2 };");

        NodeMult mult = Assert.IsType<NodeMult>(Assert.Single(policy.Conditions));
        NodeValue left = Assert.IsType<NodeValue>(mult.Left);
        NodeValue right = Assert.IsType<NodeValue>(mult.Right);
        Assert.Equal(new CedarLong(42), left.Value);
        Assert.Equal(new CedarLong(2), right.Value);
    }

    [Fact]
    public void ParseAddition()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 42 + 2 };");

        NodeAdd add = Assert.IsType<NodeAdd>(Assert.Single(policy.Conditions));
        NodeValue left = Assert.IsType<NodeValue>(add.Left);
        NodeValue right = Assert.IsType<NodeValue>(add.Right);
        Assert.Equal(new CedarLong(42), left.Value);
        Assert.Equal(new CedarLong(2), right.Value);
    }

    [Fact]
    public void ParseSubtraction()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { 42 - 2 };");

        NodeSub sub = Assert.IsType<NodeSub>(Assert.Single(policy.Conditions));
        NodeValue left = Assert.IsType<NodeValue>(sub.Left);
        NodeValue right = Assert.IsType<NodeValue>(sub.Right);
        Assert.Equal(new CedarLong(42), left.Value);
        Assert.Equal(new CedarLong(2), right.Value);
    }

    [Fact]
    public void ParseSingleAnd()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { true && false };");

        NodeAnd and = Assert.IsType<NodeAnd>(Assert.Single(policy.Conditions));
        NodeValue left = Assert.IsType<NodeValue>(and.Left);
        NodeValue right = Assert.IsType<NodeValue>(and.Right);
        Assert.Equal(CedarBool.True, left.Value);
        Assert.Equal(CedarBool.False, right.Value);
    }

    [Fact]
    public void ParseSingleOr()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { true || false };");

        NodeOr or = Assert.IsType<NodeOr>(Assert.Single(policy.Conditions));
        NodeValue left = Assert.IsType<NodeValue>(or.Left);
        NodeValue right = Assert.IsType<NodeValue>(or.Right);
        Assert.Equal(CedarBool.True, left.Value);
        Assert.Equal(CedarBool.False, right.Value);
    }

    [Fact]
    public void ParseResourceIsLongScope()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource is X::Y);");

        ScopeIs scope = Assert.IsType<ScopeIs>(policy.ResourceScope);
        Assert.Equal("X::Y", scope.Type.Value);
    }

    [Fact]
    public void ParseResourceIsInScopeWithEntity()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource is X in X::\"z\");");

        ScopeIsIn scope = Assert.IsType<ScopeIsIn>(policy.ResourceScope);
        Assert.Equal("X", scope.Type.Value);
        Assert.Equal("X", scope.Entity.Type.Value);
        Assert.Equal("z", scope.Entity.Id.Value);
    }

    [Fact]
    public void ParseWhenIsExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal is X };");

        NodeIs node = Assert.IsType<NodeIs>(Assert.Single(policy.Conditions));
        Assert.Equal("X", node.EntityType.Value);
    }

    [Fact]
    public void ParseWhenIsInExpression()
    {
        PolicyAst policy = ParseSingle("permit(principal, action, resource) when { principal is X in X::\"z\" };");

        NodeIsIn node = Assert.IsType<NodeIsIn>(Assert.Single(policy.Conditions));
        Assert.Equal("X", node.EntityType.Value);
    }

    private static PolicyAst ParseSingle(string source)
    {
        PolicyAst[] policies = CedarParser.ParsePolicies(source);
        return Assert.Single(policies);
    }
}

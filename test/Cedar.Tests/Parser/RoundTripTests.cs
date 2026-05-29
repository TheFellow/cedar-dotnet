using System;
using Cedar.Ast.Internal;
using Cedar.Core.Internal.Parser;
using Xunit;

namespace Cedar.Tests.Parser;

public sealed class RoundTripTests
{
    [Theory]
    [MemberData(nameof(PolicyPatterns))]
    public void ParseWriteParseIsStable(string source)
    {
        PolicyAst[] first = CedarParser.ParsePolicies(source);
        string written = CedarWriter.Write(first);

        PolicyAst[] second = CedarParser.ParsePolicies(written);
        string rewritten = CedarWriter.Write(second);

        Assert.Equal(written, rewritten);
    }

    public static TheoryData<string> PolicyPatterns => new()
    {
        "permit(principal, action, resource);",
        "forbid(principal == User::\"alice\", action == Action::\"read\", resource == File::\"f1\");",
        "permit(principal, action in [Action::\"read\", Action::\"write\"], resource);",
        "permit(principal, action, resource is Doc);",
        "permit(principal, action, resource is Doc in Folder::\"fin\");",
        "permit(principal, action, resource) when { true };",
        "permit(principal, action, resource) unless { false };",
        "permit(principal, action, resource) when { 1 + 2 * 3 };",
        "permit(principal, action, resource) when { !principal && action || resource };",
        "permit(principal, action, resource) when { User::\"alice\" };",
        "permit(principal, action, resource) when { [1, 2, 3] };",
        "permit(principal, action, resource) when { {a: 1, \"b\": 2} };",
        "permit(principal, action, resource) when { resource.tags.contains(\"blue\") };",
        "permit(principal, action, resource) when { resource.hasTag(\"env\") && resource.getTag(\"env\") == \"prod\" };",
        "permit(principal, action, resource) when { context.name like \"ab*\\*cd\" };",
        "permit(principal, action, resource) when { if true then 1 else 2 };",
        "permit(principal, action, resource) when { decimal(\"1.0\") };",
        "@id(\"abc\") permit(principal, action, resource);",
        "permit(principal, action, resource) when { principal is User in Team::\"eng\" };",
        "permit(principal, action, resource) when { context has user.name };",
        "permit(principal, action, resource) when { resource[\"env\"] == \"prod\" };",
        "permit(principal, action, resource,) when { ((1 + 2) * 3) };",
        "permit(principal, action, resource);\nforbid(principal, action, resource) when { false };",
        "permit(principal is User, action, resource is Crop);",
        "permit(principal is User in Group::\"folkHeroes\", action, resource is Crop in Genus::\"malus\");",
        "permit(principal in Group::\"folkHeroes\", action in ActionType::\"farming\", resource in Genus::\"malus\");",
        "permit(principal, action in [ActionType::\"farming\", ActionType::\"forestry\"], resource);",
        "permit(principal, action, resource) when { context.strings.containsAll([\"foo\"]) };",
        "permit(principal, action, resource) when { context.strings.containsAny([\"foo\"]) };",
        "permit(principal, action, resource) when { context.strings.isEmpty() };",
        "permit(principal, action, resource) when { context.sourceIP.isIpv4() };",
        "permit(principal, action, resource) when { 42 * 2 };",
        "permit(principal, action, resource) when { principal.hasTag(\"blue\") };",
        "permit(principal, action, resource) when { principal.hasTag(\"blue\") && principal.getTag(\"blue\") == \"green\" };",
        "permit(principal, action, resource) when { ip(\"1.2.3.4\") == ip(\"2.3.4.5\") };",
        "permit(principal, action, resource) when { decimal(\"12.34\") == decimal(\"23.45\") };",
        "permit(principal, action, resource) when { true && false || true && true };",
        "permit(principal, action, resource) when { principal has firstName };",
        "permit(principal, action, resource) when { principal has \"1stName\" };",
        "permit(principal, action, resource) when { principal.firstName like \"johnny\" };",
        "permit(principal, action, resource) when { principal.firstName like \"*\" };",
        "permit(principal, action, resource) when { principal is User };",
        "permit(principal, action, resource) when { principal is User in Group::\"folkHeroes\" };"
    };

    [Theory]
    [MemberData(nameof(ReservedNamesInEntityPath))]
    public void ReservedNameEntityPaths_ParseOrReject(string source, bool shouldSucceed)
    {
        if (shouldSucceed)
        {
            PolicyAst[] policies = CedarParser.ParsePolicies(source);
            Assert.Single(policies);

            string written = CedarWriter.Write(policies);
            PolicyAst[] second = CedarParser.ParsePolicies(written);
            Assert.Single(second);
        }
        else
        {
            Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(source));
        }
    }

    public static TheoryData<string, bool> ReservedNamesInEntityPath =>
        new()
        {
            { "permit(principal, action, resource) when { action::\"test\" };", true },
            { "permit(principal, action, resource) when { context::\"test\" };", true },
            { "permit(principal, action, resource) when { else::\"test\" };", false },
            { "permit(principal, action, resource) when { false::\"test\" };", false },
            { "permit(principal, action, resource) when { forbid::\"test\" };", true },
            { "permit(principal, action, resource) when { has::\"test\" };", false },
            { "permit(principal, action, resource) when { if::\"test\" };", false },
            { "permit(principal, action, resource) when { in::\"test\" };", false },
            { "permit(principal, action, resource) when { is::\"test\" };", false },
            { "permit(principal, action, resource) when { like::\"test\" };", false },
            { "permit(principal, action, resource) when { permit::\"test\" };", true },
            { "permit(principal, action, resource) when { principal::\"test\" };", true },
            { "permit(principal, action, resource) when { resource::\"test\" };", true },
            { "permit(principal, action, resource) when { then::\"test\" };", false },
            { "permit(principal, action, resource) when { true::\"test\" };", false },
            { "permit(principal, action, resource) when { unless::\"test\" };", true },
            { "permit(principal, action, resource) when { when::\"test\" };", true },
            { "permit(principal == action::\"test\", action, resource);", true },
            { "permit(principal == context::\"test\", action, resource);", true },
            { "permit(principal == else::\"test\", action, resource);", false },
            { "permit(principal == false::\"test\", action, resource);", false },
            { "permit(principal == forbid::\"test\", action, resource);", true },
            { "permit(principal == has::\"test\", action, resource);", false },
            { "permit(principal == if::\"test\", action, resource);", false },
            { "permit(principal == in::\"test\", action, resource);", false },
            { "permit(principal == is::\"test\", action, resource);", false },
            { "permit(principal == like::\"test\", action, resource);", false },
            { "permit(principal == permit::\"test\", action, resource);", true },
            { "permit(principal == principal::\"test\", action, resource);", true },
            { "permit(principal == resource::\"test\", action, resource);", true },
            { "permit(principal == then::\"test\", action, resource);", false },
            { "permit(principal == true::\"test\", action, resource);", false },
            { "permit(principal == unless::\"test\", action, resource);", true },
            { "permit(principal == when::\"test\", action, resource);", true },
        };
}

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

    public static TheoryData<string> PolicyPatterns =>
    [
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
        "permit(principal, action, resource);\nforbid(principal, action, resource) when { false };"
    ];
}

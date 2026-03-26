using System.Collections.Generic;
using System.Linq;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Core.Internal.Parser;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Integration;

public sealed class EndToEndTests
{
    private static readonly EntityUid Alice = new(new EntityType("User"), new CedarString("alice"));
    private static readonly EntityUid Bob = new(new EntityType("User"), new CedarString("bob"));
    private static readonly EntityUid Read = new(new EntityType("Action"), new CedarString("read"));
    private static readonly EntityUid Doc1 = new(new EntityType("Document"), new CedarString("doc1"));
    private static readonly EntityUid GroupAdmins = new(new EntityType("Group"), new CedarString("admins"));

    [Fact]
    public void PermitAll_Allows()
    {
        PolicySet policies = BuildPolicySet("permit(principal, action, resource);");
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());

        Assert.Equal(Decision.Allow, decision);
        Assert.Equal(["policy0"], PolicyIds(diagnostic.Reasons));
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void ForbidOverridesPermit_Denies()
    {
        PolicySet policies = BuildPolicySet(
            "permit(principal, action, resource);\n" +
            "forbid(principal, action, resource);");

        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());

        Assert.Equal(Decision.Deny, decision);
        Assert.Equal(["policy1"], PolicyIds(diagnostic.Reasons));
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void PrincipalScopeMismatch_DefaultDeny()
    {
        PolicySet policies = BuildPolicySet("permit(principal == User::\"alice\", action, resource);");
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest(principal: Bob));

        Assert.Equal(Decision.Deny, decision);
        Assert.Empty(diagnostic.Reasons);
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void ContextCondition_AllowsWhenMatched()
    {
        PolicySet policies = BuildPolicySet("permit(principal, action, resource) when { context.level == 5 };");
        CedarRecord context = new(new RecordMap
        {
            [new CedarString("level")] = new CedarLong(5)
        });

        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest(context: context));

        Assert.Equal(Decision.Allow, decision);
        Assert.Equal(["policy0"], PolicyIds(diagnostic.Reasons));
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void ContextCondition_MissingAttributeReportsError()
    {
        PolicySet policies = BuildPolicySet("permit(principal, action, resource) when { context.level == 5 };");
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());

        Assert.Equal(Decision.Deny, decision);
        Assert.Empty(diagnostic.Reasons);
        Assert.Equal(["policy0"], PolicyIds(diagnostic.Errors));
    }

    [Fact]
    public void PrincipalInHierarchy_Allows()
    {
        PolicySet policies = BuildPolicySet("permit(principal in Group::\"admins\", action, resource);");
        Entity alice = new(Alice, new EntityUidSet([GroupAdmins]), new CedarRecord(), new CedarRecord());
        EntityMap entities = new([alice]);

        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, entities, MakeRequest());

        Assert.Equal(Decision.Allow, decision);
        Assert.Equal(["policy0"], PolicyIds(diagnostic.Reasons));
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void ExtensionMethods_EvaluateEndToEnd()
    {
        PolicySet policies = BuildPolicySet(
            "permit(principal, action, resource) when { ip(\"10.0.0.1\").isIpv4() && decimal(\"1.2\").lessThan(decimal(\"2.0\")) };");

        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());

        Assert.Equal(Decision.Allow, decision);
        Assert.Equal(["policy0"], PolicyIds(diagnostic.Reasons));
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void UnlessClause_BlocksPermit()
    {
        PolicySet policies = BuildPolicySet("permit(principal, action, resource) unless { true };");
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());

        Assert.Equal(Decision.Deny, decision);
        Assert.Empty(diagnostic.Reasons);
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void MultiplePermitPolicies_ReportAllReasons()
    {
        PolicySet policies = BuildPolicySet(
            "permit(principal, action, resource);\n" +
            "permit(principal, action == Action::\"read\", resource);");

        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest(action: Read));

        Assert.Equal(Decision.Allow, decision);
        Assert.Equal(["policy0", "policy1"], PolicyIds(diagnostic.Reasons));
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void ErroringForbidPolicy_DoesNotPreventPermit()
    {
        PolicySet policies = BuildPolicySet(
            "permit(principal, action, resource);\n" +
            "forbid(principal, action, resource) when { context.missing == 1 };");

        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());

        Assert.Equal(Decision.Allow, decision);
        Assert.Equal(["policy0"], PolicyIds(diagnostic.Reasons));
        Assert.Equal(["policy1"], PolicyIds(diagnostic.Errors));
    }

    private static Request MakeRequest(EntityUid? principal = null, EntityUid? action = null, EntityUid? resource = null, CedarRecord? context = null)
    {
        return new Request(
            principal ?? Alice,
            action ?? Read,
            resource ?? Doc1,
            context ?? new CedarRecord());
    }

    private static PolicySet BuildPolicySet(string cedar)
    {
        PolicyAst[] ast = CedarParser.ParsePolicies(cedar);
        PolicySet set = new();
        for (int index = 0; index < ast.Length; index++)
        {
            set.Add(new PolicyId($"policy{index}"), new Policy(ast[index]));
        }

        return set;
    }

    private static string[] PolicyIds(IEnumerable<DiagnosticReason> reasons)
    {
        return reasons.Select(static reason => reason.PolicyId.Value).OrderBy(static id => id).ToArray();
    }

    private static string[] PolicyIds(IEnumerable<DiagnosticError> errors)
    {
        return errors.Select(static error => error.PolicyId.Value).OrderBy(static id => id).ToArray();
    }
}

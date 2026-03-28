using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Core.Internal.Eval;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Eval;

public sealed class AuthorizeTests
{
    private static readonly EntityUid Alice = new(new EntityType("User"), new CedarString("alice"));
    private static readonly EntityUid Bob = new(new EntityType("User"), new CedarString("bob"));
    private static readonly EntityUid ActionRead = new(new EntityType("Action"), new CedarString("read"));
    private static readonly EntityUid ActionWrite = new(new EntityType("Action"), new CedarString("write"));
    private static readonly EntityUid Doc1 = new(new EntityType("Document"), new CedarString("doc1"));
    private static readonly EntityUid Doc2 = new(new EntityType("Document"), new CedarString("doc2"));
    private static readonly EntityUid Group = new(new EntityType("Group"), new CedarString("admins"));
    private static readonly Position NoPos = new("", 0, 0, 0);

    private static Request MakeRequest(EntityUid? principal = null, EntityUid? action = null, EntityUid? resource = null)
    {
        return new Request(
            principal ?? Alice,
            action ?? ActionRead,
            resource ?? Doc1,
            new CedarRecord());
    }

    private static Policy MakePolicy(Effect effect, IScope? principalScope = null, IScope? actionScope = null, IScope? resourceScope = null, params INode[] conditions)
    {
        PolicyAst ast = new(
            effect,
            principalScope ?? new ScopeAll(),
            actionScope ?? new ScopeAll(),
            resourceScope ?? new ScopeAll(),
            conditions.Length > 0 ? ImmutableArray.Create(conditions) : ImmutableArray<INode>.Empty,
            ImmutableArray<Annotation>.Empty,
            NoPos);
        return new Policy(ast);
    }

    private static PolicySet MakePolicySet(params (string id, Policy policy)[] entries)
    {
        PolicySet set = new();
        foreach ((string id, Policy policy) in entries)
        {
            set.Add(new PolicyId(id), policy);
        }
        return set;
    }

    // --- Basic authorization ---

    [Fact]
    public void SimplePermit_Allow()
    {
        PolicySet policies = MakePolicySet(("p1", MakePolicy(Effect.Permit)));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void SimpleForbid_Deny()
    {
        PolicySet policies = MakePolicySet(("f1", MakePolicy(Effect.Forbid)));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Deny, decision);
    }

    [Fact]
    public void ForbidOverridesPermit_Deny()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit)),
            ("f1", MakePolicy(Effect.Forbid)));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Deny, decision);
    }

    [Fact]
    public void NoPolicies_DefaultDeny()
    {
        PolicySet policies = new();
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Deny, decision);
    }

    [Fact]
    public void MultiplePermits_NoForbids_Allow()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit)),
            ("p2", MakePolicy(Effect.Permit)));
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Allow, decision);
        Assert.True(diagnostic.Reasons.Length >= 2);
    }

    [Fact]
    public void MultiplePermits_WithReasons_HasAllReasons()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit)),
            ("p2", MakePolicy(Effect.Permit)));
        (Decision _, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(2, diagnostic.Reasons.Length);
    }

    // --- Eval error captured in diagnostics ---

    [Fact]
    public void EvalError_CapturedInDiagnostics_DoesNotCrash()
    {
        // Create a policy with a condition that will cause an eval error (accessing a missing attribute)
        INode errorCondition = new NodeAccess(new NodeVariable(new CedarString("principal")), new NodeValue(new CedarString("missing_attr")));
        PolicySet policies = MakePolicySet(
            ("err", MakePolicy(Effect.Permit, conditions: errorCondition)));
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Deny, decision);
        Assert.True(diagnostic.Errors.Length > 0);
    }

    [Fact]
    public void EvalError_StillAllowsOtherPolicies()
    {
        INode errorCondition = new NodeAccess(new NodeVariable(new CedarString("principal")), new NodeValue(new CedarString("missing_attr")));
        PolicySet policies = MakePolicySet(
            ("err", MakePolicy(Effect.Permit, conditions: errorCondition)),
            ("p1", MakePolicy(Effect.Permit)));
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Allow, decision);
        Assert.True(diagnostic.Errors.Length > 0);
        Assert.True(diagnostic.Reasons.Length > 0);
    }

    // --- Scope filtering ---

    [Fact]
    public void PrincipalMismatch_PolicyNotApplied()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit, principalScope: new ScopeEq(Bob))));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Deny, decision);
    }

    [Fact]
    public void PrincipalMatch_PolicyApplied()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit, principalScope: new ScopeEq(Alice))));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void ActionScopeFiltering_Mismatch_NotApplied()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit, actionScope: new ScopeEq(ActionWrite))));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Deny, decision);
    }

    [Fact]
    public void ActionScopeFiltering_Match_Applied()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit, actionScope: new ScopeEq(ActionRead))));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void ResourceScopeFiltering_Mismatch_NotApplied()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit, resourceScope: new ScopeEq(Doc2))));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Deny, decision);
    }

    [Fact]
    public void ResourceScopeFiltering_Match_Applied()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit, resourceScope: new ScopeEq(Doc1))));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Allow, decision);
    }

    // --- Condition filtering ---

    [Fact]
    public void WhenCondition_True_PolicyApplied()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit, conditions: new NodeValue(CedarBool.True))));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void WhenCondition_False_PolicyNotApplied()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit, conditions: new NodeValue(CedarBool.False))));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Deny, decision);
    }

    [Fact]
    public void UnlessCondition_ForbidWithConditionFalse_NotApplied()
    {
        // Forbid with a condition that evaluates to false -- the forbid should not fire
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit)),
            ("f1", MakePolicy(Effect.Forbid, conditions: new NodeValue(CedarBool.False))));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void UnlessCondition_ForbidWithConditionTrue_Applied()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit)),
            ("f1", MakePolicy(Effect.Forbid, conditions: new NodeValue(CedarBool.True))));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Deny, decision);
    }

    // --- Entity hierarchy ---

    [Fact]
    public void PrincipalInScope_WithEntityHierarchy_Matches()
    {
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit, principalScope: new ScopeIn(Group))));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(new[] { aliceEntity }), MakeRequest());
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PrincipalInScope_MissingPrincipalEntity_DeniesWithoutThrow()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit, principalScope: new ScopeIn(Group))));

        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());

        Assert.Equal(Decision.Deny, decision);
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void PrincipalInScope_MissingAncestorEntity_DeniesWithoutThrow()
    {
        EntityUid organization = new(new EntityType("Organization"), new CedarString("acme"));
        Entity aliceEntity = new(Alice, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit, principalScope: new ScopeIn(organization))));

        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(new[] { aliceEntity }), MakeRequest());

        Assert.Equal(Decision.Deny, decision);
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void NullEntities_UsesEmptyEntityMap()
    {
        PolicySet policies = MakePolicySet(("p1", MakePolicy(Effect.Permit)));
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, null!, MakeRequest());
        Assert.Equal(Decision.Allow, decision);
    }

    // --- IPolicyIterator (non-PolicySet) ---

    [Fact]
    public void PolicyList_GeneratesAutoIds()
    {
        Policy permit = MakePolicy(Effect.Permit);
        SimplePolicyIterator iterator = new(new[] { permit });
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(iterator, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Allow, decision);
        Assert.Equal("policy0", diagnostic.Reasons[0].PolicyId.Value);
    }

    [Fact]
    public void ForbidDiagnostic_ContainsForbidReasons()
    {
        PolicySet policies = MakePolicySet(
            ("p1", MakePolicy(Effect.Permit)),
            ("f1", MakePolicy(Effect.Forbid)));
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), MakeRequest());
        Assert.Equal(Decision.Deny, decision);
        Assert.True(diagnostic.Reasons.Length > 0);
    }

    [Fact]
    public void BracketAccess_OnRecord_EvaluatesCorrectly()
    {
        // Bracket access like record["key"] must produce NodeAccess (attribute access),
        // not NodeGetTag (tag access). This was a parser bug where [] was mapped to getTag.
        string cedarText = "forbid(principal is a, action == Action::\"action\", resource) when { (true && ({\"k\": \"v\"}[\"k\"] like \"3\")) && false };";

        Policy[] policies = Policy.UnmarshalCedarList(cedarText);
        PolicySet policySet = new();
        policySet.Add(new PolicyId("policy0"), policies[0]);

        EntityUid principal = new(new EntityType("a"), new CedarString(""));
        EntityUid action = new(new EntityType("Action"), new CedarString("action"));
        EntityUid resource = new(new EntityType("a"), new CedarString(""));
        Request request = new(principal, action, resource, new CedarRecord());

        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policySet, new EntityMap(), request);

        Assert.Empty(diagnostic.Errors);
        Assert.Empty(diagnostic.Reasons);
        Assert.Equal(Decision.Deny, decision);
    }

    [Fact]
    public void IpAddress_ShortForm_IsRejected()
    {
        // Cedar follows Go's netip.ParseAddr which requires strict dotted-decimal for IPv4.
        // Short forms like "0", "127", "192.168" must be rejected.
        Assert.Throws<FormatException>(() => CedarIpAddress.Parse("0"));
        Assert.Throws<FormatException>(() => CedarIpAddress.Parse("127"));
        Assert.Throws<FormatException>(() => CedarIpAddress.Parse("192.168"));
    }

    [Fact]
    public void IpAddress_LeadingZeros_IsRejected()
    {
        // Cedar follows Go's netip.ParseAddr which rejects leading zeros in IPv4 octets.
        Assert.Throws<FormatException>(() => CedarIpAddress.Parse("01.02.03.04"));
        Assert.Throws<FormatException>(() => CedarIpAddress.Parse("1.2.3.04"));
    }

    private sealed class SimplePolicyIterator : IPolicyIterator
    {
        private readonly Policy[] _policies;

        public SimplePolicyIterator(Policy[] policies)
        {
            _policies = policies;
        }

        public IEnumerable<Policy> Policies => _policies;
    }
}

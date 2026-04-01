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

    [Fact]
    public void IsEmpty_InPolicy_AllowsWhenContextSetIsEmpty()
    {
        const string cedarText = "permit(principal, action, resource) when { context.foo.isEmpty() && !context.bar.isEmpty() };";

        PolicySet policies = PolicySet.ParseCedar(cedarText);
        CedarRecord context = new(new RecordMap
        {
            [new CedarString("foo")] = new CedarSet(),
            [new CedarString("bar")] = new CedarSet(new CedarLong(1))
        });
        Request request = new(
            new EntityUid(new EntityType("Principal"), new CedarString("1")),
            new EntityUid(new EntityType("Action"), new CedarString("action")),
            new EntityUid(new EntityType("Resource"), new CedarString("resource")),
            context);

        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), request);

        Assert.Equal(Decision.Allow, decision);
        Assert.Empty(diagnostic.Errors);
        Assert.Equal("policy0", Assert.Single(diagnostic.Reasons).PolicyId.Value);
    }

    // --- End-to-end tests from Go authorize_test.go ---

    [Fact]
    public void PermitWhenTags_PrincipalHasTag_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { principal.hasTag(\"foo\") };");
        EntityUid cuzco = new(new EntityType("coder"), new CedarString("cuzco"));
        Entity cuzcoEntity = new(cuzco, new EntityUidSet(), new CedarRecord(),
            new CedarRecord(new RecordMap { [new CedarString("foo")] = new CedarString("bar") }));

        Request request = new(cuzco, new EntityUid(new EntityType("table"), new CedarString("drop")),
            new EntityUid(new EntityType("table"), new CedarString("whatever")), new CedarRecord());
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(new[] { cuzcoEntity }), request);

        Assert.Equal(Decision.Allow, decision);
        Assert.Empty(diagnostic.Errors);
    }

    [Fact]
    public void PermitNoMatch_ResourceScope_Deny()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource in asdf::\"1234\");");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Deny, decision);
    }

    [Fact]
    public void ErrorInPolicy_CapturedAsDiagnosticError()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { resource in \"foo\" };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Deny, decision);
        Assert.Single(diagnostic.Errors);
    }

    [Fact]
    public void ErrorInPolicy_ContinuesToNextPolicy()
    {
        PolicySet policies = PolicySet.ParseCedar(
            "permit(principal,action,resource) when { resource in \"foo\" };\n" +
            "permit(principal,action,resource);");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
        Assert.Single(diagnostic.Errors);
    }

    [Fact]
    public void PermitRequiresContext_ContextMatches_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { context.x == 42 };");
        CedarRecord context = new(new RecordMap { [new CedarString("x")] = new CedarLong(42) });
        Request request = new(Alice, ActionRead, Doc1, context);
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitRequiresContext_ContextMismatch_Deny()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { context.x == 42 };");
        CedarRecord context = new(new RecordMap { [new CedarString("x")] = new CedarLong(43) });
        Request request = new(Alice, ActionRead, Doc1, context);
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Deny, decision);
    }

    [Fact]
    public void PermitRequiresEntity_EntityMatchesAttribute_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { principal.x == 42 };");
        Entity aliceEntity = new(Alice, new EntityUidSet(), new CedarRecord(new RecordMap { [new CedarString("x")] = new CedarLong(42) }), new CedarRecord());
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(new[] { aliceEntity }), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitRequiresEntity_EntityMismatch_Deny()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { principal.x == 42 };");
        Entity aliceEntity = new(Alice, new EntityUidSet(), new CedarRecord(new RecordMap { [new CedarString("x")] = new CedarLong(43) }), new CedarRecord());
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(new[] { aliceEntity }), request);
        Assert.Equal(Decision.Deny, decision);
    }

    [Fact]
    public void PermitActionIn_WithParent_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action in scary::\"stuff\",resource);");
        EntityUid dropTable = new(new EntityType("table"), new CedarString("drop"));
        EntityUid scaryStuff = new(new EntityType("scary"), new CedarString("stuff"));
        Entity dropEntity = new(dropTable, new EntityUidSet(new[] { scaryStuff }), new CedarRecord(), new CedarRecord());
        Request request = new(Alice, dropTable, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(new[] { dropEntity }), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitActionInSet_WithParent_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action in [scary::\"stuff\"],resource);");
        EntityUid dropTable = new(new EntityType("table"), new CedarString("drop"));
        EntityUid scaryStuff = new(new EntityType("scary"), new CedarString("stuff"));
        Entity dropEntity = new(dropTable, new EntityUidSet(new[] { scaryStuff }), new CedarRecord(), new CedarRecord());
        Request request = new(Alice, dropTable, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(new[] { dropEntity }), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenRelations_AllComparisons_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { (1<2) && (1<=1) && (2>1) && (1>=1) && (1!=2) && (1==1)};");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenPrincipalInPrincipal_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { principal in principal };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenPrincipalHasAttribute_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { principal has name };");
        Entity aliceEntity = new(Alice, new EntityUidSet(), new CedarRecord(new RecordMap { [new CedarString("name")] = new CedarString("bob") }), new CedarRecord());
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(new[] { aliceEntity }), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenAddSubtract_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { 40+3-1==42 };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenMultiply_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { 6*7==42 };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenNegate_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { -42==-42 };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenNot_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { !(1+1==42) };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenSetContains_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { [1,2,3].contains(2) };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenRecordHas_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { {name:\"bob\"} has name };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenActionInAction_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { action in action };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenContainsAll_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { [1,2,3].containsAll([2,3]) };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenContainsAny_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { [1,2,3].containsAny([2,5]) };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenRecordAccess_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { {name:\"bob\"}[\"name\"] == \"bob\" };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenLike_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { \"bananas\" like \"*nan*\" };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenDecimalComparisons_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar(
            "permit(principal,action,resource) when {\n" +
            "  decimal(\"10.0\").lessThan(decimal(\"11.0\")) &&\n" +
            "  decimal(\"10.0\").lessThanOrEqual(decimal(\"11.0\")) &&\n" +
            "  decimal(\"10.0\").greaterThan(decimal(\"9.0\")) &&\n" +
            "  decimal(\"10.0\").greaterThanOrEqual(decimal(\"9.0\")) };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenDecimalWrongArity_EvalError()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { decimal(1, 2) };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic diagnostic) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Deny, decision);
        Assert.Single(diagnostic.Errors);
    }

    [Fact]
    public void PermitWhenDatetime_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar(
            "permit(principal,action,resource) when {\n" +
            "  datetime(\"1970-01-01T09:08:07Z\") < (datetime(\"1970-02-01\")) &&\n" +
            "  datetime(\"1970-01-01T09:08:07Z\") <= (datetime(\"1970-02-01\")) &&\n" +
            "  datetime(\"1970-01-01T09:08:07Z\") > (datetime(\"1970-01-01\")) &&\n" +
            "  datetime(\"1970-01-01T09:08:07Z\") >= (datetime(\"1970-01-01\")) &&\n" +
            "  datetime(\"1970-01-01T09:08:07Z\").toDate() == datetime(\"1970-01-01\")};");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenDuration_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar(
            "permit(principal,action,resource) when {\n" +
            "  duration(\"9h8m\") < (duration(\"10h\")) &&\n" +
            "  duration(\"9h8m\") <= (duration(\"10h\")) &&\n" +
            "  duration(\"9h8m\") > (duration(\"7h\")) &&\n" +
            "  duration(\"9h8m\") >= (duration(\"7h\")) &&\n" +
            "  duration(\"1ms\").toMilliseconds() == 1 &&\n" +
            "  duration(\"1s\").toSeconds() == 1 &&\n" +
            "  duration(\"1m\").toMinutes() == 1 &&\n" +
            "  duration(\"1h\").toHours() == 1 &&\n" +
            "  duration(\"1d\").toDays() == 1 &&\n" +
            "  datetime(\"1970-01-01\").toTime() == duration(\"0ms\") &&\n" +
            "  datetime(\"1970-01-01\").offset(duration(\"1ms\")).toTime() == duration(\"1ms\") &&\n" +
            "  datetime(\"1970-01-01T00:00:00.001Z\").durationSince(datetime(\"1970-01-01\")) == duration(\"1ms\")};");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenIp_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar(
            "permit(principal,action,resource) when {\n" +
            "  ip(\"1.2.3.4\").isIpv4() &&\n" +
            "  ip(\"a:b:c:d::/16\").isIpv6() &&\n" +
            "  ip(\"::1\").isLoopback() &&\n" +
            "  ip(\"224.1.2.3\").isMulticast() &&\n" +
            "  ip(\"127.0.0.1\").isInRange(ip(\"127.0.0.0/16\"))};");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void NegativeUnaryOp_ContextValue_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { -context.value > 0 };");
        CedarRecord context = new(new RecordMap { [new CedarString("value")] = new CedarLong(-42) });
        Request request = new(Alice, ActionRead, Doc1, context);
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PrincipalIs_TypeMatches_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal is User,action,resource);");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PrincipalIsIn_TypeAndEntityMatch_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal is User in User::\"alice\",action,resource);");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void ResourceIs_TypeMatches_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource is Document);");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void ResourceIsIn_TypeAndEntityMatch_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource is Document in Document::\"doc1\");");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void WhenResourceIs_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { resource is Document };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void WhenResourceIsIn_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { resource is Document in Document::\"doc1\" };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void WhenResourceIsIn_WithEntityHierarchy_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { resource is Document in Group::\"admins\" };");
        Entity docEntity = new(Doc1, new EntityUidSet(new[] { Group }), new CedarRecord(), new CedarRecord());
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(new[] { docEntity }), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void Rfc57_GeneralMultiplication_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal, action, resource) when { context.foo * principal.bar >= 100 };");
        EntityUid principal = new(new EntityType("Principal"), new CedarString("1"));
        Entity principalEntity = new(principal, new EntityUidSet(),
            new CedarRecord(new RecordMap { [new CedarString("bar")] = new CedarLong(42) }), new CedarRecord());
        CedarRecord context = new(new RecordMap { [new CedarString("foo")] = new CedarLong(43) });
        Request request = new(principal, ActionRead, Doc1, context);
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(new[] { principalEntity }), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenIfThenElse_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { (if true then true else true) };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenOr_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { (true || false) };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitWhenAnd_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) when { (true && true) };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
    }

    [Fact]
    public void PermitUnlessFalse_Allow()
    {
        PolicySet policies = PolicySet.ParseCedar("permit(principal,action,resource) unless { false };");
        Request request = new(Alice, ActionRead, Doc1, new CedarRecord());
        (Decision decision, Diagnostic _) = Authorization.Authorize(policies, new EntityMap(), request);
        Assert.Equal(Decision.Allow, decision);
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

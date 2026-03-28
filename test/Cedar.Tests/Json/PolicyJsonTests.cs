using System;
using System.Collections.Generic;
using System.Text.Json;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Types;
using Xunit;
using static Cedar.Ast.Values;

namespace Cedar.Tests.Json;

public sealed class PolicyJsonTests
{
    [Fact]
    public void MarshalJson_OmitsNullOptionalScopeFieldsAnnotationsAndConditions()
    {
        Policy policy = new(CedarAst.Permit().Ast);

        string json = policy.MarshalJson();

        Assert.DoesNotContain("\"conditions\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"annotations\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"entity\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"entities\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"entity_type\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"in\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MarshalJson_IncludesConditionAndAnnotationWhenPresent()
    {
        Policy policy = new(CedarAst.Annotation("env", "prod")
            .Permit()
            .When(Boolean(true))
            .Ast);

        string json = policy.MarshalJson();

        Assert.Contains("\"conditions\":[{\"kind\":\"when\"", json, StringComparison.Ordinal);
        Assert.Contains("\"annotations\":{\"env\":\"prod\"}", json, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalJson_ScopeEqWithoutEntityThrowsJsonException()
    {
        const string json = """
            {
              "effect": "permit",
              "principal": { "op": "==" },
              "action": { "op": "All" },
              "resource": { "op": "All" }
            }
            """;

        JsonException exception = Assert.Throws<JsonException>(() => Policy.UnmarshalJson(json));

        Assert.Contains("Scope with op '==' must include 'entity'.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalJson_ValueNode_BoolTrue_IsCedarBoolTrue()
    {
        NodeValue node = UnmarshalConditionValueNode("true");

        Assert.Equal(CedarBool.True, node.Value);
    }

    [Fact]
    public void UnmarshalJson_ValueNode_BoolFalse_IsCedarBoolFalse()
    {
        NodeValue node = UnmarshalConditionValueNode("false");

        Assert.Equal(CedarBool.False, node.Value);
    }

    [Fact]
    public void UnmarshalJson_ValueNode_Long_IsCedarLong()
    {
        NodeValue node = UnmarshalConditionValueNode("42");

        Assert.Equal(new CedarLong(42), node.Value);
    }

    [Fact]
    public void UnmarshalJson_ValueNode_ExplicitEntity_IsEntityUid()
    {
        NodeValue node = UnmarshalConditionValueNode("""
            { "__entity": { "type": "User", "id": "alice" } }
            """);

        EntityUid uid = Assert.IsType<EntityUid>(node.Value);
        Assert.Equal(new EntityType("User"), uid.Type);
        Assert.Equal(new CedarString("alice"), uid.Id);
    }

    [Fact]
    public void UnmarshalJson_ValueNode_NonIntegerNumber_ThrowsJsonException()
    {
        JsonException exception = Assert.Throws<JsonException>(() => UnmarshalConditionValueNode("3.14"));

        Assert.Contains("signed 64-bit integer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalJson_ConditionBodyDecimalExtensionCall_IsNodeExtensionCall()
    {
        const string bodyJson = """
            { "decimal": [{ "Value": "1.0" }] }
            """;

        Policy policy = UnmarshalPolicyWithConditionBody(bodyJson);

        NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
        Assert.Equal("decimal", call.Name);

        NodeValue arg = Assert.IsType<NodeValue>(Assert.Single(call.Args));
        Assert.Equal(new CedarString("1.0"), Assert.IsType<CedarString>(arg.Value));

        string remarshaled = policy.MarshalJson();
        Assert.Contains("\"decimal\":[{\"Value\":\"1.0\"}]", remarshaled, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalJson_ConditionBodyIpExtensionCall_IsNodeExtensionCall()
    {
        const string bodyJson = """
            { "ip": [{ "Value": "127.0.0.1" }] }
            """;

        Policy policy = UnmarshalPolicyWithConditionBody(bodyJson);

        NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
        Assert.Equal("ip", call.Name);

        NodeValue arg = Assert.IsType<NodeValue>(Assert.Single(call.Args));
        Assert.Equal(new CedarString("127.0.0.1"), Assert.IsType<CedarString>(arg.Value));

        string remarshaled = policy.MarshalJson();
        Assert.Contains("\"ip\":[{\"Value\":\"127.0.0.1\"}]", remarshaled, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalJson_ConditionBodyLessThanExtensionCall_RoundTripsNestedDecimalCalls()
    {
        const string bodyJson = """
            {
              "lessThan": [
                { "decimal": [{ "Value": "1.0" }] },
                { "decimal": [{ "Value": "2.0" }] }
              ]
            }
            """;

        Policy policy = UnmarshalPolicyWithConditionBody(bodyJson);

        NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
        Assert.Equal("lessThan", call.Name);
        Assert.Equal(2, call.Args.Length);

        NodeExtensionCall left = Assert.IsType<NodeExtensionCall>(call.Args[0]);
        NodeExtensionCall right = Assert.IsType<NodeExtensionCall>(call.Args[1]);

        Assert.Equal("decimal", left.Name);
        Assert.Equal("decimal", right.Name);
        Assert.Equal(new CedarString("1.0"), Assert.IsType<CedarString>(Assert.IsType<NodeValue>(Assert.Single(left.Args)).Value));
        Assert.Equal(new CedarString("2.0"), Assert.IsType<CedarString>(Assert.IsType<NodeValue>(Assert.Single(right.Args)).Value));

        string remarshaled = policy.MarshalJson();
        Assert.Contains("\"lessThan\":[{\"decimal\":[{\"Value\":\"1.0\"}]},{\"decimal\":[{\"Value\":\"2.0\"}]}]", remarshaled, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalJson_ConditionBodyIsIpv4ExtensionCall_RoundTripsNestedIpCall()
    {
        const string bodyJson = """
            {
              "isIpv4": [
                { "ip": [{ "Value": "127.0.0.1" }] }
              ]
            }
            """;

        Policy policy = UnmarshalPolicyWithConditionBody(bodyJson);

        NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
        Assert.Equal("isIpv4", call.Name);

        NodeExtensionCall arg = Assert.IsType<NodeExtensionCall>(Assert.Single(call.Args));
        Assert.Equal("ip", arg.Name);
        Assert.Equal(new CedarString("127.0.0.1"), Assert.IsType<CedarString>(Assert.IsType<NodeValue>(Assert.Single(arg.Args)).Value));

        string remarshaled = policy.MarshalJson();
        Assert.Contains("\"isIpv4\":[{\"ip\":[{\"Value\":\"127.0.0.1\"}]}]", remarshaled, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalJson_ConditionBodyIsInRangeExtensionCall_RoundTripsNestedIpCalls()
    {
        const string bodyJson = """
            {
              "isInRange": [
                { "ip": [{ "Value": "192.168.1.10" }] },
                { "ip": [{ "Value": "192.168.1.0/24" }] }
              ]
            }
            """;

        Policy policy = UnmarshalPolicyWithConditionBody(bodyJson);

        NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(Assert.Single(policy.Ast.Conditions));
        Assert.Equal("isInRange", call.Name);
        Assert.Equal(2, call.Args.Length);

        NodeExtensionCall left = Assert.IsType<NodeExtensionCall>(call.Args[0]);
        NodeExtensionCall right = Assert.IsType<NodeExtensionCall>(call.Args[1]);

        Assert.Equal("ip", left.Name);
        Assert.Equal("ip", right.Name);
        Assert.Equal(new CedarString("192.168.1.10"), Assert.IsType<CedarString>(Assert.IsType<NodeValue>(Assert.Single(left.Args)).Value));
        Assert.Equal(new CedarString("192.168.1.0/24"), Assert.IsType<CedarString>(Assert.IsType<NodeValue>(Assert.Single(right.Args)).Value));

        string remarshaled = policy.MarshalJson();
        Assert.Contains("\"isInRange\":[{\"ip\":[{\"Value\":\"192.168.1.10\"}]},{\"ip\":[{\"Value\":\"192.168.1.0/24\"}]}]", remarshaled, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmarshalJson_ConditionBodyIsSet_DeserializesSetNode()
    {
        const string bodyJson = """
            {
              "Set": [
                { "Value": true },
                { "Value": 42 }
              ]
            }
            """;

        Policy policy = UnmarshalPolicyWithConditionBody(bodyJson);

        NodeSet set = Assert.IsType<NodeSet>(Assert.Single(policy.Ast.Conditions));
        Assert.Equal(2, set.Elements.Length);

        NodeValue first = Assert.IsType<NodeValue>(set.Elements[0]);
        Assert.Equal(CedarBool.True, first.Value);

        NodeValue second = Assert.IsType<NodeValue>(set.Elements[1]);
        Assert.Equal(new CedarLong(42), second.Value);
    }

    [Fact]
    public void UnmarshalJson_ConditionBodyIsRecord_DeserializesRecordNode()
    {
        const string bodyJson = """
            {
              "Record": {
                "x": { "Value": "hello" }
              }
            }
            """;

        Policy policy = UnmarshalPolicyWithConditionBody(bodyJson);

        NodeRecord record = Assert.IsType<NodeRecord>(Assert.Single(policy.Ast.Conditions));
        NodeRecordElement element = Assert.Single(record.Elements);

        Assert.Equal(new CedarString("x"), element.Key);
        Assert.Equal(new CedarString("hello"), Assert.IsType<NodeValue>(element.Value).Value);
    }

    [Fact]
    public void RoundTrip_SetNodeCondition_PreservesStructure()
    {
        Policy policy = new(CedarAst.Permit()
            .When(Set(Long(1), Long(2)))
            .Ast);

        string json = policy.MarshalJson();
        Policy roundTripped = Policy.UnmarshalJson(json);

        NodeSet set = Assert.IsType<NodeSet>(Assert.Single(roundTripped.Ast.Conditions));
        Assert.Equal(2, set.Elements.Length);
        Assert.Equal(new CedarLong(1), Assert.IsType<NodeValue>(set.Elements[0]).Value);
        Assert.Equal(new CedarLong(2), Assert.IsType<NodeValue>(set.Elements[1]).Value);
    }

    [Fact]
    public void RoundTrip_RecordNodeCondition_PreservesStructure()
    {
        Policy policy = new(CedarAst.Permit()
            .When(RecordNodes(new Dictionary<string, Node>
            {
                ["name"] = Boolean(true)
            }))
            .Ast);

        string json = policy.MarshalJson();
        Policy roundTripped = Policy.UnmarshalJson(json);

        NodeRecord record = Assert.IsType<NodeRecord>(Assert.Single(roundTripped.Ast.Conditions));
        NodeRecordElement element = Assert.Single(record.Elements);

        Assert.Equal(new CedarString("name"), element.Key);
        Assert.Equal(CedarBool.True, Assert.IsType<NodeValue>(element.Value).Value);
    }

    [Fact]
    public void UnmarshalJson_ConditionBodyIfThenElse_DeserializesNodeIfThenElse()
    {
        const string bodyJson = """
            {
              "if-then-else": {
                "if": { "Value": true },
                "then": { "Value": 1 },
                "else": { "Value": 2 }
              }
            }
            """;

        Policy policy = UnmarshalPolicyWithConditionBody(bodyJson);

        NodeIfThenElse node = Assert.IsType<NodeIfThenElse>(Assert.Single(policy.Ast.Conditions));
        Assert.Equal(CedarBool.True, Assert.IsType<NodeValue>(node.If).Value);
        Assert.Equal(new CedarLong(1), Assert.IsType<NodeValue>(node.Then).Value);
        Assert.Equal(new CedarLong(2), Assert.IsType<NodeValue>(node.Else).Value);
    }

    [Fact]
    public void RoundTrip_IfThenElseCondition_PreservesIfThenElseJsonShape()
    {
        const string bodyJson = """
            {
              "if-then-else": {
                "if": { "Value": true },
                "then": { "Value": 1 },
                "else": { "Value": 2 }
              }
            }
            """;

        Policy policy = UnmarshalPolicyWithConditionBody(bodyJson);

        string json = policy.MarshalJson();
        Policy roundTripped = Policy.UnmarshalJson(json);

        Assert.Contains("\"if-then-else\"", json, StringComparison.Ordinal);
        Assert.Contains("\"if\":{\"Value\":true}", json, StringComparison.Ordinal);
        Assert.Contains("\"then\":{\"Value\":1}", json, StringComparison.Ordinal);
        Assert.Contains("\"else\":{\"Value\":2}", json, StringComparison.Ordinal);

        NodeIfThenElse node = Assert.IsType<NodeIfThenElse>(Assert.Single(roundTripped.Ast.Conditions));
        Assert.Equal(CedarBool.True, Assert.IsType<NodeValue>(node.If).Value);
        Assert.Equal(new CedarLong(1), Assert.IsType<NodeValue>(node.Then).Value);
        Assert.Equal(new CedarLong(2), Assert.IsType<NodeValue>(node.Else).Value);
    }

    private static NodeValue UnmarshalConditionValueNode(string valueJson)
    {
        Policy policy = UnmarshalPolicyWithConditionBody($$"""
            { "Value": {{valueJson}} }
            """);

        INode condition = Assert.Single(policy.Ast.Conditions);
        return Assert.IsType<NodeValue>(condition);
    }

    private static Policy UnmarshalPolicyWithConditionBody(string bodyJson)
    {
        string json = $$"""
            {
              "effect": "permit",
              "principal": { "op": "All" },
              "action": { "op": "All" },
              "resource": { "op": "All" },
              "conditions": [
                {
                  "kind": "when",
                  "body": {{bodyJson}}
                }
              ]
            }
            """;

        return Policy.UnmarshalJson(json);
    }
}
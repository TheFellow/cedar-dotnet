using System;
using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Types;
using Xunit;
using static Cedar.Ast.Values;
using static Cedar.Ast.Variables;

namespace Cedar.Tests.Ast;

public sealed class VariableAndValueTests
{
    [Fact]
    public void PrincipalCreatesPrincipalVariableNode()
    {
        NodeVariable node = Assert.IsType<NodeVariable>(Principal().Inner);

        Assert.Equal("principal", node.Name.Value);
    }

    [Fact]
    public void ActionCreatesActionVariableNode()
    {
        NodeVariable node = Assert.IsType<NodeVariable>(Action().Inner);

        Assert.Equal("action", node.Name.Value);
    }

    [Fact]
    public void ResourceCreatesResourceVariableNode()
    {
        NodeVariable node = Assert.IsType<NodeVariable>(Resource().Inner);

        Assert.Equal("resource", node.Name.Value);
    }

    [Fact]
    public void ContextCreatesContextVariableNode()
    {
        NodeVariable node = Assert.IsType<NodeVariable>(Context().Inner);

        Assert.Equal("context", node.Name.Value);
    }

    [Fact]
    public void BooleanCreatesCedarBoolValueNode()
    {
        NodeValue node = Assert.IsType<NodeValue>(Boolean(true).Inner);

        CedarBool value = Assert.IsType<CedarBool>(node.Value);
        Assert.True(value.Value);
    }

    [Fact]
    public void StringCreatesCedarStringValueNode()
    {
        NodeValue node = Assert.IsType<NodeValue>(String("hello").Inner);

        CedarString value = Assert.IsType<CedarString>(node.Value);
        Assert.Equal("hello", value.Value);
    }

    [Fact]
    public void LongCreatesCedarLongValueNode()
    {
        NodeValue node = Assert.IsType<NodeValue>(Long(42).Inner);

        CedarLong value = Assert.IsType<CedarLong>(node.Value);
        Assert.Equal(42, value.Value);
    }

    [Fact]
    public void SetCreatesNodeSet()
    {
        NodeSet node = Assert.IsType<NodeSet>(Set(Long(1), Long(2)).Inner);

        Assert.Equal(2, node.Elements.Length);
        Assert.All(node.Elements, element => Assert.IsType<NodeValue>(element));
    }

    [Fact]
    public void RecordCreatesNodeRecord()
    {
        NodeRecord node = Assert.IsType<NodeRecord>(Record(
            new Values.RecordElement("x", Long(1)),
            new Values.RecordElement("y", Resource())).Inner);

        Assert.Equal(2, node.Elements.Length);
        Assert.Equal("x", node.Elements[0].Key.Value);
        Assert.Equal("y", node.Elements[1].Key.Value);
        Assert.IsType<NodeValue>(node.Elements[0].Value);
        Assert.IsType<NodeVariable>(node.Elements[1].Value);
    }

    [Fact]
    public void RecordUsesLastValueForDuplicateKey()
    {
        NodeRecord node = Assert.IsType<NodeRecord>(Record(
            new Values.RecordElement("x", Long(1)),
            new Values.RecordElement("x", Long(2))).Inner);

        Assert.Single(node.Elements);
        NodeValue value = Assert.IsType<NodeValue>(node.Elements[0].Value);
        CedarLong longValue = Assert.IsType<CedarLong>(value.Value);
        Assert.Equal(2, longValue.Value);
    }

    [Fact]
    public void EntityUidFromStringsCreatesEntityUidValueNode()
    {
        NodeValue node = Assert.IsType<NodeValue>(EntityUid("User", "alice").Inner);

        EntityUid uid = Assert.IsType<EntityUid>(node.Value);
        Assert.Equal("User", uid.Type.Value);
        Assert.Equal("alice", uid.Id.Value);
    }

    [Fact]
    public void EntityUidFromTypedArgumentsCreatesEntityUidValueNode()
    {
        NodeValue node = Assert.IsType<NodeValue>(EntityUid(new EntityType("Team"), "eng").Inner);

        EntityUid uid = Assert.IsType<EntityUid>(node.Value);
        Assert.Equal("Team", uid.Type.Value);
        Assert.Equal("eng", uid.Id.Value);
    }

    [Fact]
    public void EntityUidFromEntityCreatesEntityUidValueNode()
    {
        EntityUid uid = new(new EntityType("Repo"), new CedarString("cedar-dotnet"));

        NodeValue node = Assert.IsType<NodeValue>(EntityUid(uid).Inner);

        Assert.Equal(uid, node.Value);
    }

    [Fact]
    public void IpAddrCreatesIpValueNode()
    {
        NodeValue node = Assert.IsType<NodeValue>(IpAddr("127.0.0.1/16").Inner);

        CedarIpAddress ip = Assert.IsType<CedarIpAddress>(node.Value);
        Assert.Equal("ip(" + '"' + "127.0.0.1/16" + '"' + ")", ip.MarshalCedar());
    }

    [Fact]
    public void DecimalFromStringCreatesDecimalValueNode()
    {
        NodeValue node = Assert.IsType<NodeValue>(Decimal("12.34").Inner);

        CedarDecimal value = Assert.IsType<CedarDecimal>(node.Value);
        Assert.Equal("decimal(" + '"' + "12.34" + '"' + ")", value.MarshalCedar());
    }

    [Fact]
    public void DecimalFromPartsCreatesDecimalValueNode()
    {
        NodeValue node = Assert.IsType<NodeValue>(Decimal(1234, -2).Inner);

        CedarDecimal value = Assert.IsType<CedarDecimal>(node.Value);
        Assert.Equal("decimal(" + '"' + "12.34" + '"' + ")", value.MarshalCedar());
    }

    [Fact]
    public void DatetimeFromStringCreatesDatetimeValueNode()
    {
        NodeValue node = Assert.IsType<NodeValue>(Datetime("2020-01-02T03:04:05.006Z").Inner);

        CedarDatetime value = Assert.IsType<CedarDatetime>(node.Value);
        Assert.Equal("datetime(" + '"' + "2020-01-02T03:04:05.006Z" + '"' + ")", value.MarshalCedar());
    }

    [Fact]
    public void DatetimeFromExpandedYearStringCreatesDatetimeValueNode()
    {
        NodeValue node = Assert.IsType<NodeValue>(Datetime("+000010000-01-01T00:00:00.000Z").Inner);

        CedarDatetime value = Assert.IsType<CedarDatetime>(node.Value);
        Assert.Equal("datetime(" + '"' + "+000010000-01-01T00:00:00.000Z" + '"' + ")", value.MarshalCedar());
    }

    [Fact]
    public void DatetimeFromDateTimeOffsetCreatesDatetimeValueNode()
    {
        DateTimeOffset timestamp = new(2020, 1, 2, 3, 4, 5, 6, TimeSpan.Zero);

        NodeValue node = Assert.IsType<NodeValue>(Datetime(timestamp).Inner);

        CedarDatetime value = Assert.IsType<CedarDatetime>(node.Value);
        Assert.Equal("datetime(" + '"' + "2020-01-02T03:04:05.006Z" + '"' + ")", value.MarshalCedar());
    }

    [Fact]
    public void DurationFromStringCreatesDurationValueNode()
    {
        NodeValue node = Assert.IsType<NodeValue>(Duration("1h30m").Inner);

        CedarDuration value = Assert.IsType<CedarDuration>(node.Value);
        Assert.Equal("duration(" + '"' + "1h30m" + '"' + ")", value.MarshalCedar());
    }

    [Fact]
    public void DurationFromTimeSpanCreatesDurationValueNode()
    {
        NodeValue node = Assert.IsType<NodeValue>(Duration(TimeSpan.FromSeconds(90)).Inner);

        CedarDuration value = Assert.IsType<CedarDuration>(node.Value);
        Assert.Equal("duration(" + '"' + "1m30s" + '"' + ")", value.MarshalCedar());
    }
}

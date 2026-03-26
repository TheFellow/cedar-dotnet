using Cedar.Ast;
using Cedar.Ast.Internal;
using Cedar.Types;
using Xunit;
using static Cedar.Ast.ExtensionOperators;
using static Cedar.Ast.Values;
using static Cedar.Ast.Variables;

namespace Cedar.Tests.Ast;

public sealed class OperatorTests
{
    [Fact]
    public void EqualCreatesNodeEquals()
    {
        NodeEquals node = Assert.IsType<NodeEquals>(Long(1).Equal(Long(2)).Inner);

        Assert.IsType<NodeValue>(node.Left);
        Assert.IsType<NodeValue>(node.Right);
    }

    [Fact]
    public void NotEqualCreatesNodeNotEquals()
    {
        Assert.IsType<NodeNotEquals>(Long(1).NotEqual(Long(2)).Inner);
    }

    [Fact]
    public void LessThanCreatesNodeLessThan()
    {
        Assert.IsType<NodeLessThan>(Long(1).LessThan(Long(2)).Inner);
    }

    [Fact]
    public void LessThanOrEqualCreatesNodeLessThanOrEqual()
    {
        Assert.IsType<NodeLessThanOrEqual>(Long(1).LessThanOrEqual(Long(2)).Inner);
    }

    [Fact]
    public void GreaterThanCreatesNodeGreaterThan()
    {
        Assert.IsType<NodeGreaterThan>(Long(1).GreaterThan(Long(2)).Inner);
    }

    [Fact]
    public void GreaterThanOrEqualCreatesNodeGreaterThanOrEqual()
    {
        Assert.IsType<NodeGreaterThanOrEqual>(Long(1).GreaterThanOrEqual(Long(2)).Inner);
    }

    [Fact]
    public void AndCreatesNodeAnd()
    {
        Assert.IsType<NodeAnd>(Boolean(true).And(Boolean(false)).Inner);
    }

    [Fact]
    public void OrCreatesNodeOr()
    {
        Assert.IsType<NodeOr>(Boolean(true).Or(Boolean(false)).Inner);
    }

    [Fact]
    public void NotCreatesNodeNot()
    {
        Assert.IsType<NodeNot>(Boolean(true).Not().Inner);
    }

    [Fact]
    public void NegateCreatesNodeNegate()
    {
        Assert.IsType<NodeNegate>(Long(1).Negate().Inner);
    }

    [Fact]
    public void AddCreatesNodeAdd()
    {
        Assert.IsType<NodeAdd>(Long(1).Add(Long(2)).Inner);
    }

    [Fact]
    public void SubCreatesNodeSub()
    {
        Assert.IsType<NodeSub>(Long(1).Sub(Long(2)).Inner);
    }

    [Fact]
    public void MultCreatesNodeMult()
    {
        Assert.IsType<NodeMult>(Long(2).Mult(Long(3)).Inner);
    }

    [Fact]
    public void InCreatesNodeIn()
    {
        Assert.IsType<NodeIn>(Principal().In(Resource()).Inner);
    }

    [Fact]
    public void IsCreatesNodeIs()
    {
        NodeIs node = Assert.IsType<NodeIs>(Principal().Is("User").Inner);

        Assert.Equal("User", node.EntityType.Value);
    }

    [Fact]
    public void IsInCreatesNodeIsIn()
    {
        Assert.IsType<NodeIsIn>(Principal().IsIn("User", Resource()).Inner);
    }

    [Fact]
    public void HasCreatesNodeHas()
    {
        NodeHas node = Assert.IsType<NodeHas>(Principal().Has("owner").Inner);

        Assert.Equal("owner", node.Attribute.Value);
    }

    [Fact]
    public void HasTagCreatesNodeHasTag()
    {
        Assert.IsType<NodeHasTag>(Principal().HasTag(String("key")).Inner);
    }

    [Fact]
    public void LikeCreatesNodeLike()
    {
        CedarPattern pattern = CedarPattern.Parse("ab*");

        NodeLike node = Assert.IsType<NodeLike>(String("abc").Like(pattern).Inner);

        Assert.Equal(pattern, node.Pattern);
    }

    [Fact]
    public void AccessCreatesNodeAccess()
    {
        NodeAccess node = Assert.IsType<NodeAccess>(Resource().Access("owner").Inner);

        Assert.Equal("owner", node.Attribute.Value);
    }

    [Fact]
    public void GetTagCreatesNodeGetTag()
    {
        Assert.IsType<NodeGetTag>(Resource().GetTag(String("env")).Inner);
    }

    [Fact]
    public void ContainsCreatesNodeContains()
    {
        Assert.IsType<NodeContains>(Set(Long(1), Long(2)).Contains(Long(1)).Inner);
    }

    [Fact]
    public void ContainsAllCreatesNodeContainsAll()
    {
        Assert.IsType<NodeContainsAll>(Set(Long(1), Long(2)).ContainsAll(Set(Long(1))).Inner);
    }

    [Fact]
    public void ContainsAnyCreatesNodeContainsAny()
    {
        Assert.IsType<NodeContainsAny>(Set(Long(1), Long(2)).ContainsAny(Set(Long(2))).Inner);
    }

    [Fact]
    public void IsEmptyCreatesNodeIsEmpty()
    {
        Assert.IsType<NodeIsEmpty>(Set().IsEmpty().Inner);
    }

    [Fact]
    public void IfThenElseCreatesNodeIfThenElse()
    {
        Assert.IsType<NodeIfThenElse>(Boolean(true).IfThenElse(Long(1), Long(2)).Inner);
    }

    [Fact]
    public void DecimalComparisonOperatorsCreateExtensionCalls()
    {
        AssertExtensionCall(Long(1).LessThanDecimal(Long(2)), "lessThan", 2);
        AssertExtensionCall(Long(1).LessThanOrEqualDecimal(Long(2)), "lessThanOrEqual", 2);
        AssertExtensionCall(Long(1).GreaterThanDecimal(Long(2)), "greaterThan", 2);
        AssertExtensionCall(Long(1).GreaterThanOrEqualDecimal(Long(2)), "greaterThanOrEqual", 2);
    }

    [Fact]
    public void IpExtensionOperatorsCreateExtensionCalls()
    {
        AssertExtensionCall(IpAddr("127.0.0.1").IsIpv4(), "isIpv4", 1);
        AssertExtensionCall(IpAddr("::1").IsIpv6(), "isIpv6", 1);
        AssertExtensionCall(IpAddr("127.0.0.1").IsLoopback(), "isLoopback", 1);
        AssertExtensionCall(IpAddr("239.0.0.1").IsMulticast(), "isMulticast", 1);
        AssertExtensionCall(IpAddr("127.0.0.1").IsInRange(IpAddr("127.0.0.0/16")), "isInRange", 2);
    }

    [Fact]
    public void DatetimeExtensionOperatorsCreateExtensionCalls()
    {
        Node datetime = Datetime("2020-01-02T03:04:05.006Z");

        AssertExtensionCall(datetime.Offset(Duration("1h")), "offset", 2);
        AssertExtensionCall(datetime.DaysInMonth(), "daysInMonth", 1);
        AssertExtensionCall(datetime.Year(), "year", 1);
        AssertExtensionCall(datetime.Month(), "month", 1);
        AssertExtensionCall(datetime.Day(), "day", 1);
        AssertExtensionCall(datetime.DayOfWeek(), "dayOfWeek", 1);
        AssertExtensionCall(datetime.DayOfYear(), "dayOfYear", 1);
        AssertExtensionCall(datetime.Hour(), "hour", 1);
        AssertExtensionCall(datetime.Minute(), "minute", 1);
        AssertExtensionCall(datetime.Second(), "second", 1);
        AssertExtensionCall(datetime.Millisecond(), "millisecond", 1);
        AssertExtensionCall(datetime.ToDate(), "toDate", 1);
        AssertExtensionCall(datetime.ToTime(), "toTime", 1);
    }

    [Fact]
    public void DurationExtensionOperatorsCreateExtensionCalls()
    {
        Node duration = Duration("1d2h3m4s5ms");

        AssertExtensionCall(duration.ToDays(), "toDays", 1);
        AssertExtensionCall(duration.ToHours(), "toHours", 1);
        AssertExtensionCall(duration.ToMinutes(), "toMinutes", 1);
        AssertExtensionCall(duration.ToSeconds(), "toSeconds", 1);
        AssertExtensionCall(duration.ToMilliseconds(), "toMilliseconds", 1);
    }

    [Fact]
    public void ExtensionValueWrappersCreateSingleArgumentExtensionCalls()
    {
        AssertExtensionCall(Decimal(String("1.25")), "decimal", 1);
        AssertExtensionCall(Ip(String("127.0.0.1")), "ip", 1);
        AssertExtensionCall(Datetime(String("2020-01-02T03:04:05Z")), "datetime", 1);
        AssertExtensionCall(Duration(String("1h")), "duration", 1);
    }

    private static void AssertExtensionCall(Node node, string expectedName, int expectedArgs)
    {
        NodeExtensionCall call = Assert.IsType<NodeExtensionCall>(node.Inner);
        Assert.Equal(expectedName, call.Name);
        Assert.Equal(expectedArgs, call.Args.Length);
    }
}

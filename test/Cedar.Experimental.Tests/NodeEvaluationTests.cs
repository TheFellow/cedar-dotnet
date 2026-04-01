using System;
using System.Collections.Generic;
using Cedar.Ast;
using Cedar.Core;
using Cedar.Experimental;
using Cedar.Types;
using Xunit;

namespace Cedar.Experimental.Tests;

public sealed class NodeEvaluationTests
{
    private static readonly EntityUid Alice = new(new EntityType("User"), new CedarString("alice"));
    private static readonly EntityUid Read = new(new EntityType("Action"), new CedarString("read"));
    private static readonly EntityUid Doc1 = new(new EntityType("Document"), new CedarString("doc1"));
    private static readonly EntityUid Group = new(new EntityType("Group"), new CedarString("admins"));
    private static readonly EntityUid Tagged = new(new EntityType("Service"), new CedarString("svc"));

    [Fact]
    public void EvaluatesAttributeAccess()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Record(new Values.RecordElement("level", Values.Long(42))).Access("level"),
            new EvalEnv());

        Assert.Equal(new CedarLong(42), value);
    }

    [Fact]
    public void EvaluatesHasTag()
    {
        EntityMap entities = new(
        [
            new Entity(Tagged, new EntityUidSet(), new CedarRecord(), Record(("critical", new CedarBool(true))))
        ]);

        ICedarData value = NodeEvaluation.Evaluate(
            Values.EntityUid(Tagged).HasTag(Values.String("critical")),
            new EvalEnv(entities: entities));

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesIfThenElse()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Boolean(true).IfThenElse(Values.Long(1), Values.Long(2)),
            new EvalEnv());

        Assert.Equal(new CedarLong(1), value);
    }

    [Fact]
    public void EvaluatesIsInAgainstEntityHierarchy()
    {
        EntityMap entities = new(
        [
            new Entity(Alice, new EntityUidSet([Group]), new CedarRecord(), new CedarRecord())
        ]);

        ICedarData value = NodeEvaluation.Evaluate(
            Values.EntityUid(Alice).IsIn(new CedarPath("User"), Values.EntityUid(Group)),
            new EvalEnv(entities: entities));

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesPrincipalVariableFromEnvironment()
    {
        ICedarData value = NodeEvaluation.Evaluate(Variables.Principal(), new EvalEnv(principal: Alice));

        Assert.Equal(Alice, value);
    }

    [Fact]
    public void EvaluatesContextVariableFromEnvironment()
    {
        CedarRecord context = Record(("level", new CedarLong(7)));

        ICedarData value = NodeEvaluation.Evaluate(Variables.Context(), new EvalEnv(context: context));

        Assert.Equal(context, value);
    }

    [Fact]
    public void EvaluatesSetContains()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Set(Values.Long(1), Values.Long(2), Values.Long(3)).Contains(Values.Long(2)),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesExtensionCalls()
    {
        ICedarData value = NodeEvaluation.Evaluate(ExtensionOperators.Ip(Values.String("127.0.0.1/32")), new EvalEnv());

        Assert.Equal(CedarIpAddress.Parse("127.0.0.1/32"), value);
    }

    [Fact]
    public void UnknownExtensionThrows()
    {
        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(Operators.ExtensionCall("unknown", Values.String("x")), new EvalEnv()));

        Assert.Contains("function does not exist", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialErrorNodeThrowsStoredMessage()
    {
        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(PartialEvaluation.PartialError("boom"), new EvalEnv()));

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public void EvaluatesRecordConstruction()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Record(new Values.RecordElement("key", Values.Long(42))),
            new EvalEnv());

        CedarRecord record = Assert.IsType<CedarRecord>(value);
        Assert.True(record.TryGetValue(new CedarString("key"), out ICedarData actual));
        Assert.Equal(new CedarLong(42), actual);
    }

    [Fact]
    public void EvaluatesSetConstruction()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Set(Values.Long(42)),
            new EvalEnv());

        CedarSet set = Assert.IsType<CedarSet>(value);
        Assert.True(set.Contains(new CedarLong(42)));
    }

    [Fact]
    public void EvaluatesNegate()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Long(42).Negate(),
            new EvalEnv());

        Assert.Equal(new CedarLong(-42), value);
    }

    [Fact]
    public void EvaluatesNot()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Boolean(true).Not(),
            new EvalEnv());

        Assert.Equal(CedarBool.False, value);
    }

    [Fact]
    public void EvaluatesActionVariable()
    {
        ICedarData value = NodeEvaluation.Evaluate(Variables.Action(), new EvalEnv(action: Read));

        Assert.Equal(Read, value);
    }

    [Fact]
    public void EvaluatesResourceVariable()
    {
        ICedarData value = NodeEvaluation.Evaluate(Variables.Resource(), new EvalEnv(resource: Doc1));

        Assert.Equal(Doc1, value);
    }

    [Fact]
    public void EvaluatesValueNode()
    {
        ICedarData value = NodeEvaluation.Evaluate(Values.Long(42), new EvalEnv());

        Assert.Equal(new CedarLong(42), value);
    }

    [Fact]
    public void EvaluatesLikeOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.String("test").Like(new CedarPattern()),
            new EvalEnv());

        Assert.Equal(CedarBool.False, value);
    }

    [Fact]
    public void EvaluatesIsOperator()
    {
        EntityUid entity = new(new EntityType("T"), new CedarString("42"));

        ICedarData value = NodeEvaluation.Evaluate(
            Values.EntityUid(entity).Is("T"),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesIsInOperator()
    {
        EntityUid entity = new(new EntityType("T"), new CedarString("42"));

        ICedarData value = NodeEvaluation.Evaluate(
            Values.EntityUid(entity).IsIn(new CedarPath("T"), Values.EntityUid(entity)),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesInOperator()
    {
        EntityUid e1 = new(new EntityType("T"), new CedarString("42"));
        EntityUid e2 = new(new EntityType("T"), new CedarString("43"));

        ICedarData value = NodeEvaluation.Evaluate(
            Values.EntityUid(e1).In(Values.EntityUid(e2)),
            new EvalEnv());

        Assert.Equal(CedarBool.False, value);
    }

    [Fact]
    public void EvaluatesAndOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Boolean(true).And(Values.Boolean(false)),
            new EvalEnv());

        Assert.Equal(CedarBool.False, value);
    }

    [Fact]
    public void EvaluatesOrOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Boolean(true).Or(Values.Boolean(false)),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesEqualsOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Long(42).Equal(Values.Long(43)),
            new EvalEnv());

        Assert.Equal(CedarBool.False, value);
    }

    [Fact]
    public void EvaluatesNotEqualsOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Long(42).NotEqual(Values.Long(43)),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesGreaterThanOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Long(42).GreaterThan(Values.Long(43)),
            new EvalEnv());

        Assert.Equal(CedarBool.False, value);
    }

    [Fact]
    public void EvaluatesGreaterThanOrEqualOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Long(42).GreaterThanOrEqual(Values.Long(43)),
            new EvalEnv());

        Assert.Equal(CedarBool.False, value);
    }

    [Fact]
    public void EvaluatesLessThanOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Long(42).LessThan(Values.Long(43)),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesLessThanOrEqualOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Long(42).LessThanOrEqual(Values.Long(43)),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesSubtractOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Long(42).Sub(Values.Long(2)),
            new EvalEnv());

        Assert.Equal(new CedarLong(40), value);
    }

    [Fact]
    public void EvaluatesAddOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Long(40).Add(Values.Long(2)),
            new EvalEnv());

        Assert.Equal(new CedarLong(42), value);
    }

    [Fact]
    public void EvaluatesMultiplyOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Long(6).Mult(Values.Long(7)),
            new EvalEnv());

        Assert.Equal(new CedarLong(42), value);
    }

    [Fact]
    public void EvaluatesContainsAllOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Set(Values.Long(42), Values.Long(43), Values.Long(44))
                .ContainsAll(Values.Set(Values.Long(42), Values.Long(43))),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesContainsAnyOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Set(Values.Long(42), Values.Long(43), Values.Long(44))
                .ContainsAny(Values.Set(Values.Long(1), Values.Long(42))),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesIsEmptyOperator()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Set(Values.Long(42), Values.Long(43), Values.Long(44)).IsEmpty(),
            new EvalEnv());

        Assert.Equal(CedarBool.False, value);
    }

    [Fact]
    public void EvaluatesDecimalExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            ExtensionOperators.Decimal(Values.String("42.42")),
            new EvalEnv());

        Assert.Equal(CedarDecimal.Parse("42.42"), value);
    }

    [Fact]
    public void EvaluatesDatetimeExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            ExtensionOperators.Datetime(Values.String("1970-01-01T00:00:00.001Z")),
            new EvalEnv());

        Assert.IsType<CedarDatetime>(value);
    }

    [Fact]
    public void EvaluatesDurationExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            ExtensionOperators.Duration(Values.String("1ms")),
            new EvalEnv());

        Assert.IsType<CedarDuration>(value);
    }

    [Fact]
    public void EvaluatesToDateExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Datetime("1970-01-01T00:00:00.001Z").ToDate(),
            new EvalEnv());

        Assert.IsType<CedarDatetime>(value);
    }

    [Fact]
    public void EvaluatesToTimeExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Datetime("1970-01-01T00:00:00.001Z").ToTime(),
            new EvalEnv());

        Assert.IsType<CedarDuration>(value);
    }

    [Fact]
    public void EvaluatesToDaysExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Duration(TimeSpan.Zero).ToDays(),
            new EvalEnv());

        Assert.Equal(new CedarLong(0), value);
    }

    [Fact]
    public void EvaluatesToHoursExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Duration(TimeSpan.Zero).ToHours(),
            new EvalEnv());

        Assert.Equal(new CedarLong(0), value);
    }

    [Fact]
    public void EvaluatesToMinutesExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Duration(TimeSpan.Zero).ToMinutes(),
            new EvalEnv());

        Assert.Equal(new CedarLong(0), value);
    }

    [Fact]
    public void EvaluatesToSecondsExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Duration(TimeSpan.Zero).ToSeconds(),
            new EvalEnv());

        Assert.Equal(new CedarLong(0), value);
    }

    [Fact]
    public void EvaluatesToMillisecondsExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Duration(TimeSpan.Zero).ToMilliseconds(),
            new EvalEnv());

        Assert.Equal(new CedarLong(0), value);
    }

    [Fact]
    public void EvaluatesOffsetExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Datetime(DateTimeOffset.UnixEpoch).Offset(Values.Duration(TimeSpan.FromMilliseconds(1))),
            new EvalEnv());

        Assert.IsType<CedarDatetime>(value);
    }

    [Fact]
    public void EvaluatesDurationSinceExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Datetime(DateTimeOffset.UnixEpoch).DurationSince(Values.Datetime(DateTimeOffset.UnixEpoch)),
            new EvalEnv());

        Assert.IsType<CedarDuration>(value);
    }

    [Fact]
    public void EvaluatesDecimalLessThanExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Decimal("42.0").LessThanDecimal(Values.Decimal("43.0")),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesDecimalLessThanOrEqualExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Decimal("42.0").LessThanOrEqualDecimal(Values.Decimal("43.0")),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesDecimalGreaterThanExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Decimal("42.0").GreaterThanDecimal(Values.Decimal("43.0")),
            new EvalEnv());

        Assert.Equal(CedarBool.False, value);
    }

    [Fact]
    public void EvaluatesDecimalGreaterThanOrEqualExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Decimal("42.0").GreaterThanOrEqualDecimal(Values.Decimal("43.0")),
            new EvalEnv());

        Assert.Equal(CedarBool.False, value);
    }

    [Fact]
    public void EvaluatesIsIpv4Extension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.IpAddr("127.0.0.42/16").IsIpv4(),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesIsIpv6Extension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.IpAddr("::1/16").IsIpv6(),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesIsLoopbackExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.IpAddr("127.0.0.1/32").IsLoopback(),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesIsMulticastExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.IpAddr("239.255.255.255/32").IsMulticast(),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesIsInRangeExtension()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.IpAddr("127.0.0.42/32").IsInRange(Values.IpAddr("127.0.0.0/16")),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesHas()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Record(new Values.RecordElement("key", Values.Long(42))).Has("key"),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesGetTag()
    {
        EntityMap entities = new(
        [
            new Entity(Tagged, new EntityUidSet(), new CedarRecord(), Record(("key", new CedarLong(42))))
        ]);

        ICedarData value = NodeEvaluation.Evaluate(
            Values.EntityUid(Tagged).GetTag(Values.String("key")),
            new EvalEnv(entities: entities));

        Assert.Equal(new CedarLong(42), value);
    }

    private static CedarRecord Record(params (string Key, ICedarData Value)[] entries)
    {
        Dictionary<CedarString, ICedarData> result = [];
        foreach ((string key, ICedarData value) in entries)
        {
            result.Add(new CedarString(key), value);
        }

        return new CedarRecord(result);
    }
}

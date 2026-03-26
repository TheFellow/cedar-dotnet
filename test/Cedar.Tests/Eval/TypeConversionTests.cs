using System.Collections.Generic;
using System.Net;
using Cedar.Core.Internal.Eval;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Eval;

public sealed class TypeConversionTests
{
    [Fact]
    public void ValueToBool_True_ReturnsTrue()
    {
        Assert.True(TypeConversion.ValueToBool(CedarBool.True));
    }

    [Fact]
    public void ValueToBool_False_ReturnsFalse()
    {
        Assert.False(TypeConversion.ValueToBool(CedarBool.False));
    }

    [Fact]
    public void ValueToBool_WrongType_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => TypeConversion.ValueToBool(new CedarLong(1)));
    }

    [Fact]
    public void ValueToLong_Positive_ReturnsValue()
    {
        Assert.Equal(42L, TypeConversion.ValueToLong(new CedarLong(42)));
    }

    [Fact]
    public void ValueToLong_Negative_ReturnsValue()
    {
        Assert.Equal(-7L, TypeConversion.ValueToLong(new CedarLong(-7)));
    }

    [Fact]
    public void ValueToLong_Zero_ReturnsZero()
    {
        Assert.Equal(0L, TypeConversion.ValueToLong(new CedarLong(0)));
    }

    [Fact]
    public void ValueToLong_WrongType_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => TypeConversion.ValueToLong(CedarBool.True));
    }

    [Fact]
    public void ValueToString_Normal_ReturnsValue()
    {
        Assert.Equal("hello", TypeConversion.ValueToString(new CedarString("hello")));
    }

    [Fact]
    public void ValueToString_Empty_ReturnsEmpty()
    {
        Assert.Equal("", TypeConversion.ValueToString(new CedarString("")));
    }

    [Fact]
    public void ValueToString_WrongType_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => TypeConversion.ValueToString(new CedarLong(1)));
    }

    [Fact]
    public void ValueToSet_Normal_ReturnsSet()
    {
        CedarSet set = new(new CedarLong(1), new CedarLong(2));
        CedarSet result = TypeConversion.ValueToSet(set);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ValueToSet_WrongType_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => TypeConversion.ValueToSet(new CedarLong(1)));
    }

    [Fact]
    public void ValueToRecord_Normal_ReturnsRecord()
    {
        CedarRecord record = new(new Dictionary<CedarString, ICedarData>
        {
            { new CedarString("key"), new CedarLong(1) }
        });
        CedarRecord result = TypeConversion.ValueToRecord(record);
        Assert.True(result.TryGetValue(new CedarString("key"), out _));
    }

    [Fact]
    public void ValueToRecord_WrongType_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => TypeConversion.ValueToRecord(CedarBool.True));
    }

    [Fact]
    public void ValueToEntity_Normal_ReturnsEntity()
    {
        EntityUid uid = new(new EntityType("User"), new CedarString("alice"));
        EntityUid result = TypeConversion.ValueToEntity(uid);
        Assert.Equal(uid, result);
    }

    [Fact]
    public void ValueToEntity_WrongType_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => TypeConversion.ValueToEntity(new CedarLong(1)));
    }

    [Fact]
    public void ValueToDecimal_Normal_ReturnsDecimal()
    {
        CedarDecimal dec = CedarDecimal.Parse("1.5");
        CedarDecimal result = TypeConversion.ValueToDecimal(dec);
        Assert.Equal(dec, result);
    }

    [Fact]
    public void ValueToDecimal_WrongType_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => TypeConversion.ValueToDecimal(new CedarLong(1)));
    }

    [Fact]
    public void ValueToDatetime_Normal_ReturnsDatetime()
    {
        CedarDatetime dt = CedarDatetime.Parse("2024-01-01T00:00:00Z");
        CedarDatetime result = TypeConversion.ValueToDatetime(dt);
        Assert.Equal(dt, result);
    }

    [Fact]
    public void ValueToDatetime_WrongType_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => TypeConversion.ValueToDatetime(CedarBool.True));
    }

    [Fact]
    public void ValueToDuration_Normal_ReturnsDuration()
    {
        CedarDuration dur = CedarDuration.Parse("1h30m");
        CedarDuration result = TypeConversion.ValueToDuration(dur);
        Assert.Equal(dur, result);
    }

    [Fact]
    public void ValueToDuration_WrongType_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => TypeConversion.ValueToDuration(new CedarString("nope")));
    }

    [Fact]
    public void ValueToIp_Normal_ReturnsIp()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("127.0.0.1");
        CedarIpAddress result = TypeConversion.ValueToIp(ip);
        Assert.Equal(ip, result);
    }

    [Fact]
    public void ValueToIp_WrongType_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => TypeConversion.ValueToIp(new CedarLong(1)));
    }
}

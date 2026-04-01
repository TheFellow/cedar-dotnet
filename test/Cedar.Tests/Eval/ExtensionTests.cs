using System;
using Cedar.Core.Internal.Eval;
using Cedar.Core.Internal.Extensions;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Eval;

public sealed class ExtensionTests
{
    private static ICedarData Invoke(string name, params ICedarData[] args)
    {
        return ExtensionRegistry.Invoke(name, args);
    }

    // --- Constructor extensions ---

    [Fact]
    public void Decimal_ValidString_ReturnsDecimal()
    {
        ICedarData result = Invoke("decimal", new CedarString("1.0"));
        Assert.IsType<CedarDecimal>(result);
    }

    [Fact]
    public void Decimal_NegativeValue_ReturnsDecimal()
    {
        ICedarData result = Invoke("decimal", new CedarString("-3.14"));
        Assert.IsType<CedarDecimal>(result);
    }

    [Fact]
    public void Ip_ValidIpv4_ReturnsIp()
    {
        ICedarData result = Invoke("ip", new CedarString("127.0.0.1"));
        Assert.IsType<CedarIpAddress>(result);
    }

    [Fact]
    public void Ip_ValidIpv6_ReturnsIp()
    {
        ICedarData result = Invoke("ip", new CedarString("::1"));
        Assert.IsType<CedarIpAddress>(result);
    }

    [Fact]
    public void Datetime_ValidString_ReturnsDatetime()
    {
        ICedarData result = Invoke("datetime", new CedarString("2024-01-01T00:00:00Z"));
        Assert.IsType<CedarDatetime>(result);
    }

    [Fact]
    public void Duration_ValidString_ReturnsDuration()
    {
        ICedarData result = Invoke("duration", new CedarString("1h30m"));
        Assert.IsType<CedarDuration>(result);
    }

    [Fact]
    public void Duration_MillisecondsOnly_ReturnsDuration()
    {
        ICedarData result = Invoke("duration", new CedarString("500ms"));
        CedarDuration duration = Assert.IsType<CedarDuration>(result);
        Assert.Equal(500L, duration.Value);
    }

    // --- Decimal comparisons ---

    [Fact]
    public void LessThan_True()
    {
        CedarDecimal left = CedarDecimal.Parse("1.0");
        CedarDecimal right = CedarDecimal.Parse("2.0");
        Assert.Equal(CedarBool.True, Invoke("lessThan", left, right));
    }

    [Fact]
    public void LessThan_False()
    {
        CedarDecimal left = CedarDecimal.Parse("3.0");
        CedarDecimal right = CedarDecimal.Parse("2.0");
        Assert.Equal(CedarBool.False, Invoke("lessThan", left, right));
    }

    [Fact]
    public void LessThanOrEqual_Equal()
    {
        CedarDecimal val = CedarDecimal.Parse("5.0");
        Assert.Equal(CedarBool.True, Invoke("lessThanOrEqual", val, val));
    }

    [Fact]
    public void GreaterThan_True()
    {
        CedarDecimal left = CedarDecimal.Parse("3.0");
        CedarDecimal right = CedarDecimal.Parse("1.0");
        Assert.Equal(CedarBool.True, Invoke("greaterThan", left, right));
    }

    [Fact]
    public void GreaterThanOrEqual_Equal()
    {
        CedarDecimal val = CedarDecimal.Parse("5.0");
        Assert.Equal(CedarBool.True, Invoke("greaterThanOrEqual", val, val));
    }

    // --- IP functions ---

    [Fact]
    public void IsIpv4_Ipv4Address_ReturnsTrue()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("192.168.1.1");
        Assert.Equal(CedarBool.True, Invoke("isIpv4", ip));
    }

    [Fact]
    public void IsIpv4_Ipv6Address_ReturnsFalse()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("::1");
        Assert.Equal(CedarBool.False, Invoke("isIpv4", ip));
    }

    [Fact]
    public void IsIpv6_Ipv6Address_ReturnsTrue()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("::1");
        Assert.Equal(CedarBool.True, Invoke("isIpv6", ip));
    }

    [Fact]
    public void IsIpv6_Ipv4Address_ReturnsFalse()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("192.168.1.1");
        Assert.Equal(CedarBool.False, Invoke("isIpv6", ip));
    }

    [Fact]
    public void IsLoopback_Loopback_ReturnsTrue()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("127.0.0.1");
        Assert.Equal(CedarBool.True, Invoke("isLoopback", ip));
    }

    [Fact]
    public void IsLoopback_NonLoopback_ReturnsFalse()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("192.168.1.1");
        Assert.Equal(CedarBool.False, Invoke("isLoopback", ip));
    }

    [Fact]
    public void IsMulticast_MulticastIp_ReturnsTrue()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("224.0.0.1");
        Assert.Equal(CedarBool.True, Invoke("isMulticast", ip));
    }

    [Fact]
    public void IsMulticast_NonMulticast_ReturnsFalse()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("192.168.1.1");
        Assert.Equal(CedarBool.False, Invoke("isMulticast", ip));
    }

    [Fact]
    public void IsInRange_InRange_ReturnsTrue()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("192.168.1.100");
        CedarIpAddress range = CedarIpAddress.Parse("192.168.1.0/24");
        Assert.Equal(CedarBool.True, Invoke("isInRange", ip, range));
    }

    [Fact]
    public void IsInRange_OutOfRange_ReturnsFalse()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("10.0.0.1");
        CedarIpAddress range = CedarIpAddress.Parse("192.168.1.0/24");
        Assert.Equal(CedarBool.False, Invoke("isInRange", ip, range));
    }

    // --- Datetime functions ---

    [Fact]
    public void ToDate_StripsTimeComponent()
    {
        CedarDatetime dt = CedarDatetime.Parse("2024-06-15T13:30:45Z");
        ICedarData result = Invoke("toDate", dt);
        CedarDatetime date = Assert.IsType<CedarDatetime>(result);
        System.DateTimeOffset dto = date.ToDateTimeOffset();
        Assert.Equal(0, dto.Hour);
        Assert.Equal(0, dto.Minute);
        Assert.Equal(0, dto.Second);
    }

    [Fact]
    public void ToTime_ReturnsTimeDuration()
    {
        CedarDatetime dt = CedarDatetime.Parse("2024-06-15T13:30:45Z");
        ICedarData result = Invoke("toTime", dt);
        CedarDuration duration = Assert.IsType<CedarDuration>(result);
        Assert.True(duration.Value > 0);
    }

    [Fact]
    public void Offset_AddsDuration()
    {
        CedarDatetime dt = CedarDatetime.Parse("2024-01-01T00:00:00Z");
        CedarDuration dur = CedarDuration.Parse("1h");
        ICedarData result = Invoke("offset", dt, dur);
        CedarDatetime offsetDt = Assert.IsType<CedarDatetime>(result);
        Assert.Equal(dt.Value + dur.Value, offsetDt.Value);
    }

    [Fact]
    public void Offset_ThrowsOnOverflow()
    {
        CedarDatetime dt = new(long.MaxValue);
        CedarDuration dur = new(1L);

        EvalException ex = Assert.Throws<EvalException>(() => Invoke("offset", dt, dur));

        Assert.Contains("overflow", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DurationSince_ComputesDifference()
    {
        CedarDatetime dt1 = CedarDatetime.Parse("2024-01-02T00:00:00Z");
        CedarDatetime dt2 = CedarDatetime.Parse("2024-01-01T00:00:00Z");
        ICedarData result = Invoke("durationSince", dt1, dt2);
        CedarDuration duration = Assert.IsType<CedarDuration>(result);
        Assert.Equal(86400000L, duration.Value);
    }

    [Fact]
    public void DurationSince_ThrowsOnOverflow()
    {
        CedarDatetime dt1 = new(long.MaxValue);
        CedarDatetime dt2 = new(long.MinValue);

        EvalException ex = Assert.Throws<EvalException>(() => Invoke("durationSince", dt1, dt2));

        Assert.Contains("overflow", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DatetimeComponentFunctionsSupportExpandedYearValues()
    {
        CedarDatetime dt = CedarDatetime.Parse("+000010000-06-15T13:30:45.006Z");

        Assert.Equal(new CedarLong(30), Invoke("daysInMonth", dt));
        Assert.Equal(new CedarLong(10000), Invoke("year", dt));
        Assert.Equal(new CedarLong(6), Invoke("month", dt));
        Assert.Equal(new CedarLong(15), Invoke("day", dt));
        Assert.Equal(new CedarLong(4), Invoke("dayOfWeek", dt));
        Assert.Equal(new CedarLong(167), Invoke("dayOfYear", dt));
        Assert.Equal(new CedarLong(13), Invoke("hour", dt));
        Assert.Equal(new CedarLong(30), Invoke("minute", dt));
        Assert.Equal(new CedarLong(45), Invoke("second", dt));
        Assert.Equal(new CedarLong(6), Invoke("millisecond", dt));
    }

    // --- Duration functions ---

    [Fact]
    public void ToDays_ReturnsDays()
    {
        CedarDuration dur = CedarDuration.Parse("2d");
        ICedarData result = Invoke("toDays", dur);
        CedarLong value = Assert.IsType<CedarLong>(result);
        Assert.Equal(2L, value.Value);
    }

    [Fact]
    public void ToHours_ReturnsHours()
    {
        CedarDuration dur = CedarDuration.Parse("3h");
        ICedarData result = Invoke("toHours", dur);
        CedarLong value = Assert.IsType<CedarLong>(result);
        Assert.Equal(3L, value.Value);
    }

    [Fact]
    public void ToMinutes_ReturnsMinutes()
    {
        CedarDuration dur = CedarDuration.Parse("2h30m");
        ICedarData result = Invoke("toMinutes", dur);
        CedarLong value = Assert.IsType<CedarLong>(result);
        Assert.Equal(150L, value.Value);
    }

    [Fact]
    public void ToSeconds_ReturnsSeconds()
    {
        CedarDuration dur = CedarDuration.Parse("1m30s");
        ICedarData result = Invoke("toSeconds", dur);
        CedarLong value = Assert.IsType<CedarLong>(result);
        Assert.Equal(90L, value.Value);
    }

    [Fact]
    public void ToMilliseconds_ReturnsMilliseconds()
    {
        CedarDuration dur = CedarDuration.Parse("1s");
        ICedarData result = Invoke("toMilliseconds", dur);
        CedarLong value = Assert.IsType<CedarLong>(result);
        Assert.Equal(1000L, value.Value);
    }

    // --- Error cases ---

    [Fact]
    public void UnknownFunction_ThrowsEvalException()
    {
        EvalException ex = Assert.Throws<EvalException>(() => Invoke("nonexistent"));
        Assert.Contains("function does not exist", ex.Message);
    }

    [Fact]
    public void WrongArity_ThrowsEvalException()
    {
        EvalException ex = Assert.Throws<EvalException>(() => Invoke("decimal", new CedarString("1.0"), new CedarString("2.0")));
        Assert.Contains("wrong number of arguments", ex.Message);
    }

    [Fact]
    public void WrongType_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => Invoke("isIpv4", new CedarString("not an ip")));
    }

    [Fact]
    public void Decimal_InvalidString_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => Invoke("decimal", new CedarString("notanumber")));
    }

    // --- Registry count (from Go TestExtensions: len(ExtMap)==22) ---
    // C# has additional datetime component extensions not present in Go.

    [Fact]
    public void ExtensionRegistry_ContainsAllExpectedFunctions()
    {
        // Verify all expected functions exist by invoking with valid args
        string[] expectedFunctions = new[]
        {
            "decimal", "ip", "datetime", "duration",
            "lessThan", "lessThanOrEqual", "greaterThan", "greaterThanOrEqual",
            "isIpv4", "isIpv6", "isLoopback", "isMulticast", "isInRange",
            "toDate", "toTime", "offset", "durationSince",
            "toDays", "toHours", "toMinutes", "toSeconds", "toMilliseconds",
            "daysInMonth", "year", "month", "day", "dayOfWeek", "dayOfYear",
            "hour", "minute", "second", "millisecond"
        };

        foreach (string name in expectedFunctions)
        {
            Assert.True(
                Cedar.Core.Internal.Extensions.ExtensionRegistry.TryGet(name, out _),
                $"Expected extension '{name}' to be registered");
        }
    }

    // --- IP with CIDR prefix (from Go TestToEval/ip test with /16) ---

    [Fact]
    public void Ip_WithCidrPrefix_ReturnsIp()
    {
        ICedarData result = Invoke("ip", new CedarString("127.0.0.42/16"));
        CedarIpAddress ip = Assert.IsType<CedarIpAddress>(result);
        Assert.NotNull(ip);
    }

    [Fact]
    public void Ip_InvalidString_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => Invoke("ip", new CedarString("not-an-IP-address")));
    }

    [Fact]
    public void Datetime_InvalidString_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => Invoke("datetime", new CedarString("not-a-datetime")));
    }

    [Fact]
    public void Duration_InvalidString_ThrowsEvalException()
    {
        Assert.Throws<EvalException>(() => Invoke("duration", new CedarString("not-a-duration")));
    }

    // --- IsIpv6 with Ipv4 loopback (from Go TestIPTestNode) ---

    [Fact]
    public void IsIpv6_Ipv4Loopback_ReturnsFalse()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("127.0.0.1");
        Assert.Equal(CedarBool.False, Invoke("isIpv6", ip));
    }

    // --- IsIpv4 with Ipv6 loopback (from Go TestIPTestNode) ---

    [Fact]
    public void IsIpv4_Ipv6Loopback_ReturnsFalse()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("::1");
        Assert.Equal(CedarBool.False, Invoke("isIpv4", ip));
    }

    // --- IsLoopback Ipv6 (from Go TestIPTestNode/LoopbackTrue with ipv6) ---

    [Fact]
    public void IsLoopback_Ipv6Loopback_ReturnsTrue()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("::1");
        Assert.Equal(CedarBool.True, Invoke("isLoopback", ip));
    }

    // --- IsMulticast false (from Go TestIPTestNode/MulticastFalse) ---

    [Fact]
    public void IsMulticast_Ipv6Loopback_ReturnsFalse()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("::1");
        Assert.Equal(CedarBool.False, Invoke("isMulticast", ip));
    }

    // --- IsInRange detailed cases (from Go TestIPIsInRangeNode) ---

    [Fact]
    public void IsInRange_SameIp_ReturnsTrue()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("1.2.3.4");
        Assert.Equal(CedarBool.True, Invoke("isInRange", ip, ip));
    }

    [Fact]
    public void IsInRange_RangeDoesNotContainIp_ReturnsFalse()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("1.2.3.4");
        CedarIpAddress range = CedarIpAddress.Parse("1.2.4.0/24");
        Assert.Equal(CedarBool.False, Invoke("isInRange", ip, range));
    }

    [Fact]
    public void IsInRange_SubnetContainsIp_ReturnsTrue()
    {
        CedarIpAddress ip = CedarIpAddress.Parse("1.2.3.4");
        CedarIpAddress range = CedarIpAddress.Parse("1.2.3.0/24");
        Assert.Equal(CedarBool.True, Invoke("isInRange", ip, range));
    }

    [Fact]
    public void IsInRange_IpDoesNotContainSubnet_ReturnsFalse()
    {
        CedarIpAddress subnet = CedarIpAddress.Parse("1.2.3.0/24");
        CedarIpAddress ip = CedarIpAddress.Parse("1.2.3.4");
        Assert.Equal(CedarBool.False, Invoke("isInRange", subnet, ip));
    }

    // --- Decimal comparisons with equal values (from Go TestDecimalLessThanNode etc.) ---

    [Fact]
    public void LessThan_Equal_ReturnsFalse()
    {
        CedarDecimal val = CedarDecimal.Parse("1.0");
        Assert.Equal(CedarBool.False, Invoke("lessThan", val, val));
    }

    [Fact]
    public void GreaterThan_Equal_ReturnsFalse()
    {
        CedarDecimal val = CedarDecimal.Parse("1.0");
        Assert.Equal(CedarBool.False, Invoke("greaterThan", val, val));
    }

    [Fact]
    public void LessThanOrEqual_Less_ReturnsTrue()
    {
        CedarDecimal left = CedarDecimal.Parse("1.0");
        CedarDecimal right = CedarDecimal.Parse("2.0");
        Assert.Equal(CedarBool.True, Invoke("lessThanOrEqual", left, right));
    }

    [Fact]
    public void GreaterThanOrEqual_Greater_ReturnsTrue()
    {
        CedarDecimal left = CedarDecimal.Parse("2.0");
        CedarDecimal right = CedarDecimal.Parse("1.0");
        Assert.Equal(CedarBool.True, Invoke("greaterThanOrEqual", left, right));
    }

    [Fact]
    public void GreaterThan_Less_ReturnsFalse()
    {
        CedarDecimal left = CedarDecimal.Parse("1.0");
        CedarDecimal right = CedarDecimal.Parse("2.0");
        Assert.Equal(CedarBool.False, Invoke("greaterThan", left, right));
    }

    [Fact]
    public void GreaterThanOrEqual_Less_ReturnsFalse()
    {
        CedarDecimal left = CedarDecimal.Parse("1.0");
        CedarDecimal right = CedarDecimal.Parse("2.0");
        Assert.Equal(CedarBool.False, Invoke("greaterThanOrEqual", left, right));
    }

    [Fact]
    public void LessThanOrEqual_Greater_ReturnsFalse()
    {
        CedarDecimal left = CedarDecimal.Parse("2.0");
        CedarDecimal right = CedarDecimal.Parse("1.0");
        Assert.Equal(CedarBool.False, Invoke("lessThanOrEqual", left, right));
    }

    // --- Duration conversions from 1d (from Go TestDurationTo* tests) ---

    [Fact]
    public void ToMilliseconds_OneDay_ReturnsCorrectValue()
    {
        CedarDuration dur = CedarDuration.Parse("1d");
        Assert.Equal(new CedarLong(24L * 60 * 60 * 1000), Invoke("toMilliseconds", dur));
    }

    [Fact]
    public void ToSeconds_OneDay_ReturnsCorrectValue()
    {
        CedarDuration dur = CedarDuration.Parse("1d");
        Assert.Equal(new CedarLong(24L * 60 * 60), Invoke("toSeconds", dur));
    }

    [Fact]
    public void ToMinutes_OneDay_ReturnsCorrectValue()
    {
        CedarDuration dur = CedarDuration.Parse("1d");
        Assert.Equal(new CedarLong(24L * 60), Invoke("toMinutes", dur));
    }

    [Fact]
    public void ToHours_OneDay_ReturnsCorrectValue()
    {
        CedarDuration dur = CedarDuration.Parse("1d");
        Assert.Equal(new CedarLong(24L), Invoke("toHours", dur));
    }

    // --- ToDate extracts date (from Go TestDatetimeToDate) ---

    [Fact]
    public void ToDate_ExtractsDatePortion()
    {
        CedarDatetime dt = CedarDatetime.Parse("1970-01-02T10:00:00Z");
        ICedarData result = Invoke("toDate", dt);
        CedarDatetime date = Assert.IsType<CedarDatetime>(result);
        // 1970-01-02 is 86400000 ms from epoch
        Assert.Equal(new CedarDatetime(86400000L), date);
    }

    // --- ToTime extracts time portion (from Go TestDatetimeToTime) ---

    [Fact]
    public void ToTime_ExtractsTimePortion()
    {
        CedarDatetime dt = CedarDatetime.Parse("1970-01-01T10:00:00Z");
        ICedarData result = Invoke("toTime", dt);
        CedarDuration duration = Assert.IsType<CedarDuration>(result);
        // 10 hours in ms
        Assert.Equal(10L * 60 * 60 * 1000, duration.Value);
    }
}

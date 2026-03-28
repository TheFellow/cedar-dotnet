using System;
using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class CedarDurationTests
{
    [Fact]
    public void ConstructorStoresMilliseconds()
    {
        CedarDuration value = new(42);

        Assert.Equal(42, value.Value);
    }

    [Fact]
    public void ParseAcceptsSingleUnit()
    {
        CedarAssert.CedarText(CedarDuration.Parse("1h"), "duration(\"1h\")");
    }

    [Fact]
    public void ParseCollapsesEquivalentUnits()
    {
        CedarAssert.CedarText(CedarDuration.Parse("60m"), "duration(\"1h\")");
    }

    [Fact]
    public void ParseAcceptsNegativeDurations()
    {
        CedarAssert.CedarText(CedarDuration.Parse("-36h"), "duration(\"-1d12h\")");
    }

    [Fact]
    public void ZeroFormatsAsMilliseconds()
    {
        CedarAssert.CedarText(new CedarDuration(0), "duration(\"0ms\")");
    }

    [Fact]
    public void UnitAccessorsReturnTruncatedValues()
    {
        CedarDuration duration = CedarDuration.Parse("1d2h31m43s17ms");

        Assert.Equal(1, duration.ToDays());
        Assert.Equal(26, duration.ToHours());
        Assert.Equal(1591, duration.ToMinutes());
        Assert.Equal(95503, duration.ToSeconds());
        Assert.Equal(95_503_017, duration.ToMilliseconds());
    }

    [Fact]
    public void FromTimeSpanCreatesFromMilliseconds()
    {
        CedarDuration result = CedarDuration.FromTimeSpan(TimeSpan.FromMilliseconds(1000));

        Assert.Equal(new CedarDuration(1000), result);
    }

    [Fact]
    public void FromTimeSpanRoundTripsWithToTimeSpan()
    {
        CedarDuration original = new(42);

        CedarDuration roundTripped = CedarDuration.FromTimeSpan(original.ToTimeSpan());

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void ParseRejectsEmptyInput()
    {
        Assert.Throws<FormatException>(() => CedarDuration.Parse(""));
    }

    [Fact]
    public void ParseRejectsUnexpectedUnitOrder()
    {
        Assert.Throws<FormatException>(() => CedarDuration.Parse("1h1h"));
    }

    [Fact]
    public void ParseRejectsMissingTrailingUnit()
    {
        Assert.Throws<FormatException>(() => CedarDuration.Parse("3h3"));
    }

    [Fact]
    public void EqualValuesAreEqual()
    {
        CedarAssert.Equal(new CedarDuration(3_600_000), CedarDuration.Parse("60m"));
    }

    [Fact]
    public void DifferentValuesAreNotEqual()
    {
        CedarAssert.NotEqual(new CedarDuration(1), new CedarDuration(2));
    }

    [Fact]
    public void HashCodeIsStable()
    {
        CedarAssert.HashStable(CedarDuration.Parse("42ms"));
    }

    [Fact]
    public void JsonRoundTripUsesDurationExtension()
    {
        CedarDuration expected = CedarDuration.Parse("42ms");

        string json = CedarJson.SerializeData(expected);
        ICedarData actual = CedarJson.DeserializeData(json);

        Assert.Equal("{\"__extn\":{\"fn\":\"duration\",\"arg\":\"42ms\"}}", json);
        CedarAssert.Equal(expected, Assert.IsType<CedarDuration>(actual));
    }

    [Fact]
    public void ParseRejectsDigitAccumulationOverflow()
    {
        Assert.Throws<FormatException>(() => CedarDuration.Parse("99999999999999999999ms"));
    }

    [Fact]
    public void ParseRejectsUnitMultiplicationOverflow()
    {
        Assert.Throws<FormatException>(() => CedarDuration.Parse("106751991168d"));
    }

    [Fact]
    public void ParseRejectsTotalAccumulationOverflow()
    {
        Assert.Throws<FormatException>(() => CedarDuration.Parse("106751991167d8h"));
    }

    [Fact]
    public void ParseAcceptsMaxValidDuration()
    {
        CedarDuration duration = CedarDuration.Parse("106751991167d7h12m55s807ms");

        Assert.Equal(long.MaxValue, duration.Value);
    }

    [Fact]
    public void ParseAcceptsValueExceedingInt32Max()
    {
        CedarDuration duration = CedarDuration.Parse("2147483648ms");

        Assert.Equal(2_147_483_648L, duration.Value);
    }

    [Fact]
    public void ParseRejectsExactInt64MaxPlusOne()
    {
        Assert.Throws<FormatException>(() => CedarDuration.Parse("9223372036854775808ms"));
    }

    [Fact]
    public void ParseRejectsTotalAccumulationOverflowAtBoundary()
    {
        Assert.Throws<FormatException>(() => CedarDuration.Parse("106751991167d7h12m55s808ms"));
    }
}

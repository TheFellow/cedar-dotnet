using System;
using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class CedarDatetimeTests
{
    [Fact]
    public void ConstructorStoresMilliseconds()
    {
        CedarDatetime value = new(42);

        Assert.Equal(42, value.Value);
    }

    [Fact]
    public void ParseAcceptsDateOnlyForm()
    {
        CedarAssert.CedarText(CedarDatetime.Parse("1970-01-01"), "datetime(\"1970-01-01T00:00:00.000Z\")");
    }

    [Fact]
    public void ParseAcceptsZuluTime()
    {
        CedarAssert.CedarText(CedarDatetime.Parse("1970-01-01T01:01:01Z"), "datetime(\"1970-01-01T01:01:01.000Z\")");
    }

    [Fact]
    public void ParseAcceptsMillisecondPrecision()
    {
        CedarAssert.CedarText(CedarDatetime.Parse("1970-01-01T00:00:00.042Z"), "datetime(\"1970-01-01T00:00:00.042Z\")");
    }

    [Fact]
    public void ParseAddsPositiveOffset()
    {
        CedarAssert.CedarText(CedarDatetime.Parse("1970-01-01T00:00:00+0100"), "datetime(\"1970-01-01T01:00:00.000Z\")");
    }

    [Fact]
    public void ParseAppliesNegativeOffset()
    {
        CedarAssert.CedarText(CedarDatetime.Parse("1970-01-01T01:00:00-0100"), "datetime(\"1970-01-01T00:00:00.000Z\")");
    }

    [Fact]
    public void ParseRejectsShortInput()
    {
        Assert.Throws<FormatException>(() => CedarDatetime.Parse("1970-01"));
    }

    [Fact]
    public void ParseRejectsInvalidDate()
    {
        Assert.Throws<FormatException>(() => CedarDatetime.Parse("2024-02-30T00:00:00Z"));
    }

    [Fact]
    public void ParseRejectsInvalidTimeZone()
    {
        Assert.Throws<FormatException>(() => CedarDatetime.Parse("1970-01-01T00:00:00V"));
    }

    [Fact]
    public void EqualValuesAreEqual()
    {
        CedarAssert.Equal(new CedarDatetime(42), CedarDatetime.Parse("1970-01-01T00:00:00.042Z"));
    }

    [Fact]
    public void DifferentValuesAreNotEqual()
    {
        CedarAssert.NotEqual(new CedarDatetime(42), new CedarDatetime(43));
    }

    [Fact]
    public void HashCodeIsStable()
    {
        CedarAssert.HashStable(new CedarDatetime(1234));
    }

    [Fact]
    public void JsonRoundTripUsesDatetimeExtension()
    {
        CedarDatetime expected = CedarDatetime.Parse("1970-01-01T00:00:00.042Z");

        string json = CedarJson.SerializeData(expected);
        ICedarData actual = CedarJson.DeserializeData(json);

        Assert.Equal("{\"__extn\":{\"fn\":\"datetime\",\"arg\":\"1970-01-01T00:00:00.042Z\"}}", json);
        CedarAssert.Equal(expected, Assert.IsType<CedarDatetime>(actual));
    }
}

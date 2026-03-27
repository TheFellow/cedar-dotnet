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

    [Theory]
    [InlineData("0000-01-01", "datetime(\"0000-01-01T00:00:00.000Z\")")]
    [InlineData("+000000010-01-01", "datetime(\"0010-01-01T00:00:00.000Z\")")]
    [InlineData("+000001970-06-15", "datetime(\"1970-06-15T00:00:00.000Z\")")]
    [InlineData("+000009999-12-31", "datetime(\"9999-12-31T00:00:00.000Z\")")]
    [InlineData("+000010000-01-01T00:00:00.000Z", "datetime(\"+000010000-01-01T00:00:00.000Z\")")]
    [InlineData("+000100000-06-15", "datetime(\"+000100000-06-15T00:00:00.000Z\")")]
    [InlineData("+001000000-12-31", "datetime(\"+001000000-12-31T00:00:00.000Z\")")]
    [InlineData("-000000001-01-01T00:00:00.000Z", "datetime(\"-000000001-01-01T00:00:00.000Z\")")]
    [InlineData("-000001000-06-15", "datetime(\"-000001000-06-15T00:00:00.000Z\")")]
    [InlineData("-000010000-12-31", "datetime(\"-000010000-12-31T00:00:00.000Z\")")]
    [InlineData("+000010000-01-01T12:30:45.123Z", "datetime(\"+000010000-01-01T12:30:45.123Z\")")]
    [InlineData("-000000100-01-01T00:00:00.001Z", "datetime(\"-000000100-01-01T00:00:00.001Z\")")]
    [InlineData("+292278994-08-17T07:12:55.807Z", "datetime(\"+292278994-08-17T07:12:55.807Z\")")]
    [InlineData("+292278994-08-17T06:12:55.807-0100", "datetime(\"+292278994-08-17T07:12:55.807Z\")")]
    [InlineData("+292278994-08-17T08:12:55.807+0100", "datetime(\"+292278994-08-17T07:12:55.807Z\")")]
    [InlineData("-292275055-05-17T16:47:04.192Z", "datetime(\"-292275055-05-17T16:47:04.192Z\")")]
    [InlineData("-292275055-05-17T15:47:04.192-0100", "datetime(\"-292275055-05-17T16:47:04.192Z\")")]
    [InlineData("-292275055-05-17T17:47:04.192+0100", "datetime(\"-292275055-05-17T16:47:04.192Z\")")]
    public void ParseAcceptsExpandedYearForms(string input, string expected)
    {
        CedarAssert.CedarText(CedarDatetime.Parse(input), expected);
    }

    [Theory]
    [InlineData("1970-01-01T00:00:00+0001", "datetime(\"1969-12-31T23:59:00.000Z\")")]
    [InlineData("1970-01-01T00:00:00+0010", "datetime(\"1969-12-31T23:50:00.000Z\")")]
    [InlineData("1970-01-01T00:00:00+0100", "datetime(\"1969-12-31T23:00:00.000Z\")")]
    [InlineData("1970-01-01T00:00:00+1000", "datetime(\"1969-12-31T14:00:00.000Z\")")]
    [InlineData("1970-01-01T00:00:00-0001", "datetime(\"1970-01-01T00:01:00.000Z\")")]
    [InlineData("1970-01-01T00:00:00-0010", "datetime(\"1970-01-01T00:10:00.000Z\")")]
    [InlineData("1970-01-01T00:00:00-0100", "datetime(\"1970-01-01T01:00:00.000Z\")")]
    [InlineData("1970-01-01T00:00:00-1000", "datetime(\"1970-01-01T10:00:00.000Z\")")]
    [InlineData("1972-02-29T10:00:00+1000", "datetime(\"1972-02-29T00:00:00.000Z\")")]
    public void ParseAppliesTimezoneOffsetsUsingUtcSemantics(string input, string expected)
    {
        CedarAssert.CedarText(CedarDatetime.Parse(input), expected);
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

    [Theory]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("+12345678")]
    [InlineData("+1234-01-01")]
    [InlineData("+00000000a-01-01")]
    [InlineData("-abcdefghi-01-01")]
    [InlineData("+12345678A-01-01")]
    [InlineData("1972-02-29T10:00:00-1000x")]
    [InlineData("+292278994-08-17T07:12:55.808Z")]
    [InlineData("+292278994-08-17T06:12:55.808-0100")]
    [InlineData("+292278994-08-17T08:12:55.808+0100")]
    [InlineData("-292275055-05-17T16:47:04.191Z")]
    [InlineData("-292275055-05-17T15:47:04.191-0100")]
    [InlineData("-292275055-05-17T17:47:04.191+0100")]
    public void ParseRejectsInvalidExpandedYearInputs(string input)
    {
        Assert.Throws<FormatException>(() => CedarDatetime.Parse(input));
    }

    [Fact]
    public void LongMinValueUsesUpstreamCanonicalExpandedYearFormatting()
    {
        CedarAssert.CedarText(new CedarDatetime(long.MinValue), "datetime(\"-292275055-05-16T16:47:04.192Z\")");
    }

    [Theory]
    [InlineData("0000-01-01", "datetime(\"0000-01-01T00:00:00.000Z\")")]
    [InlineData("9999-12-31T00:00:00.000Z", "datetime(\"9999-12-31T00:00:00.000Z\")")]
    [InlineData("+000010000-01-01T00:00:00.000Z", "datetime(\"+000010000-01-01T00:00:00.000Z\")")]
    [InlineData("+000100000-06-15T00:00:00.000Z", "datetime(\"+000100000-06-15T00:00:00.000Z\")")]
    [InlineData("+001000000-12-31T00:00:00.000Z", "datetime(\"+001000000-12-31T00:00:00.000Z\")")]
    [InlineData("-000000001-01-01T00:00:00.000Z", "datetime(\"-000000001-01-01T00:00:00.000Z\")")]
    [InlineData("-000000100-06-15T00:00:00.000Z", "datetime(\"-000000100-06-15T00:00:00.000Z\")")]
    [InlineData("-000010000-12-31T00:00:00.000Z", "datetime(\"-000010000-12-31T00:00:00.000Z\")")]
    public void MarshalCedarUsesExpandedYearFormattingWhenNeeded(string input, string expected)
    {
        CedarAssert.CedarText(CedarDatetime.Parse(input), expected);
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

    [Fact]
    public void JsonRoundTripUsesExpandedYearDatetimeExtension()
    {
        CedarDatetime expected = CedarDatetime.Parse("+000010000-01-01T00:00:00.042Z");

        string json = CedarJson.SerializeData(expected);
        ICedarData actual = CedarJson.DeserializeData(json);

        Assert.Equal("{\"__extn\":{\"fn\":\"datetime\",\"arg\":\"+000010000-01-01T00:00:00.042Z\"}}", json);
        CedarAssert.Equal(expected, Assert.IsType<CedarDatetime>(actual));
    }
}

using System;
using System.Collections.Generic;
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

    [Theory]
    [InlineData("1970-01-01", "datetime(\"1970-01-01T00:00:00.000Z\")")]
    [InlineData("1970-10-10", "datetime(\"1970-10-10T00:00:00.000Z\")")]
    [InlineData("1970-11-11", "datetime(\"1970-11-11T00:00:00.000Z\")")]
    [InlineData("1970-01-01T01:01:01Z", "datetime(\"1970-01-01T01:01:01.000Z\")")]
    [InlineData("1970-01-01T10:10:10Z", "datetime(\"1970-01-01T10:10:10.000Z\")")]
    [InlineData("1970-01-01T11:11:11Z", "datetime(\"1970-01-01T11:11:11.000Z\")")]
    [InlineData("1970-01-01T00:00:00.000Z", "datetime(\"1970-01-01T00:00:00.000Z\")")]
    [InlineData("1970-01-01T00:00:00.001Z", "datetime(\"1970-01-01T00:00:00.001Z\")")]
    [InlineData("1970-01-01T00:00:00.011Z", "datetime(\"1970-01-01T00:00:00.011Z\")")]
    [InlineData("1970-01-01T00:00:00.111Z", "datetime(\"1970-01-01T00:00:00.111Z\")")]
    [InlineData("1970-01-01T00:00:00.010Z", "datetime(\"1970-01-01T00:00:00.010Z\")")]
    [InlineData("1970-01-01T00:00:00.100Z", "datetime(\"1970-01-01T00:00:00.100Z\")")]
    public void ParseAcceptsValidDatetimeFormats(string input, string expected)
    {
        CedarAssert.CedarText(CedarDatetime.Parse(input), expected);
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
    public void ToDateTimeOffsetReturnsUtcRepresentation()
    {
        CedarDatetime value = CedarDatetime.Parse("1970-01-01T00:00:00.042Z");

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(42).ToUniversalTime(), value.ToDateTimeOffset());
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
    [InlineData("", "unexpected EOF")]
    [InlineData("*", "invalid year")]
    [InlineData("012345678", "unexpected character '4'")]
    [InlineData("1995-01", "unexpected EOF")]
    [InlineData("1995-01T00:00:00Z", "unexpected character 'T'")]
    [InlineData("1995-01-01Y00:00:00Z", "unexpected character 'Y'")]
    [InlineData("1995-01-01T:00:00Z", "invalid hour")]
    [InlineData("1995-01-01Taa:00:00Z", "invalid hour")]
    [InlineData("1995-01-01T00:aa:00Z", "invalid minute")]
    [InlineData("1995-01-01T00:00:aaZ", "invalid second")]
    [InlineData("1995-01-01T00:00:00", "unexpected EOF")]
    [InlineData("1995-01-01T00:00:00.", "unexpected EOF")]
    [InlineData("1995-01-01T00:00:00.00", "unexpected EOF")]
    [InlineData("1995-01-01T00:00:00.aaa", "invalid millisecond")]
    [InlineData("1995-01-01T00:00:00V", "invalid time zone designator")]
    [InlineData("1995-01-01T00:00:00.000+", "invalid time zone offset")]
    [InlineData("1995-01-01T00:00:00.000-000a", "invalid time zone offset")]
    [InlineData("1995-01-01T00:00:00.000-0aaa", "invalid time zone offset")]
    [InlineData("1995-04-31T00:00:00Z", "invalid date")]
    [InlineData("2024-02-30T00:00:00Z", "invalid date")]
    [InlineData("2024-02-29T23:59:60Z", "second is greater than 59")]
    [InlineData("2023-02-28T23:59:60Z", "second is greater than 59")]
    [InlineData("2023-02-28T23:60:59Z", "minute is greater than 59")]
    [InlineData("1970-01-01T25:00:00Z", "hour is greater than 23")]
    [InlineData("1970-01-32T00:00:00Z", "day is greater than 31")]
    [InlineData("1970-13-01T00:00:00Z", "month is greater than 12")]
    [InlineData("1970-01-01T00:00:00+2400", "invalid time zone offset")]
    [InlineData("1970-01-01T00:00:00+2360", "invalid time zone offset")]
    [InlineData("1972-02-29T10:00:00-1000x", "unexpected trailer after time zone designator")]
    [InlineData("1995-01-01T00+00:00Z", "unexpected character '+'")]
    [InlineData("1995-01-01T00:00+00Z", "unexpected character '+'")]
    [InlineData("+", "unexpected EOF")]
    [InlineData("-", "unexpected EOF")]
    [InlineData("+12345678", "unexpected EOF")]
    [InlineData("+1234-01-01", "invalid year")]
    [InlineData("+00000000a-01-01", "invalid year")]
    [InlineData("-abcdefghi-01-01", "invalid year")]
    [InlineData("+12345678A-01-01", "invalid year")]
    [InlineData("+292278994-08-17T07:12:55.808Z", "timestamp out of range")]
    [InlineData("+292278994-08-17T06:12:55.808-0100", "timestamp out of range")]
    [InlineData("+292278994-08-17T08:12:55.808+0100", "timestamp out of range")]
    [InlineData("-292275055-05-17T16:47:04.191Z", "timestamp out of range")]
    [InlineData("-292275055-05-17T15:47:04.191-0100", "timestamp out of range")]
    [InlineData("-292275055-05-17T17:47:04.191+0100", "timestamp out of range")]
    public void ParseRejectsInvalidInputsWithUpstreamAlignedMessages(string input, string expectedMessage)
    {
        FormatException exception = Assert.Throws<FormatException>(() => CedarDatetime.Parse(input));

        Assert.Equal(expectedMessage, exception.Message);
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
    public void DifferentValueTypesAreNotEqual()
    {
        CedarAssert.NotEqual(new CedarDatetime(0), new CedarBool(false));
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

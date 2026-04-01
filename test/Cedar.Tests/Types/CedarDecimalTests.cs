using System;
using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class CedarDecimalTests
{
    [Fact]
    public void ConstructorStoresRawScaledValue()
    {
        CedarAssert.Equal(CedarDecimal.Parse("1.2345"), CedarDecimal.NewDecimal(12_345, -4));
    }

    [Theory]
    [InlineData("1.2345", "1.2345")]
    [InlineData("1.2340", "1.234")]
    [InlineData("1.2300", "1.23")]
    [InlineData("1.2000", "1.2")]
    [InlineData("1.0000", "1.0")]
    [InlineData("1.234", "1.234")]
    [InlineData("1.230", "1.23")]
    [InlineData("1.200", "1.2")]
    [InlineData("1.000", "1.0")]
    [InlineData("1.23", "1.23")]
    [InlineData("1.20", "1.2")]
    [InlineData("1.00", "1.0")]
    [InlineData("1.2", "1.2")]
    [InlineData("1.0", "1.0")]
    [InlineData("01.0100", "1.01")]
    [InlineData("01.2345", "1.2345")]
    [InlineData("1234.5678", "1234.5678")]
    [InlineData("1234.5670", "1234.567")]
    [InlineData("1234.5600", "1234.56")]
    [InlineData("1234.5000", "1234.5")]
    [InlineData("1234.0000", "1234.0")]
    [InlineData("0.0", "0.0")]
    [InlineData("00.0", "0.0")]
    [InlineData("922337203685477.5807", "922337203685477.5807")]
    [InlineData("-922337203685477.5808", "-922337203685477.5808")]
    public void ParseDecimalProducesCanonicalString(string input, string expected)
    {
        CedarAssert.CedarText(CedarDecimal.Parse(input), "decimal(\"" + expected + "\")");
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("a")]
    [InlineData("0")]
    [InlineData("1.")]
    [InlineData("1.00000")]
    [InlineData("1.12345")]
    [InlineData("1.1234567890")]
    public void ParseDecimalRejectsInvalidInputs(string input)
    {
        Assert.Throws<FormatException>(() => CedarDecimal.Parse(input));
    }

    [Theory]
    [InlineData("10000000000000000000.0")]
    [InlineData("1000000000000000.0")]
    [InlineData("-1000000000000000.0")]
    [InlineData("922337203685477.5808")]
    [InlineData("922337203685478.0")]
    [InlineData("-922337203685477.5809")]
    [InlineData("-922337203685478.0")]
    public void ParseDecimalRejectsOverflowInputs(string input)
    {
        Assert.ThrowsAny<Exception>(() => CedarDecimal.Parse(input));
    }

    [Fact]
    public void CompareToOrdersDecimalsByNumericValue()
    {
        CedarDecimal smaller = CedarDecimal.Parse("1.0");
        CedarDecimal equal = CedarDecimal.Parse("1.0");
        CedarDecimal larger = CedarDecimal.Parse("2.0");

        Assert.True(smaller.CompareTo(larger) < 0);
        Assert.Equal(0, smaller.CompareTo(equal));
        Assert.True(larger.CompareTo(smaller) > 0);
    }

    [Fact]
    public void NewDecimalScalesNegativeExponent()
    {
        CedarAssert.Equal(CedarDecimal.Parse("123.45"), CedarDecimal.NewDecimal(12_345, -2));
    }

    [Fact]
    public void NewDecimalScalesPositiveExponent()
    {
        CedarAssert.Equal(CedarDecimal.Parse("900000000000000.0"), CedarDecimal.NewDecimal(9, 14));
    }

    [Theory]
    [InlineData(9223372036854775807L, -4, "922337203685477.5807")]
    [InlineData(922337203685477580L, -3, "922337203685477.58")]
    [InlineData(92233720368547758L, -2, "922337203685477.58")]
    [InlineData(9223372036854775L, -1, "922337203685477.5")]
    [InlineData(922337203685477L, 0, "922337203685477.0")]
    [InlineData(-9223372036854775808L, -4, "-922337203685477.5808")]
    [InlineData(-922337203685477580L, -3, "-922337203685477.58")]
    [InlineData(-92233720368547758L, -2, "-922337203685477.58")]
    [InlineData(-9223372036854775L, -1, "-922337203685477.5")]
    [InlineData(-922337203685477L, 0, "-922337203685477.0")]
    [InlineData(92233720368547L, 1, "922337203685470.0")]
    [InlineData(9223372036854L, 2, "922337203685400.0")]
    [InlineData(922337203685L, 3, "922337203685000.0")]
    [InlineData(9L, 14, "900000000000000.0")]
    public void NewDecimalProducesExpectedStringForExponentMatrix(long significand, int exponent, string expected)
    {
        CedarAssert.CedarText(CedarDecimal.NewDecimal(significand, exponent), "decimal(\"" + expected + "\")");
    }

    [Fact]
    public void NewDecimalNegativeFractionalOnlyRoundTrips()
    {
        CedarAssert.Equal(CedarDecimal.Parse("-0.1234"), CedarDecimal.NewDecimal(-1234, -4));
    }

    [Fact]
    public void ParseAcceptsLeadingZeroes()
    {
        CedarAssert.Equal(CedarDecimal.Parse("1.01"), CedarDecimal.Parse("01.0100"));
    }

    [Fact]
    public void ParseSupportsMinimumValue()
    {
        CedarAssert.Equal(CedarDecimal.DecimalMin, CedarDecimal.Parse("-922337203685477.5808"));
    }

    [Theory]
    [InlineData("-0.0001", "-0.0001")]
    [InlineData("-0.1", "-0.1")]
    [InlineData("-0.12", "-0.12")]
    [InlineData("-0.123", "-0.123")]
    [InlineData("-0.1234", "-0.1234")]
    public void ParseNegativeFractionalOnlyDecimalsAreNegative(string input, string expected)
    {
        CedarDecimal result = CedarDecimal.Parse(input);

        CedarAssert.Equal(CedarDecimal.Parse(expected), result);
    }

    [Fact]
    public void ParseNegativeZeroNormalizesToPositiveZero()
    {
        CedarDecimal result = CedarDecimal.Parse("-0.0");

        CedarAssert.Equal(CedarDecimal.Parse("0.0"), result);
    }

    [Fact]
    public void ParseRejectsMissingDecimalPoint()
    {
        Assert.Throws<FormatException>(() => CedarDecimal.Parse("42"));
    }

    [Fact]
    public void ParseRejectsTooManyFractionalDigits()
    {
        Assert.Throws<FormatException>(() => CedarDecimal.Parse("1.23456"));
    }

    [Fact]
    public void NewDecimalRejectsExponentOutsideRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CedarDecimal.NewDecimal(1, 15));
    }

    [Fact]
    public void NewDecimalRejectsOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CedarDecimal.NewDecimal(922337203685478, 0));
    }

    [Fact]
    public void EqualValuesAreEqual()
    {
        CedarAssert.Equal(CedarDecimal.Parse("42.0"), CedarDecimal.NewDecimal(42, 0));
    }

    [Fact]
    public void DifferentValuesAreNotEqual()
    {
        CedarAssert.NotEqual(CedarDecimal.Parse("42.0"), CedarDecimal.Parse("43.0"));
    }

    [Fact]
    public void DifferentValueTypesAreNotEqual()
    {
        CedarAssert.NotEqual(CedarDecimal.Parse("0.0"), new CedarBool(false));
    }

    [Fact]
    public void HashCodeIsStable()
    {
        CedarAssert.HashStable(CedarDecimal.Parse("1234.5678"));
    }

    [Fact]
    public void MarshalCedarFormatsCanonicalDecimal()
    {
        CedarAssert.CedarText(CedarDecimal.NewDecimal(42, 0), "decimal(\"42.0\")");
    }

    [Fact]
    public void JsonRoundTripUsesExplicitExtensionForm()
    {
        CedarDecimal expected = CedarDecimal.Parse("1234.5678");

        string json = CedarJson.SerializeData(expected);
        ICedarData actual = CedarJson.DeserializeData(json);

        Assert.Equal("{\"__extn\":{\"fn\":\"decimal\",\"arg\":\"1234.5678\"}}", json);
        CedarAssert.Equal(expected, Assert.IsType<CedarDecimal>(actual));
    }

    [Fact]
    public void DecimalMaxStoresMaximumRawValue()
    {
        CedarAssert.Equal(CedarDecimal.Parse("922337203685477.5807"), CedarDecimal.DecimalMax);
    }

    [Fact]
    public void DecimalMinStoresMinimumRawValue()
    {
        CedarAssert.Equal(CedarDecimal.Parse("-922337203685477.5808"), CedarDecimal.DecimalMin);
    }

    [Theory]
    [InlineData(0L, "0.0")]
    [InlineData(1L, "1.0")]
    [InlineData(-1L, "-1.0")]
    [InlineData(922337203685477L, "922337203685477.0")]
    [InlineData(-922337203685477L, "-922337203685477.0")]
    public void NewDecimalFromIntProducesExpectedString(long input, string expected)
    {
        CedarAssert.Equal(CedarDecimal.Parse(expected), CedarDecimal.NewDecimalFromInt(input));
    }

    [Fact]
    public void NewDecimalFromIntRejectsOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CedarDecimal.NewDecimalFromInt(922337203685478L));
    }

    [Fact]
    public void ToDoubleReturnsApproximation()
    {
        CedarDecimal value = CedarDecimal.NewDecimalFromFloat(42.42);

        Assert.Equal(42.42, value.ToDouble());
    }

    [Theory]
    [InlineData(0.0, "0.0")]
    [InlineData(1.0, "1.0")]
    [InlineData(-1.0, "-1.0")]
    [InlineData(1.23451, "1.2345")]
    [InlineData(1.23456, "1.2345")]
    [InlineData(12345678901.2345, "12345678901.2345")]
    [InlineData(123456789012.3456, "123456789012.3456")]
    [InlineData(922337203685477.5807, "922337203685477.5807")]
    [InlineData(-922337203685477.5808, "-922337203685477.5808")]
    public void NewDecimalFromFloatProducesExpectedString(double input, string expected)
    {
        CedarAssert.Equal(CedarDecimal.Parse(expected), CedarDecimal.NewDecimalFromFloat(input));
    }

    [Theory]
    [InlineData(922337203685477.6875)]
    [InlineData(-922337203685477.6876)]
    [InlineData(1000000000000000.0)]
    [InlineData(-1000000000000000.0)]
    public void NewDecimalFromFloatRejectsOutOfRange(double input)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CedarDecimal.NewDecimalFromFloat(input));
    }

    [Theory]
    [InlineData(922337203685477581L, -3)]
    [InlineData(92233720368547759L, -2)]
    [InlineData(9223372036854776L, -1)]
    [InlineData(922337203685478L, 0)]
    [InlineData(92233720368548L, 1)]
    [InlineData(922337203685477581L, 2)]
    [InlineData(10L, 14)]
    [InlineData(1L, 15)]
    public void NewDecimalRejectsOverflowMatrix(long significand, int exponent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CedarDecimal.NewDecimal(significand, exponent));
    }

    [Theory]
    [InlineData(-922337203685477581L, -3)]
    [InlineData(-92233720368547759L, -2)]
    [InlineData(-9223372036854776L, -1)]
    [InlineData(-922337203685478L, 0)]
    [InlineData(-92233720368548L, 1)]
    [InlineData(-922337203685477581L, 2)]
    [InlineData(-10L, 14)]
    [InlineData(-1L, 15)]
    public void NewDecimalRejectsUnderflowMatrix(long significand, int exponent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CedarDecimal.NewDecimal(significand, exponent));
    }
}

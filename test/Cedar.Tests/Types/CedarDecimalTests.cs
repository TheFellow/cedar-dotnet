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
        CedarDecimal value = new(12_345);

        Assert.Equal(12_345, value.Value);
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
        CedarAssert.Equal(new CedarDecimal(long.MinValue), CedarDecimal.Parse("-922337203685477.5808"));
    }

    [Theory]
    [InlineData("-0.0001", -1L)]
    [InlineData("-0.1", -1000L)]
    [InlineData("-0.12", -1200L)]
    [InlineData("-0.123", -1230L)]
    [InlineData("-0.1234", -1234L)]
    public void ParseNegativeFractionalOnlyDecimalsAreNegative(string input, long expectedRawValue)
    {
        CedarDecimal result = CedarDecimal.Parse(input);

        Assert.Equal(expectedRawValue, result.Value);
    }

    [Fact]
    public void ParseNegativeZeroNormalizesToPositiveZero()
    {
        CedarDecimal result = CedarDecimal.Parse("-0.0");

        Assert.Equal(0L, result.Value);
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
        Assert.Equal(long.MaxValue, CedarDecimal.DecimalMax.Value);
    }

    [Fact]
    public void DecimalMinStoresMinimumRawValue()
    {
        Assert.Equal(long.MinValue, CedarDecimal.DecimalMin.Value);
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

    [Theory]
    [InlineData(0.0, "0.0")]
    [InlineData(1.0, "1.0")]
    [InlineData(-1.0, "-1.0")]
    [InlineData(1.23451, "1.2345")]
    [InlineData(1.23456, "1.2345")]
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
    [InlineData(-10L, 14)]
    [InlineData(-1L, 15)]
    public void NewDecimalRejectsUnderflowMatrix(long significand, int exponent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CedarDecimal.NewDecimal(significand, exponent));
    }
}

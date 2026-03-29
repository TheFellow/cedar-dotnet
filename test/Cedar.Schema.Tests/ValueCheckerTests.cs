using System.Collections.Generic;
using Cedar.Schema.Internal.Validate;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class ValueCheckerTests
{
    [Fact]
    public void CheckValue_AcceptsMatchingPrimitive()
    {
        (bool isDeserError, string? error) = ValueChecker.CheckValue(new Cedar.Types.CedarString("ok"), new ResolvedStringType());

        Assert.False(isDeserError);
        Assert.Null(error);
    }

    [Fact]
    public void CheckRecord_ReportsMissingRequiredAttribute()
    {
        ResolvedRecordType expected = new()
        {
            Attributes = new Dictionary<string, ResolvedAttribute>
            {
                ["name"] = new() { Type = new ResolvedStringType() }
            }
        };

        (bool isDeserError, string? error) = ValueChecker.CheckRecord(new CedarRecord(), expected);

        Assert.False(isDeserError);
        Assert.Contains("missing required attribute", error);
    }

    [Fact]
    public void CheckRecord_ReportsUnexpectedAttributeAsDeserializationError()
    {
        ResolvedRecordType expected = new()
        {
            Attributes = new Dictionary<string, ResolvedAttribute>()
        };

        (bool isDeserError, string? error) = ValueChecker.CheckRecord(
            new CedarRecord(new Dictionary<Cedar.Types.CedarString, ICedarData> { [new Cedar.Types.CedarString("extra")] = new Cedar.Types.CedarLong(1) }),
            expected);

        Assert.True(isDeserError);
        Assert.Contains("unexpected attribute", error);
    }

    [Fact]
    public void CheckExtensionValue_DistinguishesDeserializationAndConformanceErrors()
    {
        (bool wrongShapeDeser, string? wrongShapeError) = ValueChecker.CheckExtensionValue(new Cedar.Types.CedarString("x"), new ResolvedExtensionType(new Ident("ipaddr")));
        (bool wrongExtensionDeser, string? wrongExtensionError) = ValueChecker.CheckExtensionValue(CedarDecimal.Parse("1.0"), new ResolvedExtensionType(new Ident("ipaddr")));

        Assert.True(wrongShapeDeser);
        Assert.False(wrongExtensionDeser);
        Assert.NotNull(wrongShapeError);
        Assert.NotNull(wrongExtensionError);
    }
}

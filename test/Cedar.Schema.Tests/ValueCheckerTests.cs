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

    [Fact]
    public void CheckRecord_ReportsFirstMissingRequiredAmongMultiple()
    {
        ResolvedRecordType expected = new()
        {
            Attributes = new Dictionary<string, ResolvedAttribute>
            {
                ["first"] = new() { Type = new ResolvedStringType() },
                ["second"] = new() { Type = new ResolvedLongType() }
            }
        };

        (bool isDeserError, string? error) = ValueChecker.CheckRecord(new CedarRecord(), expected);

        Assert.False(isDeserError);
        Assert.Equal("missing required attribute \"first\"", error);
    }

    [Fact]
    public void CheckRecord_ReportsFirstUnexpectedAmongMultiple()
    {
        ResolvedRecordType expected = new()
        {
            Attributes = new Dictionary<string, ResolvedAttribute>()
        };

        CedarRecord record = Record(("zeta", new CedarLong(1)), ("alpha", new CedarLong(2)));

        (bool isDeserError, string? error) = ValueChecker.CheckRecord(record, expected);

        Assert.True(isDeserError);
        Assert.Equal("unexpected attribute \"alpha\"", error);
    }

    [Fact]
    public void CheckRecord_ReportsMissingBeforeUnexpected()
    {
        ResolvedRecordType expected = new()
        {
            Attributes = new Dictionary<string, ResolvedAttribute>
            {
                ["name"] = new() { Type = new ResolvedStringType() }
            }
        };

        CedarRecord record = Record(("extra", new CedarLong(1)));

        (bool isDeserError, string? error) = ValueChecker.CheckRecord(record, expected);

        Assert.False(isDeserError);
        Assert.Equal("missing required attribute \"name\"", error);
    }

    [Fact]
    public void CheckRecord_AcceptsOptionalMissingAttribute()
    {
        ResolvedRecordType expected = new()
        {
            Attributes = new Dictionary<string, ResolvedAttribute>
            {
                ["nick"] = new() { Type = new ResolvedStringType(), Optional = true }
            }
        };

        (bool isDeserError, string? error) = ValueChecker.CheckRecord(new CedarRecord(), expected);

        Assert.False(isDeserError);
        Assert.Null(error);
    }

    [Fact]
    public void CheckRecord_ReportsNestedRecordTypeError()
    {
        ResolvedRecordType expected = new()
        {
            Attributes = new Dictionary<string, ResolvedAttribute>
            {
                ["profile"] = new()
                {
                    Type = new ResolvedRecordType
                    {
                        Attributes = new Dictionary<string, ResolvedAttribute>
                        {
                            ["active"] = new() { Type = new ResolvedBoolType() }
                        }
                    }
                }
            }
        };

        CedarRecord record = Record(("profile", Record(("active", new CedarString("yes")))));

        (bool isDeserError, string? error) = ValueChecker.CheckRecord(record, expected);

        Assert.False(isDeserError);
        Assert.Equal("attribute \"profile\": attribute \"active\": expected Boolean, got CedarString", error);
    }

    [Fact]
    public void CheckRecord_ReportsAttributeTypeError()
    {
        ResolvedRecordType expected = new()
        {
            Attributes = new Dictionary<string, ResolvedAttribute>
            {
                ["name"] = new() { Type = new ResolvedStringType() }
            }
        };

        CedarRecord record = Record(("name", new CedarLong(1)));

        (bool isDeserError, string? error) = ValueChecker.CheckRecord(record, expected);

        Assert.False(isDeserError);
        Assert.Equal("attribute \"name\": expected String, got CedarLong", error);
    }

    [Fact]
    public void CheckValue_AcceptsMatchingLong()
    {
        (bool isDeserError, string? error) = ValueChecker.CheckValue(new CedarLong(1), new ResolvedLongType());

        Assert.False(isDeserError);
        Assert.Null(error);
    }

    [Fact]
    public void CheckValue_AcceptsMatchingBool()
    {
        (bool isDeserError, string? error) = ValueChecker.CheckValue(CedarBool.True, new ResolvedBoolType());

        Assert.False(isDeserError);
        Assert.Null(error);
    }

    [Fact]
    public void CheckValue_RejectsPrimitiveMismatch()
    {
        (bool isDeserError, string? error) = ValueChecker.CheckValue(CedarBool.True, new ResolvedStringType());

        Assert.False(isDeserError);
        Assert.Equal("expected String, got CedarBool", error);
    }

    [Fact]
    public void CheckSet_RejectsNonSetValue()
    {
        (bool isDeserError, string? error) = ValueChecker.CheckValue(new CedarLong(1), new ResolvedSetType(new ResolvedLongType()));

        Assert.True(isDeserError);
        Assert.Equal("expected Set, got CedarLong", error);
    }

    [Fact]
    public void CheckSet_RejectsElementTypeMismatch()
    {
        (bool isDeserError, string? error) = ValueChecker.CheckValue(new CedarSet(new CedarString("x")), new ResolvedSetType(new ResolvedLongType()));

        Assert.False(isDeserError);
        Assert.Equal("set element: expected Long, got CedarString", error);
    }

    [Fact]
    public void CheckEntityValue_RejectsNonEntityUid()
    {
        (bool isDeserError, string? error) = ValueChecker.CheckValue(new CedarLong(1), new ResolvedEntityType(new EntityType("User")));

        Assert.True(isDeserError);
        Assert.Equal("expected EntityUID, got CedarLong", error);
    }

    [Fact]
    public void CheckEntityValue_RejectsWrongEntityType()
    {
        (bool isDeserError, string? error) = ValueChecker.CheckValue(
            new EntityUid(new EntityType("Group"), new CedarString("ops")),
            new ResolvedEntityType(new EntityType("User")));

        Assert.False(isDeserError);
        Assert.Equal("expected entity type \"User\", got \"Group\"", error);
    }

    [Fact]
    public void CheckExtensionValue_AcceptsMatchingDatetime()
    {
        (bool isDeserError, string? error) = ValueChecker.CheckExtensionValue(
            CedarDatetime.Parse("2024-01-01T00:00:00Z"),
            new ResolvedExtensionType(new Ident("datetime")));

        Assert.False(isDeserError);
        Assert.Null(error);
    }

    [Fact]
    public void CheckExtensionValue_AcceptsMatchingDuration()
    {
        (bool isDeserError, string? error) = ValueChecker.CheckExtensionValue(
            CedarDuration.Parse("1h30m"),
            new ResolvedExtensionType(new Ident("duration")));

        Assert.False(isDeserError);
        Assert.Null(error);
    }

    [Fact]
    public void CheckExtensionValue_RejectsDatetimeWhenDurationExpected()
    {
        (bool isDeserError, string? error) = ValueChecker.CheckExtensionValue(
            CedarDatetime.Parse("2024-01-01T00:00:00Z"),
            new ResolvedExtensionType(new Ident("duration")));

        Assert.False(isDeserError);
        Assert.Equal("expected Duration, got CedarDatetime", error);
    }

    [Fact]
    public void CheckExtensionValue_UnknownExtensionTypeReturnsNoError()
    {
        (bool isDeserError, string? error) = ValueChecker.CheckExtensionValue(
            new CedarString("x"),
            new ResolvedExtensionType(new Ident("mystery")));

        Assert.False(isDeserError);
        Assert.Null(error);
    }

    private static CedarRecord Record(params (string Key, ICedarData Value)[] entries)
    {
        Dictionary<CedarString, ICedarData> values = [];
        foreach ((string key, ICedarData value) in entries)
        {
            values[new CedarString(key)] = value;
        }

        return new CedarRecord(values);
    }
}

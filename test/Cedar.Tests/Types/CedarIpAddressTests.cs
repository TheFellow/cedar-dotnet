using System;
using System.Net;
using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class CedarIpAddressTests
{
    [Fact]
    public void ParseAcceptsIpv4Address()
    {
        CedarIpAddress value = CedarIpAddress.Parse("127.0.0.1");

        Assert.Equal(IPAddress.Parse("127.0.0.1"), value.Address);
        Assert.Equal(32, value.PrefixLength);
    }

    [Fact]
    public void ParseAcceptsIpv6Address()
    {
        CedarIpAddress value = CedarIpAddress.Parse("2001:db8::1");

        Assert.Equal(IPAddress.Parse("2001:db8::1"), value.Address);
        Assert.Equal(128, value.PrefixLength);
    }

    [Fact]
    public void ParsePreservesExplicitCidrPrefix()
    {
        CedarIpAddress value = CedarIpAddress.Parse("127.0.0.1/24");

        Assert.Equal(24, value.PrefixLength);
        CedarAssert.CedarText(value, "ip(\"127.0.0.1/24\")");
    }

    [Fact]
    public void ParseRejectsEmbeddedIpv4InDottedIpv6Notation()
    {
        Assert.Throws<FormatException>(() => CedarIpAddress.Parse("::ffff:192.0.2.128"));
    }

    [Fact]
    public void ParseRejectsLeadingZeroPrefix()
    {
        Assert.Throws<FormatException>(() => CedarIpAddress.Parse("6b6b:f00::32ff:ffff:6368/00"));
    }

    [Fact]
    public void ContainsSingleAddressWithinRange()
    {
        Assert.True(CedarIpAddress.Parse("255.255.0.0/16").Contains(CedarIpAddress.Parse("255.255.255.255")));
    }

    [Fact]
    public void ContainsSubnetWithinRange()
    {
        Assert.True(CedarIpAddress.Parse("2001:db8::/64").Contains(CedarIpAddress.Parse("2001:db8:0:0:dead:f00d::/96")));
    }

    [Fact]
    public void DoesNotContainBroaderPrefix()
    {
        Assert.False(CedarIpAddress.Parse("255.255.0.0/16").Contains(CedarIpAddress.Parse("255.254.0.0/15")));
    }

    [Fact]
    public void FamilyHelpersDetectIpv4AndIpv6()
    {
        Assert.True(CedarIpAddress.Parse("127.0.0.1").IsIPv4());
        Assert.False(CedarIpAddress.Parse("127.0.0.1").IsIPv6());
        Assert.True(CedarIpAddress.Parse("::1").IsIPv6());
        Assert.False(CedarIpAddress.Parse("::1").IsIPv4());
    }

    [Fact]
    public void LoopbackDetectionMatchesPrefixRules()
    {
        Assert.True(CedarIpAddress.Parse("127.0.0.1/8").IsLoopback());
        Assert.False(CedarIpAddress.Parse("127.0.0.1/7").IsLoopback());
        Assert.True(CedarIpAddress.Parse("::1").IsLoopback());
        Assert.False(CedarIpAddress.Parse("::1/127").IsLoopback());
    }

    [Fact]
    public void MulticastDetectionMatchesIpv4AndIpv6Ranges()
    {
        Assert.True(CedarIpAddress.Parse("224.0.0.0/4").IsMulticast());
        Assert.False(CedarIpAddress.Parse("224.0.0.0/3").IsMulticast());
        Assert.True(CedarIpAddress.Parse("ff00::/8").IsMulticast());
        Assert.False(CedarIpAddress.Parse("ff00::/7").IsMulticast());
    }

    [Fact]
    public void EqualValuesAreEqual()
    {
        CedarAssert.Equal(CedarIpAddress.Parse("127.0.0.1"), CedarIpAddress.Parse("127.0.0.1/32"));
    }

    [Fact]
    public void HashCodeIsStable()
    {
        CedarAssert.HashStable(CedarIpAddress.Parse("10.0.0.42"));
    }

    [Fact]
    public void JsonRoundTripUsesIpExtension()
    {
        CedarIpAddress expected = CedarIpAddress.Parse("12.34.56.78");

        string json = CedarJson.SerializeData(expected);
        ICedarData actual = CedarJson.DeserializeData(json);

        Assert.Equal("{\"__extn\":{\"fn\":\"ip\",\"arg\":\"12.34.56.78\"}}", json);
        CedarAssert.Equal(expected, Assert.IsType<CedarIpAddress>(actual));
    }
}

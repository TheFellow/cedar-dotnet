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

    [Theory]
    [InlineData("0.0.0.0", "0.0.0.0")]
    [InlineData("0.0.0.1", "0.0.0.1")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    [InlineData("127.0.0.1/32", "127.0.0.1")]
    [InlineData("127.0.0.1/24", "127.0.0.1/24")]
    [InlineData("127.1.2.3/8", "127.1.2.3/8")]
    [InlineData("::/128", "::")]
    [InlineData("::1/128", "::1")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    [InlineData("2001:db8::1:0:0:1", "2001:db8::1:0:0:1")]
    [InlineData("::ffff:c000:0280", "::ffff:c000:280")]
    [InlineData("2001:db8::1/32", "2001:db8::1/32")]
    [InlineData("2001:db8::1:0:0:1/96", "2001:db8::1:0:0:1/96")]
    [InlineData("::ffff:c000:0280/24", "::ffff:c000:280/24")]
    [InlineData("::ffff:c000:0280/120", "::ffff:c000:280/120")]
    [InlineData("c5c5:c5c5:c5c5:c5c5:c5c5:c5c5:c5c5:c5c5/68", "c5c5:c5c5:c5c5:c5c5:c5c5:c5c5:c5c5:c5c5/68")]
    public void ParseAndStringProduceExpectedOutput(string input, string expected)
    {
        CedarIpAddress value = CedarIpAddress.Parse(input);

        CedarAssert.CedarText(value, "ip(\"" + expected + "\")");
    }

    [Theory]
    [InlineData("::ffff:192.0.2.128")]
    [InlineData("::ffff:192.0.2.128/24")]
    [InlineData("::ffff:192.0.2.128/120")]
    [InlineData("6b6b:f00::32ff:ffff:6368/00")]
    [InlineData("fe80::1%eth0")]
    [InlineData("fe80::1%1")]
    [InlineData("fe80::1%eth0/64")]
    [InlineData("2001:db8::1%eth0")]
    [InlineData("garbage")]
    public void ParseRejectsInvalidInputs(string input)
    {
        Assert.Throws<FormatException>(() => CedarIpAddress.Parse(input));
    }

    [Theory]
    [InlineData("fe80::1%eth0")]
    [InlineData("fe80::1%1")]
    [InlineData("fe80::1%eth0/64")]
    [InlineData("2001:db8::1%eth0")]
    public void ParseRejectsIpv6ZoneIdentifiersWithSpecificMessage(string input)
    {
        FormatException exception = Assert.Throws<FormatException>(() => CedarIpAddress.Parse(input));

        Assert.Equal("IPv6 zone identifiers are not supported.", exception.Message);
    }

    [Theory]
    [InlineData("0.0.0.0", "0.0.0.0", true)]
    [InlineData("0.0.0.0", "0.0.0.0/32", true)]
    [InlineData("127.0.0.1", "127.0.0.1", true)]
    [InlineData("127.0.0.1", "127.0.0.1/32", true)]
    [InlineData("::", "::", true)]
    [InlineData("::", "::/128", true)]
    [InlineData("::1", "::1", true)]
    [InlineData("::1", "::1/128", true)]
    [InlineData("::", "0.0.0.0", false)]
    [InlineData("::1", "127.0.0.1", false)]
    [InlineData("::ffff:c000:0280", "192.0.2.128", false)]
    [InlineData("1.2.3.4", "1.2.3.4", true)]
    [InlineData("1.2.3.4", "1.2.3.4/32", true)]
    [InlineData("1.2.3.4/32", "1.2.3.4/32", true)]
    [InlineData("1.2.3.4/24", "1.2.3.4/24", true)]
    [InlineData("1.2.3.0/24", "1.2.3.255/24", false)]
    [InlineData("1.2.3.0/24", "1.2.3.0/25", false)]
    [InlineData("::ffff:c000:0280/24", "::/24", false)]
    [InlineData("::ffff:c000:0280/120", "192.0.2.0/24", false)]
    [InlineData("2001:db8::1/32", "2001:db8::/32", false)]
    [InlineData("2001:db8::1:0:0:1/96", "2001:db8:0:0:1::/96", false)]
    [InlineData("c5c5:c5c5:c5c5:c5c5:c5c5:c5c5:c5c5:c5c5/68", "c5c5:c5c5:c5c5:c5c5:c5c5:5cc5:c5c5:c5c5/68", false)]
    public void EqualityMatchesGoUpstreamCases(string lhs, string rhs, bool expected)
    {
        CedarIpAddress left = CedarIpAddress.Parse(lhs);
        CedarIpAddress right = CedarIpAddress.Parse(rhs);

        Assert.Equal(expected, left.Equals(right));
        if (expected)
        {
            Assert.True(left.Contains(right));
            Assert.True(right.Contains(left));
        }
    }

    [Theory]
    [InlineData("0.0.0.0", true, false)]
    [InlineData("0.0.0.0/32", true, false)]
    [InlineData("127.0.0.1", true, false)]
    [InlineData("127.0.0.1/32", true, false)]
    [InlineData("::", false, true)]
    [InlineData("::1", false, true)]
    [InlineData("::/128", false, true)]
    [InlineData("::1/128", false, true)]
    [InlineData("::ffff:c000:0280", false, true)]
    [InlineData("::ffff:c000:0280/128", false, true)]
    [InlineData("::ffff:c000:0280/24", false, true)]
    [InlineData("2001:db8::1", false, true)]
    [InlineData("2001:db8::1:0:0:1", false, true)]
    [InlineData("2001:db8::1/32", false, true)]
    [InlineData("2001:db8::1:0:0:1/96", false, true)]
    public void IsIPv4AndIsIPv6MatchGoUpstreamCases(string input, bool expectIpv4, bool expectIpv6)
    {
        CedarIpAddress value = CedarIpAddress.Parse(input);

        Assert.Equal(expectIpv4, value.IsIPv4());
        Assert.Equal(expectIpv6, value.IsIPv6());
    }

    [Theory]
    [InlineData("0.0.0.0", false)]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.0.0.2", true)]
    [InlineData("127.0.0.1/32", true)]
    [InlineData("127.0.0.1/24", true)]
    [InlineData("127.0.0.1/8", true)]
    [InlineData("127.0.0.1/7", false)]
    [InlineData("::", false)]
    [InlineData("::1", true)]
    [InlineData("::/128", false)]
    [InlineData("::1/128", true)]
    [InlineData("::1/127", false)]
    [InlineData("::ffff:7f00:0001", false)]
    [InlineData("::ffff:7f00:0001/128", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("2001:db8::1:0:0:1", false)]
    [InlineData("2001:db8::1/32", false)]
    [InlineData("2001:db8::1:0:0:1/96", false)]
    public void IsLoopbackMatchesGoUpstreamCases(string input, bool expected)
    {
        Assert.Equal(expected, CedarIpAddress.Parse(input).IsLoopback());
    }

    [Theory]
    [InlineData("0.0.0.0", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("223.255.255.255", false)]
    [InlineData("224.0.0.0", true)]
    [InlineData("239.255.255.255", true)]
    [InlineData("240.0.0.0", false)]
    [InlineData("feff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", false)]
    [InlineData("ff00::", true)]
    [InlineData("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", true)]
    [InlineData("ff00::/8", true)]
    [InlineData("ff00::/7", false)]
    [InlineData("::ffff:e000:0000", false)]
    [InlineData("::ffff:e000:0000/128", false)]
    [InlineData("224.0.0.0/4", true)]
    [InlineData("224.0.0.0/3", false)]
    public void IsMulticastMatchesGoUpstreamCases(string input, bool expected)
    {
        Assert.Equal(expected, CedarIpAddress.Parse(input).IsMulticast());
    }

    [Theory]
    [InlineData("0.0.0.0/31", "0.0.0.0", true)]
    [InlineData("0.0.0.0", "0.0.0.0/31", false)]
    [InlineData("255.255.0.0/16", "255.255.255.255", true)]
    [InlineData("255.255.0.0/16", "255.255.255.248/28", true)]
    [InlineData("255.255.0.0/16", "255.255.255.0/24", true)]
    [InlineData("255.255.0.0/16", "255.255.248.0/20", true)]
    [InlineData("255.255.0.0/16", "255.255.0.0/16", true)]
    [InlineData("255.255.0.0/16", "255.254.0.0/15", false)]
    [InlineData("255.255.0.0/16", "255.254.255.0/24", false)]
    [InlineData("::ffff:c000:0280", "192.0.2.128", false)]
    [InlineData("2001:db8::/120", "2001:db8::2", true)]
    [InlineData("2001:db8::/64", "2001:db8:0:0:dead:f00d::/96", true)]
    public void ContainsMatchesGoUpstreamCases(string lhs, string rhs, bool expected)
    {
        Assert.Equal(expected, CedarIpAddress.Parse(lhs).Contains(CedarIpAddress.Parse(rhs)));
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
    public void DifferentValuesAreNotEqual()
    {
        CedarAssert.NotEqual(CedarIpAddress.Parse("127.0.0.1"), CedarIpAddress.Parse("10.0.0.1"));
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

    [Fact]
    public void MarshalCedar_FormatsIpv6WithoutEmbeddedIpv4DottedNotation()
    {
        CedarIpAddress value = new(IPAddress.Parse("::255.58.0.255"));

        CedarAssert.CedarText(value, "ip(\"::ff3a:ff\")");
    }
}

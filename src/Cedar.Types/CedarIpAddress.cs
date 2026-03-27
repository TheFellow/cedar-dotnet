using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;

namespace Cedar.Types;

public sealed record CedarIpAddress(IPAddress Address, int PrefixLength) : CedarValue
{
    public CedarIpAddress(IPAddress address)
        : this(address, GetBitLength(address))
    {
    }

    public static CedarIpAddress Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (CountOccurrences(value, ':') >= 2 && CountOccurrences(value, '.') >= 2)
        {
            throw new FormatException("IPv4 addresses embedded in IPv6 dotted notation are not supported.");
        }

        string addressText = value;
        int? prefixLength = null;

        int slashIndex = value.LastIndexOf('/');
        if (slashIndex >= 0)
        {
            addressText = value[..slashIndex];
            string prefixText = value[(slashIndex + 1)..];
            if (prefixText.Length == 0)
            {
                throw new FormatException("CIDR prefixes must include a prefix length.");
            }

            if (prefixText.Length > 1 && prefixText[0] == '0')
            {
                throw new FormatException("CIDR prefixes must not contain leading zeroes.");
            }

            if (!int.TryParse(prefixText, out int parsedPrefix))
            {
                throw new FormatException("CIDR prefix length is invalid.");
            }

            prefixLength = parsedPrefix;
        }

        if (!IPAddress.TryParse(addressText, out IPAddress? address))
        {
            throw new FormatException("The IP address is invalid.");
        }

        // .NET's IPAddress.TryParse is lenient and accepts short-form IPv4 like "0", "127",
        // "192.168". Cedar (like Go's netip.ParseAddr) requires strict dotted-decimal with
        // exactly four octets for IPv4 (a.b.c.d), and no leading zeros in octets.
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            if (CountOccurrences(addressText, '.') != 3)
            {
                throw new FormatException("IPv4 addresses must use dotted-decimal notation (a.b.c.d).");
            }

            if (HasLeadingZeroOctet(addressText))
            {
                throw new FormatException("IPv4 octets must not have leading zeros.");
            }
        }

        int bitLength = GetBitLength(address);
        int effectivePrefix = prefixLength ?? bitLength;

        if (effectivePrefix is < 0 || effectivePrefix > bitLength)
        {
            throw new FormatException("CIDR prefix length is out of range.");
        }

        return new CedarIpAddress(address, effectivePrefix);
    }

    public bool IsIPv4()
    {
        return Address.AddressFamily == AddressFamily.InterNetwork;
    }

    public bool IsIPv6()
    {
        return Address.AddressFamily == AddressFamily.InterNetworkV6;
    }

    public bool IsLoopback()
    {
        return IPAddress.IsLoopback(new IPAddress(GetMaskedBytes(Address, PrefixLength)));
    }

    public bool IsMulticast()
    {
        byte[] bytes = Address.GetAddressBytes();

        if (IsIPv4())
        {
            return PrefixLength >= 4 && bytes[0] is >= 224 and <= 239;
        }

        return PrefixLength >= 8 && bytes[0] == 0xff;
    }

    public bool Contains(CedarIpAddress other)
    {
        if (GetBitLength(Address) != GetBitLength(other.Address))
        {
            return false;
        }

        if (PrefixLength > other.PrefixLength)
        {
            return false;
        }

        return GetMaskedBytes(Address, PrefixLength).SequenceEqual(GetMaskedBytes(other.Address, PrefixLength));
    }

    public override string MarshalCedar()
    {
        return "ip(\"" + FormatValue() + "\")";
    }

    public override int GetHashCode()
    {
        return CedarHash.ForBytesAndInt32(nameof(CedarIpAddress), Address.GetAddressBytes(), PrefixLength);
    }

    private static bool HasLeadingZeroOctet(string value)
    {
        int start = 0;
        for (int index = 0; index <= value.Length; index++)
        {
            if (index == value.Length || value[index] == '.')
            {
                int octetLength = index - start;
                if (octetLength > 1 && value[start] == '0')
                {
                    return true;
                }

                start = index + 1;
            }
        }

        return false;
    }

    private static int CountOccurrences(string value, char character)
    {
        int count = 0;
        foreach (char current in value)
        {
            if (current == character)
            {
                count++;
            }
        }

        return count;
    }

    private static int GetBitLength(IPAddress address)
    {
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => 32,
            AddressFamily.InterNetworkV6 => 128,
            _ => throw new ArgumentOutOfRangeException(nameof(address), "Only IPv4 and IPv6 addresses are supported.")
        };
    }

    private static byte[] GetMaskedBytes(IPAddress address, int prefixLength)
    {
        byte[] bytes = address.GetAddressBytes();
        byte[] masked = new byte[bytes.Length];
        Array.Copy(bytes, masked, bytes.Length);

        int fullBytes = prefixLength / 8;
        int remainingBits = prefixLength % 8;

        if (remainingBits > 0 && fullBytes < masked.Length)
        {
            masked[fullBytes] &= (byte)(0xff << (8 - remainingBits));
            fullBytes++;
        }

        for (int index = fullBytes; index < masked.Length; index++)
        {
            masked[index] = 0;
        }

        return masked;
    }

    private string FormatValue()
    {
        int bitLength = GetBitLength(Address);
        string addressText = Address.ToString();
        return PrefixLength == bitLength ? addressText : addressText + "/" + PrefixLength;
    }
}

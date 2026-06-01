using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Cedar.Types;

/// <summary>
/// Represents a Cedar <c>ip</c> value as an IPv4 or IPv6 address with an associated prefix length.
/// </summary>
public sealed record CedarIpAddress(IPAddress Address, int PrefixLength) : CedarValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CedarIpAddress"/> record using the full-width prefix for the address family.
    /// </summary>
    /// <param name="address">The IPv4 or IPv6 address.</param>
    public CedarIpAddress(IPAddress address)
        : this(address, GetBitLength(address))
    {
    }

    /// <summary>
    /// Parses a Cedar IP address literal or CIDR prefix.
    /// </summary>
    /// <param name="value">The textual IP address value to parse.</param>
    /// <returns>The parsed <see cref="CedarIpAddress"/> value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The supplied value is not a valid Cedar IP address.</exception>
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

        if (addressText.Contains('%'))
        {
            throw new FormatException("IPv6 zone identifiers are not supported.");
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

    /// <summary>
    /// Determines whether this value is an IPv4 address.
    /// </summary>
    /// <returns><see langword="true"/> when the address family is IPv4; otherwise, <see langword="false"/>.</returns>
    public bool IsIPv4()
    {
        return Address.AddressFamily == AddressFamily.InterNetwork;
    }

    /// <summary>
    /// Determines whether this value is an IPv6 address.
    /// </summary>
    /// <returns><see langword="true"/> when the address family is IPv6; otherwise, <see langword="false"/>.</returns>
    public bool IsIPv6()
    {
        return Address.AddressFamily == AddressFamily.InterNetworkV6;
    }

    /// <summary>
    /// Determines whether this value is a loopback address according to Cedar semantics.
    /// </summary>
    /// <returns><see langword="true"/> when the masked address is loopback; otherwise, <see langword="false"/>.</returns>
    public bool IsLoopback()
    {
        if (Address.IsIPv4MappedToIPv6)
        {
            return false;
        }

        return IPAddress.IsLoopback(new IPAddress(GetMaskedBytes(Address, PrefixLength)));
    }

    /// <summary>
    /// Determines whether this value is a multicast address according to Cedar semantics.
    /// </summary>
    /// <returns><see langword="true"/> when the address range is multicast; otherwise, <see langword="false"/>.</returns>
    public bool IsMulticast()
    {
        if (Address.IsIPv4MappedToIPv6)
        {
            return false;
        }

        byte[] bytes = Address.GetAddressBytes();

        if (IsIPv4())
        {
            return PrefixLength >= 4 && bytes[0] is >= 224 and <= 239;
        }

        return PrefixLength >= 8 && bytes[0] == 0xff;
    }

    /// <summary>
    /// Determines whether this IP address range contains another Cedar IP address value.
    /// </summary>
    /// <param name="other">The candidate value to test for containment.</param>
    /// <returns><see langword="true"/> when <paramref name="other"/> falls within this range; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Produces the Cedar source representation of this IP address value.
    /// </summary>
    /// <returns>The Cedar <c>ip("...")</c> representation for this value.</returns>
    public override string MarshalCedar()
    {
        return "ip(\"" + FormatValue() + "\")";
    }

    /// <summary>
    /// Returns a hash code for this Cedar IP address value.
    /// </summary>
    /// <returns>A stable hash code derived from the address bytes and prefix length.</returns>
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
        string addressText = Address.AddressFamily == AddressFamily.InterNetworkV6 && CountOccurrences(Address.ToString(), '.') > 0
            ? FormatCanonicalIpv6(Address)
            : Address.ToString();
        return PrefixLength == bitLength ? addressText : addressText + "/" + PrefixLength;
    }

    private static string FormatCanonicalIpv6(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        ushort[] groups = new ushort[8];
        for (int index = 0; index < groups.Length; index++)
        {
            groups[index] = (ushort)((bytes[index * 2] << 8) | bytes[(index * 2) + 1]);
        }

        (int bestStart, int bestLength) = FindBestZeroRun(groups);

        StringBuilder builder = new();
        bool needsSeparator = false;

        for (int index = 0; index < groups.Length; index++)
        {
            if (bestLength > 0 && index == bestStart)
            {
                builder.Append("::");
                needsSeparator = false;
                index += bestLength - 1;
                continue;
            }

            if (needsSeparator)
            {
                builder.Append(':');
            }

            builder.Append(groups[index].ToString("x", CultureInfo.InvariantCulture));
            needsSeparator = true;
        }

        return builder.Length == 0 ? "::" : builder.ToString();
    }

    private static (int Start, int Length) FindBestZeroRun(ushort[] groups)
    {
        int bestStart = -1;
        int bestLength = 0;
        int currentStart = -1;
        int currentLength = 0;

        for (int index = 0; index < groups.Length; index++)
        {
            if (groups[index] == 0)
            {
                if (currentStart < 0)
                {
                    currentStart = index;
                }

                currentLength++;
                continue;
            }

            if (currentLength > bestLength)
            {
                bestStart = currentStart;
                bestLength = currentLength;
            }

            currentStart = -1;
            currentLength = 0;
        }

        if (currentLength > bestLength)
        {
            bestStart = currentStart;
            bestLength = currentLength;
        }

        return bestLength >= 2 ? (bestStart, bestLength) : (-1, 0);
    }
}

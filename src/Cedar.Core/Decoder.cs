using System;
using System.IO;
using System.Text;

namespace Cedar.Core;

public static class Decoder
{
    public static Policy[] Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string cedarText = reader.ReadToEnd();
        return Policy.UnmarshalCedarList(cedarText);
    }
}

public sealed class PolicyDecoder
{
    private readonly TextReader _reader;
    private Policy[]? _policies;
    private int _index;

    public PolicyDecoder(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _reader = reader;
    }

    public bool TryDecode(out Policy? policy)
    {
        if (_policies is null)
        {
            string cedarText = _reader.ReadToEnd();
            _policies = string.IsNullOrWhiteSpace(cedarText)
                ? Array.Empty<Policy>()
                : Policy.UnmarshalCedarList(cedarText);
        }

        if (_index < _policies.Length)
        {
            policy = _policies[_index];
            _index++;
            return true;
        }

        policy = null;
        return false;
    }
}

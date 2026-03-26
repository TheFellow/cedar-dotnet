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

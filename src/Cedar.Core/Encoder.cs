using System;
using System.IO;
using System.Text;

namespace Cedar.Core;

public static class Encoder
{
    public static void Encode(Stream stream, Policy policy)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(policy);

        WriteText(stream, policy.MarshalCedar() + "\n");
    }

    public static void Encode(Stream stream, PolicySet policySet)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(policySet);

        WriteText(stream, policySet.MarshalCedar() + "\n");
    }

    private static void WriteText(Stream stream, string text)
    {
        using StreamWriter writer = new(stream, new UTF8Encoding(false), leaveOpen: true);
        writer.Write(text);
        writer.Flush();
    }
}

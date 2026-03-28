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

public sealed class PolicyEncoder
{
    private readonly TextWriter _writer;

    public PolicyEncoder(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        _writer = writer;
    }

    public void Encode(Policy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        _writer.Write(policy.MarshalCedar());
        _writer.Write('\n');
        _writer.Flush();
    }
}

using System;
using System.IO;
using System.Text;
using Cedar.Core;
using Xunit;

namespace Cedar.Tests.PolicyApi;

public sealed class StreamTests
{
    [Fact]
    public void Encoder_EncodePolicy_WritesCedarText()
    {
        Policy policy = Policy.UnmarshalCedar("permit(principal, action, resource);");
        using MemoryStream stream = new();

        Cedar.Core.Encoder.Encode(stream, policy);

        string text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("permit(principal, action, resource);\n", text);
    }

    [Fact]
    public void Encoder_EncodePolicySet_WritesAllPolicies()
    {
        PolicySet set = new();
        set.Add(new PolicyId("a"), Policy.UnmarshalCedar("permit(principal, action, resource);"));
        set.Add(new PolicyId("b"), Policy.UnmarshalCedar("forbid(principal, action, resource);"));
        using MemoryStream stream = new();

        Cedar.Core.Encoder.Encode(stream, set);

        string text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("permit(principal, action, resource);", text);
        Assert.Contains("forbid(principal, action, resource);", text);
    }

    [Fact]
    public void Decoder_Decode_ReadsPoliciesFromStream()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("permit(principal, action, resource);\nforbid(principal, action, resource);\n"));

        Policy[] policies = Cedar.Core.Decoder.Decode(stream);

        Assert.Equal(2, policies.Length);
    }

    [Fact]
    public void EncoderDecoder_RoundTrip()
    {
        PolicySet set = new();
        set.Add(new PolicyId("a"), Policy.UnmarshalCedar("permit(principal, action, resource);"));
        set.Add(new PolicyId("b"), Policy.UnmarshalCedar("forbid(principal, action, resource);"));

        using MemoryStream stream = new();
        Cedar.Core.Encoder.Encode(stream, set);
        stream.Position = 0;

        Policy[] decoded = Cedar.Core.Decoder.Decode(stream);

        Assert.Equal(2, decoded.Length);
        Assert.Equal("permit(principal, action, resource);", decoded[0].MarshalCedar());
        Assert.Equal("forbid(principal, action, resource);", decoded[1].MarshalCedar());
    }

    [Fact]
    public void PolicyEncoder_EncodePolicies_WritesConcatenatedCedarText()
    {
        Policy policy0 = Policy.UnmarshalJson("""
            {
                "effect": "permit",
                "principal": {
                    "op": "==",
                    "entity": { "type": "User", "id": "bob" }
                },
                "action": {
                    "op": "==",
                    "entity": { "type": "Action", "id": "view" }
                },
                "resource": {
                    "op": "in",
                    "entity": { "type": "Folder", "id": "abc" }
                }
            }
            """);
        Policy policy1 = Policy.UnmarshalJson("""
            {
                "effect": "permit",
                "principal": {
                    "op": "==",
                    "entity": { "type": "User", "id": "bob" }
                },
                "action": {
                    "op": "==",
                    "entity": { "type": "Action", "id": "view" }
                },
                "resource": {
                    "op": "in",
                    "entity": { "type": "Folder", "id": "abc" }
                }
            }
            """);

        using StringWriter writer = new();
        PolicyEncoder encoder = new(writer);

        encoder.Encode(policy0);
        encoder.Encode(policy1);

        string result = writer.ToString();

        // Each Encode call writes one Cedar policy line followed by '\n'.
        // The exact whitespace is governed by MarshalCedar (tested separately);
        // here we verify two complete policy texts are present and concatenated.
        string[] lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line =>
        {
            Assert.StartsWith("permit", line, StringComparison.Ordinal);
            Assert.Contains("User::\"bob\"", line, StringComparison.Ordinal);
            Assert.Contains("Action::\"view\"", line, StringComparison.Ordinal);
            Assert.Contains("Folder::\"abc\"", line, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void PolicyEncoder_Encode_WhenWriterThrows_PropagatesIOException()
    {
        Policy policy = Policy.UnmarshalCedar("permit(principal, action, resource);");
        using ThrowingWriter writer = new();
        PolicyEncoder encoder = new(writer);

        Assert.Throws<IOException>(() => encoder.Encode(policy));
    }

    private sealed class ThrowingWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            throw new IOException("Simulated write failure");
        }

        public override void Write(string? value)
        {
            throw new IOException("Simulated write failure");
        }
    }
}

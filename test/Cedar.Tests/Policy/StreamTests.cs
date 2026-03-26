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
}

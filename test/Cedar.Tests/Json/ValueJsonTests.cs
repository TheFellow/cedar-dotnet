using System.Text.Json;
using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Json;

public sealed class ValueJsonTests
{
    [Fact]
    public void Deserialize_RecordWithLiteralEntitySentinelKey_IsRecord()
    {
        ICedarData value = CedarJson.DeserializeData("{\"__entity\":\"literal\"}");

        CedarRecord record = Assert.IsType<CedarRecord>(value);
        Assert.True(record.TryGetValue(new CedarString("__entity"), out ICedarData actual));
        Assert.Equal(new CedarString("literal"), Assert.IsType<CedarString>(actual));
    }

    [Fact]
    public void Deserialize_RecordWithLiteralExtensionSentinelKey_IsRecord()
    {
        ICedarData value = CedarJson.DeserializeData("{\"__extn\":\"literal\"}");

        CedarRecord record = Assert.IsType<CedarRecord>(value);
        Assert.True(record.TryGetValue(new CedarString("__extn"), out ICedarData actual));
        Assert.Equal(new CedarString("literal"), Assert.IsType<CedarString>(actual));
    }

    [Fact]
    public void Deserialize_RecordWithInvalidExtensionPayload_IsRecord()
    {
        ICedarData value = CedarJson.DeserializeData("{\"__extn\":{\"fn\":1,\"arg\":2}}\n");

        CedarRecord record = Assert.IsType<CedarRecord>(value);
        Assert.True(record.TryGetValue(new CedarString("__extn"), out ICedarData actual));
        Assert.IsType<CedarRecord>(actual);
    }

    [Fact]
    public void RoundTrip_RecordWithSentinelLikeKeys_PreservesKeys()
    {
        CedarRecord original = new(new RecordMap
        {
            [new CedarString("__entity")] = new CedarString("entity-literal"),
            [new CedarString("__extn")] = new CedarString("extn-literal")
        });

        ICedarData roundTripped = CedarJson.DeserializeData(CedarJson.SerializeData(original));

        CedarRecord record = Assert.IsType<CedarRecord>(roundTripped);
        Assert.True(record.TryGetValue(new CedarString("__entity"), out ICedarData entityValue));
        Assert.True(record.TryGetValue(new CedarString("__extn"), out ICedarData extnValue));
        Assert.Equal(new CedarString("entity-literal"), Assert.IsType<CedarString>(entityValue));
        Assert.Equal(new CedarString("extn-literal"), Assert.IsType<CedarString>(extnValue));
    }

    [Fact]
    public void Deserialize_ExplicitEntitySentinel_IsEntityUid()
    {
        ICedarData value = CedarJson.DeserializeData("{\"__entity\":{\"type\":\"User\",\"id\":\"alice\"}}\n");

        EntityUid uid = Assert.IsType<EntityUid>(value);
        Assert.Equal(new EntityType("User"), uid.Type);
        Assert.Equal(new CedarString("alice"), uid.Id);
    }

    [Fact]
    public void Deserialize_ExplicitExtensionSentinel_IsExtensionValue()
    {
        ICedarData value = CedarJson.DeserializeData("{\"__extn\":{\"fn\":\"ip\",\"arg\":\"10.0.0.1\"}}\n");

        Assert.Equal(CedarIpAddress.Parse("10.0.0.1"), Assert.IsType<CedarIpAddress>(value));
    }

    [Fact]
    public void Deserialize_ImplicitExtensionObject_IsExtensionValue()
    {
        ICedarData value = CedarJson.DeserializeData("{\"fn\":\"decimal\",\"arg\":\"1.23\"}");

        Assert.Equal(CedarDecimal.Parse("1.23"), Assert.IsType<CedarDecimal>(value));
    }

    [Fact]
    public void Deserialize_InvalidExtensionFunction_Throws()
    {
        Assert.Throws<JsonException>(() => CedarJson.DeserializeData("{\"__extn\":{\"fn\":\"nope\",\"arg\":\"x\"}}"));
    }
}

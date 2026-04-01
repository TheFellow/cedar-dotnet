using System.Collections.Generic;
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

    [Fact]
    public void Deserialize_Boolean_ReturnsCedarBool()
    {
        ICedarData value = CedarJson.DeserializeData("false");

        CedarBool result = Assert.IsType<CedarBool>(value);
        Assert.False(result.Value);
    }

    [Fact]
    public void Deserialize_Long_ReturnsCedarLong()
    {
        ICedarData value = CedarJson.DeserializeData("42");

        CedarLong result = Assert.IsType<CedarLong>(value);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Deserialize_String_ReturnsCedarString()
    {
        ICedarData value = CedarJson.DeserializeData("\"hello\"");

        CedarString result = Assert.IsType<CedarString>(value);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Deserialize_Set_ReturnsCedarSet()
    {
        ICedarData value = CedarJson.DeserializeData("[42]");

        CedarSet set = Assert.IsType<CedarSet>(value);
        Assert.Equal(1, set.Count);
        Assert.True(set.Contains(new CedarLong(42)));
    }

    [Fact]
    public void Deserialize_Record_ReturnsCedarRecord()
    {
        ICedarData value = CedarJson.DeserializeData("{\"a\":\"b\"}");

        CedarRecord record = Assert.IsType<CedarRecord>(value);
        Assert.True(record.TryGetValue(new CedarString("a"), out ICedarData actual));
        Assert.Equal(new CedarString("b"), Assert.IsType<CedarString>(actual));
    }

    [Fact]
    public void Deserialize_ExplicitIP_ReturnsCedarIpAddress()
    {
        ICedarData value = CedarJson.DeserializeData("{\"__extn\":{\"fn\":\"ip\",\"arg\":\"222.222.222.7\"}}");

        Assert.Equal(CedarIpAddress.Parse("222.222.222.7"), Assert.IsType<CedarIpAddress>(value));
    }

    [Fact]
    public void Deserialize_ExplicitSubnet_ReturnsCedarIpAddress()
    {
        ICedarData value = CedarJson.DeserializeData("{\"__extn\":{\"fn\":\"ip\",\"arg\":\"192.168.0.0/16\"}}");

        Assert.Equal(CedarIpAddress.Parse("192.168.0.0/16"), Assert.IsType<CedarIpAddress>(value));
    }

    [Fact]
    public void Deserialize_ExplicitDatetime_ReturnsCedarDatetime()
    {
        ICedarData value = CedarJson.DeserializeData("{\"__extn\":{\"fn\":\"datetime\",\"arg\":\"1970-01-01T00:00:01Z\"}}");

        Assert.Equal(CedarDatetime.Parse("1970-01-01T00:00:01Z"), Assert.IsType<CedarDatetime>(value));
    }

    [Fact]
    public void Deserialize_ExplicitDuration_ReturnsCedarDuration()
    {
        ICedarData value = CedarJson.DeserializeData("{\"__extn\":{\"fn\":\"duration\",\"arg\":\"1d12h30m30s500ms\"}}");

        Assert.Equal(CedarDuration.Parse("1d12h30m30s500ms"), Assert.IsType<CedarDuration>(value));
    }

    [Fact]
    public void RoundTrip_EntityUid_MarshalAndUnmarshal()
    {
        EntityUid uid = new(new EntityType("User"), new CedarString("alice"));

        string json = CedarJson.SerializeData(uid);
        ICedarData roundTripped = CedarJson.DeserializeData(json);

        Assert.Equal("{\"__entity\":{\"type\":\"User\",\"id\":\"alice\"}}", json);
        Assert.Equal(uid, Assert.IsType<EntityUid>(roundTripped));
    }

    [Fact]
    public void RoundTrip_RecordWithExtension_MarshalAndUnmarshal()
    {
        CedarRecord original = new(new RecordMap
        {
            [new CedarString("ip")] = CedarIpAddress.Parse("222.222.222.7")
        });

        string json = CedarJson.SerializeData(original);
        ICedarData roundTripped = CedarJson.DeserializeData(json);

        Assert.Equal("{\"ip\":{\"__extn\":{\"fn\":\"ip\",\"arg\":\"222.222.222.7\"}}}", json);
        CedarAssert.Equal(original, Assert.IsType<CedarRecord>(roundTripped));
    }

    [Fact]
    public void Serialize_RecordKeysAreSortedAlphabetically()
    {
        CedarRecord record = new(new RecordMap
        {
            [new CedarString("ak")] = new CedarString("av"),
            [new CedarString("ck")] = new CedarString("cv"),
            [new CedarString("bk")] = new CedarString("bv")
        });

        string json = CedarJson.SerializeData(record);

        Assert.Equal("{\"ak\":\"av\",\"bk\":\"bv\",\"ck\":\"cv\"}", json);
    }
}

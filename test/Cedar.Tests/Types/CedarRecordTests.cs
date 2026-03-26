using Cedar.Tests.TestSupport;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Types;

public sealed class CedarRecordTests
{
    [Fact]
    public void EmptyRecordHasZeroCount()
    {
        Assert.Equal(0, new CedarRecord().Count);
    }

    [Fact]
    public void ConstructorStoresPrimitiveValues()
    {
        CedarRecord record = new(new RecordMap
        {
            [new CedarString("name")] = new CedarString("alice")
        });

        Assert.True(record.TryGetValue(new CedarString("name"), out ICedarData value));
        CedarAssert.Equal(new CedarString("alice"), Assert.IsType<CedarString>(value));
    }

    [Fact]
    public void ConstructorStoresEntityUidValues()
    {
        EntityUid uid = new(new EntityType("User"), new CedarString("alice"));
        CedarRecord record = new(new RecordMap
        {
            [new CedarString("owner")] = uid
        });

        Assert.True(record.TryGetValue(new CedarString("owner"), out ICedarData value));
        Assert.Equal(uid, Assert.IsType<EntityUid>(value));
    }

    [Fact]
    public void MarshalCedarSortsKeysLexicographically()
    {
        CedarRecord record = new(new RecordMap
        {
            [new CedarString("b")] = new CedarLong(2),
            [new CedarString("a")] = new CedarLong(1)
        });

        CedarAssert.CedarText(record, "{\"a\":1, \"b\":2}");
    }

    [Fact]
    public void EqualityIgnoresInsertionOrder()
    {
        CedarRecord left = new(new RecordMap
        {
            [new CedarString("a")] = new CedarLong(1),
            [new CedarString("b")] = new CedarLong(2)
        });
        CedarRecord right = new(new RecordMap
        {
            [new CedarString("b")] = new CedarLong(2),
            [new CedarString("a")] = new CedarLong(1)
        });

        CedarAssert.Equal(left, right);
    }

    [Fact]
    public void DifferentValuesAreNotEqual()
    {
        CedarRecord left = new(new RecordMap
        {
            [new CedarString("key")] = new CedarLong(0)
        });
        CedarRecord right = new(new RecordMap
        {
            [new CedarString("key")] = CedarDecimal.Parse("0.0")
        });

        CedarAssert.NotEqual(left, right);
    }

    [Fact]
    public void HashCodeIsStable()
    {
        CedarAssert.HashStable(new CedarRecord(new RecordMap
        {
            [new CedarString("stable")] = new CedarString("hash")
        }));
    }

    [Fact]
    public void EnumerationExposesAllEntries()
    {
        CedarRecord record = new(new RecordMap
        {
            [new CedarString("a")] = new CedarLong(1),
            [new CedarString("b")] = new CedarLong(2)
        });

        Assert.Equal(2, System.Linq.Enumerable.Count(record));
    }

    [Fact]
    public void MissingKeyReturnsFalse()
    {
        CedarRecord record = new();

        Assert.False(record.TryGetValue(new CedarString("missing"), out _));
    }

    [Fact]
    public void JsonRoundTripSupportsPrimitiveValues()
    {
        CedarRecord expected = new(new RecordMap
        {
            [new CedarString("name")] = new CedarString("alice"),
            [new CedarString("age")] = new CedarLong(42)
        });

        ICedarData actual = CedarJson.DeserializeData(CedarJson.SerializeData(expected));

        CedarAssert.Equal(expected, Assert.IsType<CedarRecord>(actual));
    }

    [Fact]
    public void JsonRoundTripSupportsEntityUidValues()
    {
        CedarRecord expected = new(new RecordMap
        {
            [new CedarString("owner")] = new EntityUid(new EntityType("User"), new CedarString("alice"))
        });

        ICedarData actual = CedarJson.DeserializeData(CedarJson.SerializeData(expected));

        CedarAssert.Equal(expected, Assert.IsType<CedarRecord>(actual));
    }

    [Fact]
    public void JsonRoundTripSupportsNestedCollections()
    {
        CedarRecord expected = new(new RecordMap
        {
            [new CedarString("set")] = new CedarSet(new CedarLong(1), new CedarLong(2)),
            [new CedarString("record")] = new CedarRecord(new RecordMap
            {
                [new CedarString("flag")] = new CedarBool(true)
            })
        });

        ICedarData actual = CedarJson.DeserializeData(CedarJson.SerializeData(expected));

        CedarAssert.Equal(expected, Assert.IsType<CedarRecord>(actual));
    }
}

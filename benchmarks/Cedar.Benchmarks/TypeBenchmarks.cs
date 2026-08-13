using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Cedar.Types;

namespace Cedar.Benchmarks;

[MemoryDiagnoser]
public class TypeBenchmarks
{
    private readonly EntityMap _entities;
    private readonly EntityUid _targetUid;
    private readonly CedarSet _set;
    private readonly CedarLong _containedValue = new(3);
    private readonly CedarRecord _record;
    private readonly CedarString _recordKey = new("level");
    private readonly EntityUidSet _entityUidSet;

    public TypeBenchmarks()
    {
        _targetUid = new EntityUid(new EntityType("User"), new CedarString("alice"));
        _entities = new EntityMap(
        [
            new Entity(_targetUid, new EntityUidSet(), new CedarRecord(), new CedarRecord()),
            new Entity(new EntityUid(new EntityType("User"), new CedarString("bob")), new EntityUidSet(), new CedarRecord(), new CedarRecord())
        ]);

        _set = new CedarSet(new CedarLong(1), new CedarLong(2), _containedValue, new CedarLong(4));
        _record = new CedarRecord(new Dictionary<CedarString, ICedarData>
        {
            [_recordKey] = new CedarLong(42)
        });
        _entityUidSet = new EntityUidSet(
        [
            _targetUid,
            new EntityUid(new EntityType("User"), new CedarString("bob")),
            new EntityUid(new EntityType("User"), new CedarString("carol"))
        ]);
    }

    [Benchmark]
    public bool EntityLookup()
    {
        return _entities.TryGet(_targetUid, out _);
    }

    [Benchmark]
    public bool SetContains()
    {
        return _set.Contains(_containedValue);
    }

    [Benchmark]
    public bool RecordAccess()
    {
        return _record.TryGetValue(_recordKey, out _);
    }

    [Benchmark]
    public int SetHash()
    {
        return _set.GetHashCode();
    }

    [Benchmark]
    public int RecordHash()
    {
        return _record.GetHashCode();
    }

    [Benchmark]
    public int EntityUidSetHash()
    {
        return _entityUidSet.GetHashCode();
    }
}

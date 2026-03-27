using BenchmarkDotNet.Attributes;
using Cedar.Core;

namespace Cedar.Benchmarks;

[MemoryDiagnoser]
public sealed class ParseBenchmarks
{
    private readonly string _cedarText = """
        permit(
            principal == User::"alice",
            action == Action::"read",
            resource == Document::"doc1"
        )
        when { context.level == 42 };
        """;

    private readonly string _policyJson;

    public ParseBenchmarks()
    {
        _policyJson = Policy.UnmarshalCedar(_cedarText).MarshalJson();
    }

    [Benchmark]
    public Policy ParseCedar()
    {
        return Policy.UnmarshalCedar(_cedarText);
    }

    [Benchmark]
    public Policy ParseJson()
    {
        return Policy.UnmarshalJson(_policyJson);
    }
}

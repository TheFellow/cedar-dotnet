using System.Collections.Generic;
using System.Collections.Immutable;

namespace Cedar.Core.Internal.Eval;

internal struct DiagnosticAccumulator<T>
    where T : class
{
    private T? _first;
    private List<T>? _additional;

    public readonly int Count => _first is null ? 0 : 1 + (_additional?.Count ?? 0);

    public void Add(T item)
    {
        if (_first is null)
        {
            _first = item;
            return;
        }

        (_additional ??= []).Add(item);
    }

    public readonly ImmutableArray<T> ToImmutableArray()
    {
        if (_first is null)
        {
            return ImmutableArray<T>.Empty;
        }

        if (_additional is null)
        {
            return ImmutableArray.Create(_first);
        }

        ImmutableArray<T>.Builder builder = ImmutableArray.CreateBuilder<T>(1 + _additional.Count);
        builder.Add(_first);
        builder.AddRange(_additional);
        return builder.MoveToImmutable();
    }
}

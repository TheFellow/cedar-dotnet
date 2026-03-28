using System;
using Cedar.Core;

namespace Cedar.Batch;

public sealed record BatchOption
{
    private BatchOption(Effect? ignoreBias = null, Action<BatchResult>? callback = null, bool includeDiagnostics = true)
    {
        IgnoreBias = ignoreBias;
        Callback = callback;
        IncludeDiagnostics = includeDiagnostics;
    }

    internal Effect? IgnoreBias { get; }

    internal Action<BatchResult>? Callback { get; }

    internal bool IncludeDiagnostics { get; }

    public static BatchOption WithIgnoreForbid()
    {
        return new(ignoreBias: Effect.Forbid);
    }

    public static BatchOption WithIgnorePermit()
    {
        return new(ignoreBias: Effect.Permit);
    }

    public static BatchOption WithCallback(Action<BatchResult> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return new(callback: callback, includeDiagnostics: false);
    }

    public static BatchOption WithDiagnosticCallback(Action<BatchResult> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return new(callback: callback, includeDiagnostics: true);
    }
}
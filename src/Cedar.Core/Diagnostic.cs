using System.Collections.Immutable;

namespace Cedar.Core;

public sealed record Diagnostic(ImmutableArray<DiagnosticReason> Reasons, ImmutableArray<DiagnosticError> Errors)
{
    public Diagnostic()
        : this(ImmutableArray<DiagnosticReason>.Empty, ImmutableArray<DiagnosticError>.Empty)
    {
    }

    public static Diagnostic Empty { get; } = new();
}

namespace Cedar.Core;

public sealed record DiagnosticError(PolicyId PolicyId, Position Position, string Message)
{
    public override string ToString()
    {
        return $"while evaluating policy `{PolicyId}`: {Message}";
    }
}

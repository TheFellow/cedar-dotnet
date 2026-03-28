using System;

namespace Cedar.Batch;

public sealed class BatchMissingPartException : Exception
{
    public BatchMissingPartException(string partName)
        : base($"missing part: {partName}")
    {
        this.PartName = partName;
    }

    public string PartName { get; }
}

public sealed class BatchInvalidPartException : Exception
{
    public BatchInvalidPartException(string partName, Exception innerException)
        : base($"invalid {partName}: {innerException.Message}", innerException)
    {
        this.PartName = partName;
    }

    public string PartName { get; }
}
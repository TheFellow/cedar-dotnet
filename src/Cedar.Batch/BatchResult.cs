using System.Collections.Generic;
using Cedar.Core;
using Cedar.Types;

namespace Cedar.Batch;

public sealed record BatchResult(Request Request, IReadOnlyDictionary<string, ICedarData> Values, Decision Decision, Diagnostic Diagnostic);

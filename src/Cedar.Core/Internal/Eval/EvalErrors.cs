using Cedar.Types;

namespace Cedar.Core.Internal.Eval;

internal static class EvalErrors
{
    public const string Overflow = "integer overflow";
    public const string UnknownExtensionFunction = "function does not exist";
    public const string WrongArity = "wrong number of arguments provided to extension function";
    public const string MissingAttribute = "does not have the attribute";
    public const string MissingTag = "does not have the tag";
    public const string MissingEntity = "does not exist";
    public const string UnspecifiedEntity = "unspecified entity";
    public const string IncompatibleComparison = "incompatible types in comparison";

    public static string TypeName(ICedarData value)
    {
        return value switch
        {
            CedarBool => "bool",
            CedarLong => "long",
            CedarString => "string",
            CedarDecimal => "decimal",
            CedarDatetime => "datetime",
            CedarDuration => "duration",
            CedarIpAddress => "IP address",
            CedarSet => "set",
            CedarRecord => "record",
            CedarPattern => "pattern",
            EntityUid entity => $"entity `{entity.Type.Value}`",
            _ => value.GetType().Name
        };
    }
}

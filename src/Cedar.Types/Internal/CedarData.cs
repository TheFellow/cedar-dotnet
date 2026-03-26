using System;
using System.Collections.Generic;

namespace Cedar.Types.Internal;

internal static class CedarData
{
    public static void EnsureSupported(ICedarData value)
    {
        _ = value switch
        {
            CedarValue => value,
            EntityUid => value,
            _ => throw new ArgumentException($"Unsupported Cedar data type: {value.GetType().FullName}", nameof(value))
        };
    }

    public static int GetHashCode(ICedarData value)
    {
        EnsureSupported(value);
        return value.GetHashCode();
    }

    public static string MarshalCedar(ICedarData value)
    {
        EnsureSupported(value);
        return value.MarshalCedar();
    }

    public static bool Equals(ICedarData? left, ICedarData? right)
    {
        return left is null ? right is null : left.Equals(right);
    }

    public static int CompareCanonical(ICedarData left, ICedarData right)
    {
        int hashComparison = GetHashCode(left).CompareTo(GetHashCode(right));
        if (hashComparison != 0)
        {
            return hashComparison;
        }

        return StringComparer.Ordinal.Compare(MarshalCedar(left), MarshalCedar(right));
    }

    public static int CompareKeys(CedarString left, CedarString right)
    {
        return StringComparer.Ordinal.Compare(left.Value, right.Value);
    }

    public static IReadOnlyList<ICedarData> SortValues(IEnumerable<ICedarData> values)
    {
        List<ICedarData> sorted = [.. values];
        sorted.Sort(CompareCanonical);
        return sorted;
    }
}

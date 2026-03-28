using System;
using System.Text;

namespace Cedar.Core;

public static class PolicyList
{
    public static Policy[] ParseCedar(string cedarText)
    {
        return Policy.UnmarshalCedarList(cedarText);
    }

    public static string MarshalCedar(Policy[] policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        StringBuilder builder = new();
        foreach (Policy policy in policies)
        {
            ArgumentNullException.ThrowIfNull(policy);

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(policy.MarshalCedar());
        }

        return builder.ToString();
    }
}

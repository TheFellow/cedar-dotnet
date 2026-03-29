using System;
using System.Linq;
using Cedar.Core;
using Cedar.Types;

namespace Cedar.Schema;

public sealed partial class SchemaValidator
{
    public ValidationResult ValidateRequest(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_schema.Actions.TryGetValue(request.Action, out ResolvedAction? action))
        {
            return ValidationResult.Failure($"action `{request.Action}` does not exist in the supplied schema");
        }

        if (action.AppliesTo is null)
        {
            return ValidationResult.Success;
        }

        ValidationResult principalResult = ValidateRequestEntityType(request.Principal, "principal");
        if (!principalResult.IsValid)
        {
            return principalResult;
        }

        if (!action.AppliesTo.Principals.Contains(request.Principal.Type))
        {
            return ValidationResult.Failure($"principal type `{request.Principal.Type}` is not valid for `{request.Action}`");
        }

        ValidationResult resourceResult = ValidateRequestEntityType(request.Resource, "resource");
        if (!resourceResult.IsValid)
        {
            return resourceResult;
        }

        if (!action.AppliesTo.Resources.Contains(request.Resource.Type))
        {
            return ValidationResult.Failure($"resource type `{request.Resource.Type}` is not valid for `{request.Action}`");
        }

        (bool _, string? error) = Internal.Validate.ValueChecker.CheckRecord(request.Context ?? new CedarRecord(), action.AppliesTo.Context);
        if (error is not null)
        {
            return ValidationResult.Failure($"context `{FormatContextRecord(request.Context ?? new CedarRecord())}` is not valid for `{request.Action}`");
        }

        return ValidationResult.Success;
    }

    private ValidationResult ValidateRequestEntityType(EntityUid uid, string role)
    {
        return IsKnownEntityType(uid.Type)
            ? ValidationResult.Success
            : ValidationResult.Failure($"{role} type `{uid.Type}` is not declared in the schema");
    }

    private static string FormatContextRecord(CedarRecord record)
    {
        return record.MarshalCedar();
    }
}

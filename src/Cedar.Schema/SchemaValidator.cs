using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Schema.Internal.Validate;
using Cedar.Types;

namespace Cedar.Schema;

public enum ValidationMode
{
    Strict,
    Permissive
}

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success { get; } = new(true, Array.Empty<string>());

    public static ValidationResult Failure(params string[] errors)
    {
        return new ValidationResult(false, errors);
    }

    public static ValidationResult Failure(IReadOnlyList<string> errors)
    {
        return new ValidationResult(false, errors);
    }
}

public sealed partial class SchemaValidator
{
    private readonly ResolvedSchema _schema;
    private readonly ImmutableArray<RequestEnvironment> _requestEnvironments;
    private readonly bool _strict;

    public SchemaValidator(ResolvedSchema schema, ValidationMode mode = ValidationMode.Strict)
    {
        ArgumentNullException.ThrowIfNull(schema);

        _schema = schema;
        _requestEnvironments = [.. RequestEnvironment.Generate(schema)];
        _strict = mode == ValidationMode.Strict;
    }

    public ValidationResult ValidatePolicy(string policyId, Policy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        PolicyAst ast = policy.Ast;
        return PolicyValidator.ValidatePolicy(policyId, ast, this);
    }

    internal bool IsKnownEntityType(EntityType entityType)
    {
        return _schema.Entities.ContainsKey(entityType) || _schema.Enums.ContainsKey(entityType);
    }

    internal ResolvedSchema Schema => _schema;

    internal ImmutableArray<RequestEnvironment> RequestEnvironments => _requestEnvironments;

    internal bool IsStrict => _strict;
}

using System;
using System.Collections.Generic;
using Cedar.Ast.Internal;
using Cedar.Core;
using Cedar.Types;

namespace Cedar.Schema.Internal.Validate;

internal record ValidationIssue(string Message);

internal sealed record TypeIncompatIssue(string Message) : ValidationIssue(Message);

internal sealed record UnsafeTagAccessIssue(string Message, bool UsesPrincipal, bool UsesResource) : ValidationIssue(Message);

internal static class PolicyValidator
{
    internal static ValidationResult ValidatePolicy(string policyId, PolicyAst ast, SchemaValidator validator)
    {
        List<ValidationIssue> issues = [];

        (EntityType[]? principalTypes, List<ValidationIssue> principalErrors) = ScopeValidator.ValidatePrincipalScope(ast.PrincipalScope, validator);
        issues.AddRange(principalErrors);

        (EntityUid[]? actionUids, List<ValidationIssue> actionErrors) = ScopeValidator.ValidateAndGetActionUids(ast.ActionScope, validator);
        issues.AddRange(actionErrors);

        (EntityType[]? resourceTypes, List<ValidationIssue> resourceErrors) = ScopeValidator.ValidateResourceScope(ast.ResourceScope, validator);
        issues.AddRange(resourceErrors);

        ValidationIssue? actionApplicationError = ScopeValidator.ValidateActionApplication(principalTypes, resourceTypes, actionUids, validator);
        if (actionApplicationError is not null)
        {
            issues.Add(actionApplicationError);
        }

        if (validator.IsStrict && ast.ActionScope is ScopeInSet scopeInSet && scopeInSet.Entities.Length == 0)
        {
            issues.Add(new ValidationIssue("empty set literals are forbidden in policies"));
        }

        List<RequestEnvironment> environments = RequestEnvironment.FilterForPolicy(
            validator.RequestEnvironments,
            principalTypes,
            resourceTypes,
            actionUids);

        if (environments.Count > 0 && ast.Conditions.Length > 0 && (validator.IsStrict || actionApplicationError is null))
        {
            TypeChecker typeChecker = new(validator);
            issues.AddRange(typeChecker.TypecheckConditions(environments, ast.Conditions));
        }

        if (issues.Count == 0)
        {
            return ValidationResult.Success;
        }

        List<string> errors = [];
        foreach (ValidationIssue issue in issues)
        {
            if (issue is TypeIncompatIssue || string.IsNullOrEmpty(policyId))
            {
                errors.Add(issue.Message);
            }
            else
            {
                errors.Add($"for policy `{policyId}`, {issue.Message}");
            }
        }

        return ValidationResult.Failure(errors);
    }
}

using System;
using Cedar.Ast;
using Cedar.Core;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class PolicyValidatorTests
{
    [Fact]
    public void ValidatePolicy_AllowsGuardedOptionalContextAccess()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User;
            entity Photo;

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: {
                    token?: String,
                }
            };
            """);

        Policy policy = Policy.UnmarshalCedar(
            """
            permit(principal, action, resource)
            when { context has token && context.token == "x" };
            """);

        ValidationResult result = validator.ValidatePolicy("policy0", policy);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidatePolicy_RejectsNonLiteralAttributeAccess()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User;
            entity Photo;

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: {
                    token: String,
                }
            };
            """);

        Policy policy = Policy.FromAst(
            CedarAst.Permit()
                .When(Variables.Context().AccessNode(Variables.Principal()).Equal(Values.String("x"))));

        ValidationResult result = validator.ValidatePolicy("policy0", policy);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, static error => error.Contains("attribute access requires a string literal attribute name", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatePolicy_AcceptsCyclicEntityParentSchema()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity Application;
            entity Team in [Team, Application];
            entity User in [Team];
            entity Document;

            action view appliesTo {
                principal: User,
                resource: Document,
                context: {}
            };
            """);

        Policy policy = Policy.UnmarshalCedar(
            """
            permit (
                principal is User,
                action == Action::"view",
                resource is Document
            )
            when { principal in Application::"app" };
            """);

        ValidationResult result = validator.ValidatePolicy("policy0", policy);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidatePolicy_StrictModeRejectsEmptyActionSet()
    {
        SchemaValidator validator = CreateValidator("action view;");
        Policy policy = Policy.UnmarshalCedar("permit(principal, action in [], resource);");

        ValidationResult result = validator.ValidatePolicy("policy0", policy);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, static error => error.Contains("empty set literals are forbidden in policies", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatePolicy_ReportsInvalidDecimalConstructorInsteadOfThrowing()
    {
        SchemaValidator validator = CreateValidator(
            """
            entity User;
            entity Photo;

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: {}
            };
            """);
        Policy policy = Policy.UnmarshalCedar("permit(principal, action, resource) when { decimal(\"922337203685478.0000\") == decimal(\"1.0000\") };");

        ValidationResult result = validator.ValidatePolicy("policy0", policy);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            static error => error.Contains("error during extension function argument validation: Failed to parse as a decimal value", StringComparison.Ordinal));
    }

    private static SchemaValidator CreateValidator(string schemaText)
    {
        return new SchemaValidator(SchemaDocument.UnmarshalCedar(schemaText).Resolve());
    }
}

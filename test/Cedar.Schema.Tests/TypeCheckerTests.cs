using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Cedar.Ast.Internal;
using Cedar.Schema.Internal.Validate;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class TypeCheckerTests
{
    private static readonly ResolvedSchema Schema = SchemaDocument.UnmarshalCedar(
        """
        entity Actor {
            common: String,
        };

        entity User in [Actor] {
            common: String,
            name: String,
            manager?: User,
            profile: {
                active: Bool,
                nick?: String,
            },
        } tags String;

        entity Admin in [User] {
            common: String,
            name: String,
            manager?: User,
            profile: {
                active: Bool,
                nick?: String,
            },
            level: Long,
        } tags String;

        entity Group {
            common: String,
            department: String,
        } tags String;

        entity Photo {
            common: String,
            owner: User,
            info: {
                title: String,
                caption?: String,
            },
        } tags String;

        entity Document {
            common: String,
            owner: Admin,
            info: {
                title: String,
                revision: Long,
            },
        } tags Long;

        entity Folder;

        action view appliesTo {
            principal: [User, Admin],
            resource: [Photo, Document],
            context: {
                flag: Bool,
                token?: String,
                key: String,
                nested: {
                    child?: String,
                    stable: Long,
                },
            }
        };

        action edit in [view] appliesTo {
            principal: Admin,
            resource: Document,
            context: {
                flag: Bool,
                token?: String,
                key: String,
                nested: {
                    child?: String,
                    stable: Long,
                },
            }
        };

        action group appliesTo {
            principal: Group,
            resource: Photo,
            context: {
                flag: Bool,
                token?: String,
                key: String,
                nested: {
                    child?: String,
                    stable: Long,
                },
            }
        };
        """).Resolve();

    private static readonly List<RequestEnvironment> Environments = RequestEnvironment.Generate(Schema);

    [Fact]
    public void TypeOfExpr_BooleanLiteralReturnsSingletonType()
    {
        var result = Check(Bool(true));

        AssertTypeName(result.Type, "__cedar::internal::True");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_KnownEntityLiteralReturnsEntityType()
    {
        var result = Check(Entity("User", "alice"));

        AssertTypeName(result.Type, "User");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_UnknownActionLiteralReportsDiagnostic()
    {
        var result = Check(Entity("Action", "ghost"));

        Assert.Null(result.Type);
        AssertContainsIssue(result.Errors, "unrecognized action `Action::\"ghost\"`");
    }

    [Fact]
    public void TypeOfExpr_ContextVariableReturnsContextRecordType()
    {
        var result = Check(Var("context"));

        CedarRecordType record = Assert.IsType<CedarRecordType>(result.Type);
        Assert.Equal("String", CedarTypeOps.CedarTypeName(record.Attrs["key"].Type));
        Assert.False(record.Attrs["token"].Required);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_AndPropagatesHasCapabilityToRightSide()
    {
        INode expr = new NodeAnd(
            new NodeHas(Var("context"), new Cedar.Types.CedarString("token")),
            new NodeEquals(Access(Var("context"), "token"), Str("ok")));

        var result = Check(expr);

        AssertTypeName(result.Type, "Bool");
        Assert.True(result.Caps.Has(new Capability("context", "token")));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_OrDoesNotPropagateHasCapabilityToRightSide()
    {
        INode expr = new NodeOr(
            new NodeHas(Var("context"), new Cedar.Types.CedarString("token")),
            new NodeEquals(Access(Var("context"), "token"), Str("ok")));

        var result = Check(expr);

        Assert.Null(result.Type);
        AssertContainsIssue(result.Errors, "unable to guarantee safety of access to optional attribute `token` in context");
    }

    [Fact]
    public void TypeOfExpr_OrIntersectsCapabilitiesFromBothBranches()
    {
        INode expr = new NodeOr(
            new NodeHas(Var("context"), new Cedar.Types.CedarString("token")),
            new NodeHas(Var("context"), new Cedar.Types.CedarString("token")));

        var result = Check(expr);

        AssertTypeName(result.Type, "Bool");
        Assert.True(result.Caps.Has(new Capability("context", "token")));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_NotInvertsSingletonBoolean()
    {
        var result = Check(new NodeNot(Bool(true)));

        AssertTypeName(result.Type, "__cedar::internal::False");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_IfThenElsePropagatesHasCapabilityToThenBranch()
    {
        INode expr = new NodeIfThenElse(
            new NodeHas(Var("context"), new Cedar.Types.CedarString("token")),
            new NodeEquals(Access(Var("context"), "token"), Str("ok")),
            Bool(true));

        var result = Check(expr);

        AssertTypeName(result.Type, "Bool");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypecheckConditions_AcceptsCommonAttributeAcrossEntityLubsInGeneratedEnvironments()
    {
        EntityUid view = Action("view");
        INode source = new NodeIfThenElse(Access(Var("context"), "flag"), Var("principal"), Var("resource"));
        INode condition = new NodeLike(Access(source, "common"), CedarPattern.Parse("a*"));
        List<RequestEnvironment> viewEnvironments = RequestEnvironment.FilterForPolicy(Environments, null, null, [view]);

        List<ValidationIssue> issues = CreateChecker(ValidationMode.Permissive).TypecheckConditions(viewEnvironments, ImmutableArray.Create(condition));

        Assert.Empty(issues);
    }

    [Fact]
    public void TypeOfExpr_EqualityOnSameVariableIsTrue()
    {
        var result = Check(new NodeEquals(Var("principal"), Var("principal")));

        AssertTypeName(result.Type, "__cedar::internal::True");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_NotEqualOnDisjointEntitiesIsTrue()
    {
        var result = Check(new NodeNotEquals(Var("principal"), Entity("Group", "ops")));

        AssertTypeName(result.Type, "__cedar::internal::True");
        Assert.Empty(result.Errors);
    }

    [Theory]
    [MemberData(nameof(ComparisonNodeCases))]
    public void TypeOfExpr_ComparisonNodesRequireComparableOperands(INode expr)
    {
        var result = Check(expr);

        AssertTypeName(result.Type, "Bool");
        AssertContainsIssue(result.Errors, "expected datetime, or duration, or Long but saw String");
    }

    public static IEnumerable<object[]> ComparisonNodeCases()
    {
        yield return [new NodeLessThan(Str("a"), Long(1))];
        yield return [new NodeLessThanOrEqual(Str("a"), Long(1))];
        yield return [new NodeGreaterThan(Str("a"), Long(1))];
        yield return [new NodeGreaterThanOrEqual(Str("a"), Long(1))];
    }

    [Theory]
    [MemberData(nameof(ArithmeticNodeCases))]
    public void TypeOfExpr_ArithmeticNodesRequireLongOperands(INode expr)
    {
        var result = Check(expr);

        AssertTypeName(result.Type, "Long");
        AssertContainsIssue(result.Errors, "expected Long but saw String");
    }

    public static IEnumerable<object[]> ArithmeticNodeCases()
    {
        yield return [new NodeAdd(Str("a"), Long(1))];
        yield return [new NodeSub(Str("a"), Long(1))];
        yield return [new NodeMult(Str("a"), Long(1))];
    }

    [Fact]
    public void TypeOfExpr_NegateRequiresLongOperand()
    {
        var result = Check(new NodeNegate(Str("a")));

        AssertTypeName(result.Type, "Long");
        AssertContainsIssue(result.Errors, "expected Long but saw String");
    }

    [Fact]
    public void TypeOfExpr_InExpandsActionDescendants()
    {
        INode expr = new NodeIn(Var("action"), Set(Entity("Action", "view")));

        var result = Check(expr, principalType: "Admin", actionName: "edit", resourceType: "Document");

        AssertTypeName(result.Type, "__cedar::internal::True");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_ContainsRejectsMismatchedElementType()
    {
        INode expr = new NodeContains(Set(Long(1)), Str("x"));

        var result = Check(expr);

        Assert.Null(result.Type);
        AssertContainsIssue(result.Errors, "the types Long and String are not compatible");
    }

    [Fact]
    public void TypeOfExpr_ContainsAllRejectsIncompatibleSetTypes()
    {
        INode expr = new NodeContainsAll(Set(Long(1)), Set(Str("x")));

        var result = Check(expr);

        Assert.Null(result.Type);
        AssertContainsIssue(result.Errors, "the types Set<Long> and Set<String> are not compatible");
    }

    [Fact]
    public void TypeOfExpr_ContainsAnyAcceptsCompatibleSets()
    {
        INode expr = new NodeContainsAny(Set(Long(1), Long(2)), Set(Long(2)));

        var result = Check(expr);

        AssertTypeName(result.Type, "Bool");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_IsEmptyRequiresSetType()
    {
        var result = Check(new NodeIsEmpty(Long(1)));

        Assert.Null(result.Type);
        AssertContainsIssue(result.Errors, "expected Set<__cedar::internal::Any> but saw Long");
    }

    [Fact]
    public void TypeOfExpr_LikeRequiresString()
    {
        var result = Check(new NodeLike(Long(1), CedarPattern.Parse("1*")));

        Assert.Null(result.Type);
        AssertContainsIssue(result.Errors, "expected String but saw Long");
    }

    [Fact]
    public void TypeOfExpr_IsReturnsTrueWhenTypeMatchesExactly()
    {
        var result = Check(new NodeIs(Var("principal"), new CedarPath("User")));

        AssertTypeName(result.Type, "__cedar::internal::True");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_IsReturnsFalseWhenTypeIsAbsent()
    {
        var result = Check(new NodeIs(Var("resource"), new CedarPath("User")));

        AssertTypeName(result.Type, "__cedar::internal::False");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_IsInRejectsNonEntityRightOperand()
    {
        INode expr = new NodeIsIn(Var("principal"), new CedarPath("User"), Str("not-an-entity"));

        var result = Check(expr);

        AssertTypeName(result.Type, "Bool");
        AssertContainsIssue(result.Errors, "expected Set<__cedar::internal::AnyEntity>, or __cedar::internal::AnyEntity but saw String");
    }

    [Fact]
    public void TypeOfExpr_HasOnOptionalContextFieldReturnsCapability()
    {
        var result = Check(new NodeHas(Var("context"), new Cedar.Types.CedarString("token")));

        AssertTypeName(result.Type, "Bool");
        Assert.True(result.Caps.Has(new Capability("context", "token")));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_AccessOnOptionalContextFieldWithoutGuardErrors()
    {
        var result = Check(Access(Var("context"), "token"));

        AssertTypeName(result.Type, "String");
        AssertContainsIssue(result.Errors, "unable to guarantee safety of access to optional attribute `token` in context");
    }

    [Fact]
    public void TypeOfExpr_AccessOnNestedOptionalContextFieldUsesFullPathInError()
    {
        var result = Check(Access(Access(Var("context"), "nested"), "child"));

        AssertTypeName(result.Type, "String");
        AssertContainsIssue(result.Errors, "unable to guarantee safety of access to optional attribute `nested.child` in context");
    }

    [Fact]
    public void TypeOfExpr_AccessOnStringLiteralAttributeReturnsDeclaredType()
    {
        var result = Check(Access(Var("context"), "key"));

        AssertTypeName(result.Type, "String");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_AccessOnNonLiteralAttributeReportsError()
    {
        INode expr = new NodeAccess(Var("context"), Var("principal"));

        var result = Check(expr);

        AssertTypeName(result.Type, "__cedar::internal::Never");
        AssertContainsIssue(result.Errors, "expected String but saw User");
        AssertContainsIssue(result.Errors, "attribute access requires a string literal attribute name");
    }

    [Fact]
    public void TypeOfExpr_HasTagOnEntityWithoutTagsReturnsFalse()
    {
        var result = Check(new NodeHasTag(Entity("Folder", "root"), Str("env")));

        AssertTypeName(result.Type, "__cedar::internal::False");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_GetTagWithGuardSucceeds()
    {
        INode expr = new NodeAnd(
            new NodeHasTag(Var("principal"), Str("env")),
            new NodeEquals(new NodeGetTag(Var("principal"), Str("env")), Str("prod")));

        var result = Check(expr);

        AssertTypeName(result.Type, "Bool");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_GetTagWithoutGuardErrors()
    {
        var result = Check(new NodeGetTag(Var("principal"), Str("env")));

        Assert.Null(result.Type);
        AssertContainsIssue(result.Errors, "unable to guarantee safety of access to tag `env` on entity type `User`");
    }

    [Fact]
    public void TypeOfExpr_GetTagOnEntityUnionReportsIncompatibleTagTypes()
    {
        INode taggedEntity = new NodeIfThenElse(Access(Var("context"), "flag"), Var("resource"), Var("principal"));
        INode expr = new NodeGetTag(taggedEntity, Str("env"));

        var result = Check(expr, principalType: "Admin", actionName: "edit", resourceType: "Document", mode: ValidationMode.Permissive);

        AssertTypeName(result.Type, "__cedar::internal::Never");
        AssertContainsIssue(result.Errors, "the types Long and String are not compatible");
    }

    [Fact]
    public void TypeOfExpr_RecordLiteralInfersAttributeTypes()
    {
        var result = Check(Record(("name", Str("alice")), ("count", Long(1))));

        CedarRecordType record = Assert.IsType<CedarRecordType>(result.Type);
        Assert.Equal("String", CedarTypeOps.CedarTypeName(record.Attrs["name"].Type));
        Assert.Equal("Long", CedarTypeOps.CedarTypeName(record.Attrs["count"].Type));
        Assert.All(record.Attrs.Values, static attr => Assert.True(attr.Required));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_SetLiteralRejectsMixedElementTypes()
    {
        var result = Check(Set(Long(1), Str("x")));

        Assert.Null(result.Type);
        AssertContainsIssue(result.Errors, "the types Long and String are not compatible");
    }

    [Fact]
    public void TypeOfExpr_SetLiteralInfersEntityUnionInPermissiveMode()
    {
        var result = Check(Set(Entity("User", "alice"), Entity("Admin", "bob")), mode: ValidationMode.Permissive);

        AssertTypeName(result.Type, "Set<__cedar::internal::Union<Admin, User>>");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_ExtensionCallValidatesArgumentType()
    {
        var result = Check(Call("decimal", Long(1)));

        AssertTypeName(result.Type, "decimal");
        AssertContainsIssue(result.Errors, "expected String but saw Long");
    }

    [Fact]
    public void TypeOfExpr_ExtensionCallRejectsWrongArity()
    {
        var result = Check(Call("isIpv4", Str("127.0.0.1"), Str("extra")));

        AssertTypeName(result.Type, "Bool");
        AssertContainsIssue(result.Errors, "wrong number of arguments in extension function application. Expected 1, got 2");
    }

    [Fact]
    public void TypeOfExpr_ExtensionConstructorRejectsInvalidLiteralValue()
    {
        var result = Check(Call("decimal", Str("not-a-decimal")));

        AssertTypeName(result.Type, "decimal");
        AssertContainsIssue(result.Errors, "error during extension function argument validation: Failed to parse as a decimal value");
    }

    [Fact]
    public void TypeOfExpr_ExtensionConstructorRejectsNonLiteralExpressionsInStrictMode()
    {
        var result = Check(Call("decimal", Access(Var("context"), "key")));

        AssertTypeName(result.Type, "decimal");
        AssertContainsIssue(result.Errors, "extension constructors may not be called with non-literal expressions");
    }

    private static (CedarType? Type, CapabilitySet Caps, List<ValidationIssue> Errors) Check(
        INode expr,
        string principalType = "User",
        string actionName = "view",
        string resourceType = "Photo",
        ValidationMode mode = ValidationMode.Strict,
        CapabilitySet? caps = null)
    {
        TypeChecker checker = CreateChecker(mode);
        RequestEnvironment env = Environment(principalType, actionName, resourceType);
        return checker.TypeOfExpr(env, expr, caps ?? CapabilitySet.Create());
    }

    private static TypeChecker CreateChecker(ValidationMode mode = ValidationMode.Strict)
    {
        return new TypeChecker(new SchemaValidator(Schema, mode));
    }

    private static RequestEnvironment Environment(string principalType, string actionName, string resourceType)
    {
        EntityUid actionUid = Action(actionName);
        return Environments.Single(environment =>
            environment.PrincipalType == new EntityType(principalType)
            && environment.ActionUid == actionUid
            && environment.ResourceType == new EntityType(resourceType));
    }

    private static EntityUid Action(string id)
    {
        return new EntityUid(new EntityType("Action"), new Cedar.Types.CedarString(id));
    }

    private static NodeValue Bool(bool value)
    {
        return new NodeValue(new Cedar.Types.CedarBool(value));
    }

    private static NodeValue Long(long value)
    {
        return new NodeValue(new Cedar.Types.CedarLong(value));
    }

    private static NodeValue Str(string value)
    {
        return new NodeValue(new Cedar.Types.CedarString(value));
    }

    private static NodeValue Entity(string type, string id)
    {
        return new NodeValue(new EntityUid(new EntityType(type), new Cedar.Types.CedarString(id)));
    }

    private static NodeVariable Var(string name)
    {
        return new NodeVariable(new Cedar.Types.CedarString(name));
    }

    private static NodeAccess Access(INode arg, string attr)
    {
        return new NodeAccess(arg, Str(attr));
    }

    private static NodeSet Set(params INode[] elements)
    {
        return new NodeSet(ImmutableArray.Create(elements));
    }

    private static NodeRecord Record(params (string Key, INode Value)[] elements)
    {
        ImmutableArray<NodeRecordElement>.Builder builder = ImmutableArray.CreateBuilder<NodeRecordElement>(elements.Length);
        foreach ((string key, INode value) in elements)
        {
            builder.Add(new NodeRecordElement(new Cedar.Types.CedarString(key), value));
        }

        return new NodeRecord(builder.ToImmutable());
    }

    private static NodeExtensionCall Call(string name, params INode[] args)
    {
        return new NodeExtensionCall(new CedarPath(name), ImmutableArray.Create(args));
    }

    private static void AssertTypeName(CedarType? type, string expected)
    {
        CedarType actual = Assert.IsAssignableFrom<CedarType>(type);
        Assert.Equal(expected, CedarTypeOps.CedarTypeName(actual));
    }

    private static void AssertContainsIssue(IEnumerable<ValidationIssue> issues, string expectedMessageFragment)
    {
        Assert.Contains(
            issues,
            issue => issue.Message.Contains(expectedMessageFragment, StringComparison.Ordinal));
    }
}

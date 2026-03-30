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
    public void TypeOfExpr_ComparisonNodesRequireComparableOperands(object exprObj)
    {
        var result = Check((INode)exprObj);

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
    public void TypeOfExpr_ArithmeticNodesRequireLongOperands(object exprObj)
    {
        var result = Check((INode)exprObj);

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

    [Fact]
    public void TypeOfExpr_IpConstructorRejectsInvalidLiteralValue()
    {
        var result = Check(Call("ip", Str("not-an-ip")));

        AssertTypeName(result.Type, "ipaddr");
        AssertContainsIssue(result.Errors, "Failed to parse as IP address");
    }

    [Fact]
    public void TypeOfExpr_DatetimeConstructorRejectsInvalidLiteralValue()
    {
        var result = Check(Call("datetime", Str("not-a-datetime")));

        AssertTypeName(result.Type, "datetime");
        AssertContainsIssue(result.Errors, "Failed to parse as a datetime value");
    }

    [Fact]
    public void TypeOfExpr_DurationConstructorRejectsInvalidLiteralValue()
    {
        var result = Check(Call("duration", Str("not-a-duration")));

        AssertTypeName(result.Type, "duration");
        AssertContainsIssue(result.Errors, "Failed to parse as a duration value");
    }

    [Fact]
    public void TypeOfExpr_IpConstructorRejectsNonStringArgument()
    {
        var result = Check(Call("ip", Long(1)));

        AssertTypeName(result.Type, "ipaddr");
        AssertContainsIssue(result.Errors, "expected String but saw Long");
    }

    [Fact]
    public void TypeOfExpr_DatetimeConstructorRejectsNonStringArgument()
    {
        var result = Check(Call("datetime", Long(1)));

        AssertTypeName(result.Type, "datetime");
        AssertContainsIssue(result.Errors, "expected String but saw Long");
    }

    [Fact]
    public void TypeOfExpr_DurationConstructorRejectsNonStringArgument()
    {
        var result = Check(Call("duration", Long(1)));

        AssertTypeName(result.Type, "duration");
        AssertContainsIssue(result.Errors, "expected String but saw Long");
    }

    [Fact]
    public void TypeOfExpr_IsIpv4RejectsNonIpAddrArgument()
    {
        var result = Check(Call("isIpv4", Call("decimal", Str("1.0"))));

        AssertTypeName(result.Type, "Bool");
        AssertContainsIssue(result.Errors, "expected ipaddr but saw decimal");
    }

    [Fact]
    public void TypeOfExpr_IsInRangeRejectsNonIpAddrArguments()
    {
        var result = Check(Call("isInRange", Str("x"), Str("y")));

        AssertTypeName(result.Type, "Bool");
        AssertIssueCount(result.Errors, "expected ipaddr but saw String", 2);
    }

    [Fact]
    public void TypeOfExpr_ToDateRejectsNonDatetimeArgument()
    {
        var result = Check(Call("toDate", Call("duration", Str("1d"))));

        AssertTypeName(result.Type, "datetime");
        AssertContainsIssue(result.Errors, "expected datetime but saw duration");
    }

    [Fact]
    public void TypeOfExpr_OffsetRejectsNonDatetimeFirstArgument()
    {
        var result = Check(Call("offset", Call("duration", Str("1d")), Call("duration", Str("1d"))));

        AssertTypeName(result.Type, "datetime");
        Assert.Single(result.Errors);
        AssertContainsIssue(result.Errors, "expected datetime but saw duration");
    }

    [Fact]
    public void TypeOfExpr_OffsetRejectsNonDurationSecondArgument()
    {
        var result = Check(Call("offset", Call("datetime", Str("2024-01-01")), Call("datetime", Str("2024-01-01"))));

        AssertTypeName(result.Type, "datetime");
        Assert.Single(result.Errors);
        AssertContainsIssue(result.Errors, "expected duration but saw datetime");
    }

    [Fact]
    public void TypeOfExpr_DurationSinceRejectsNonDatetimeArguments()
    {
        var result = Check(Call("durationSince", Call("duration", Str("1d")), Call("duration", Str("1d"))));

        AssertTypeName(result.Type, "duration");
        AssertIssueCount(result.Errors, "expected datetime but saw duration", 2);
    }

    [Fact]
    public void TypeOfExpr_ToDaysRejectsNonDurationArgument()
    {
        var result = Check(Call("toDays", Call("datetime", Str("2024-01-01"))));

        AssertTypeName(result.Type, "Long");
        AssertContainsIssue(result.Errors, "expected duration but saw datetime");
    }

    [Fact]
    public void TypeOfExpr_ToHoursRejectsNonDurationArgument()
    {
        var result = Check(Call("toHours", Str("x")));

        AssertTypeName(result.Type, "Long");
        AssertContainsIssue(result.Errors, "expected duration but saw String");
    }

    [Fact]
    public void TypeOfExpr_ToMinutesRejectsNonDurationArgument()
    {
        var result = Check(Call("toMinutes", Long(1)));

        AssertTypeName(result.Type, "Long");
        AssertContainsIssue(result.Errors, "expected duration but saw Long");
    }

    [Fact]
    public void TypeOfExpr_ToSecondsRejectsNonDurationArgument()
    {
        var result = Check(Call("toSeconds", Long(1)));

        AssertTypeName(result.Type, "Long");
        AssertContainsIssue(result.Errors, "expected duration but saw Long");
    }

    [Fact]
    public void TypeOfExpr_ToMillisecondsRejectsNonDurationArgument()
    {
        var result = Check(Call("toMilliseconds", Long(1)));

        AssertTypeName(result.Type, "Long");
        AssertContainsIssue(result.Errors, "expected duration but saw Long");
    }

    [Fact]
    public void TypeOfExpr_DecimalLessThanRejectsNonDecimalArgument()
    {
        AssertDecimalComparisonRejectsNonDecimalArgument("lessThan");
    }

    [Fact]
    public void TypeOfExpr_DecimalLessThanOrEqualRejectsNonDecimalArgument()
    {
        AssertDecimalComparisonRejectsNonDecimalArgument("lessThanOrEqual");
    }

    [Fact]
    public void TypeOfExpr_DecimalGreaterThanRejectsNonDecimalArgument()
    {
        AssertDecimalComparisonRejectsNonDecimalArgument("greaterThan");
    }

    [Fact]
    public void TypeOfExpr_DecimalGreaterThanOrEqualRejectsNonDecimalArgument()
    {
        AssertDecimalComparisonRejectsNonDecimalArgument("greaterThanOrEqual");
    }

    [Fact]
    public void TypeOfExpr_IpConstructorRejectsNonLiteralExpressionInStrictMode()
    {
        var result = Check(Call("ip", Access(Var("context"), "key")));

        AssertTypeName(result.Type, "ipaddr");
        Assert.Single(result.Errors);
        AssertContainsIssue(result.Errors, "extension constructors may not be called with non-literal expressions");
    }

    [Fact]
    public void TypeOfExpr_DatetimeConstructorRejectsNonLiteralExpressionInStrictMode()
    {
        var result = Check(Call("datetime", Access(Var("context"), "key")));

        AssertTypeName(result.Type, "datetime");
        Assert.Single(result.Errors);
        AssertContainsIssue(result.Errors, "extension constructors may not be called with non-literal expressions");
    }

    [Fact]
    public void TypeOfExpr_DurationConstructorRejectsNonLiteralExpressionInStrictMode()
    {
        var result = Check(Call("duration", Access(Var("context"), "key")));

        AssertTypeName(result.Type, "duration");
        Assert.Single(result.Errors);
        AssertContainsIssue(result.Errors, "extension constructors may not be called with non-literal expressions");
    }

    [Fact]
    public void TypeOfExpr_AndWithFalseLeftSkipsRightBranchTypeChecking()
    {
        var result = Check(new NodeAnd(Bool(false), Access(Var("context"), "token")));

        AssertTypeName(result.Type, "__cedar::internal::False");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_AndWithErrorLeftAndFalseRightReturnsFalse()
    {
        var result = Check(new NodeAnd(Access(Var("context"), "token"), Bool(false)));

        AssertTypeName(result.Type, "__cedar::internal::False");
        AssertContainsIssue(result.Errors, "unable to guarantee safety of access to optional attribute `token` in context");
        AssertContainsIssue(result.Errors, "expected Bool but saw String");
    }

    [Fact]
    public void TypeOfExpr_AndWithTrueLeftReturnsRightType()
    {
        var result = Check(new NodeAnd(Bool(true), Bool(false)));

        AssertTypeName(result.Type, "__cedar::internal::False");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_OrWithTrueLeftShortCircuitsRight()
    {
        var result = Check(new NodeOr(Bool(true), Access(Var("context"), "token")));

        AssertTypeName(result.Type, "__cedar::internal::True");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_OrWithFalseLeftReturnsRightType()
    {
        var result = Check(new NodeOr(Bool(false), Bool(true)));

        AssertTypeName(result.Type, "__cedar::internal::True");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_OrWithBothFalseReturnsFalse()
    {
        var result = Check(new NodeOr(Bool(false), Bool(false)));

        AssertTypeName(result.Type, "__cedar::internal::False");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TypeOfExpr_OrWithErrorLeftPropagatesErrors()
    {
        var result = Check(new NodeOr(Access(Var("context"), "token"), Bool(true)));

        Assert.Null(result.Type);
        AssertContainsIssue(result.Errors, "unable to guarantee safety of access to optional attribute `token` in context");
        AssertContainsIssue(result.Errors, "expected Bool but saw String");
    }

    [Fact]
    public void TypeOfExpr_IfThenElseWithFalseConditionSkipsThenBranch()
    {
        INode skippedThen = new NodeAnd(Entity("Action", "ghost"), Access(Var("context"), "token"));
        var result = Check(new NodeIfThenElse(Bool(false), skippedThen, Bool(true)));

        AssertTypeName(result.Type, "__cedar::internal::True");
        Assert.Single(result.Errors);
        AssertContainsIssue(result.Errors, "unrecognized action `Action::\"ghost\"`");
        Assert.DoesNotContain(
            result.Errors,
            issue => issue.Message.Contains("unable to guarantee safety of access to optional attribute `token` in context", StringComparison.Ordinal));
    }

    [Fact]
    public void TypeOfExpr_IfThenElseWithTrueConditionSkipsElseBranch()
    {
        INode skippedElse = new NodeAnd(Entity("Action", "ghost"), Access(Var("context"), "token"));
        var result = Check(new NodeIfThenElse(Bool(true), Bool(false), skippedElse));

        AssertTypeName(result.Type, "__cedar::internal::False");
        Assert.Single(result.Errors);
        AssertContainsIssue(result.Errors, "unrecognized action `Action::\"ghost\"`");
        Assert.DoesNotContain(
            result.Errors,
            issue => issue.Message.Contains("unable to guarantee safety of access to optional attribute `token` in context", StringComparison.Ordinal));
    }

    [Fact]
    public void TypeOfExpr_IfThenElseWithNonBoolConditionReportsError()
    {
        var result = Check(new NodeIfThenElse(Long(1), Bool(true), Bool(false)));

        AssertTypeName(result.Type, "Bool");
        Assert.Single(result.Errors);
        AssertContainsIssue(result.Errors, "expected Bool but saw Long");
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

    private static void AssertIssueCount(IEnumerable<ValidationIssue> issues, string expectedMessageFragment, int expectedCount)
    {
        Assert.Equal(
            expectedCount,
            issues.Count(issue => issue.Message.Contains(expectedMessageFragment, StringComparison.Ordinal)));
    }

    private static void AssertDecimalComparisonRejectsNonDecimalArgument(string functionName)
    {
        var result = Check(Call(functionName, Str("x"), Call("decimal", Str("1.0"))));

        AssertTypeName(result.Type, "Bool");
        AssertContainsIssue(result.Errors, "expected decimal but saw String");
    }
}

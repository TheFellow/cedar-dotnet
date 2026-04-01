using System;
using System.Text;
using Cedar.Ast.Internal;
using Cedar.Core.Internal.Parser;
using Xunit;

namespace Cedar.Tests.Parser;

public sealed class ParserErrorTests
{
    [Fact]
    public void InvalidEffectProducesParseException()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("allow(principal, action, resource);"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("permit", parse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingSemicolonProducesParseException()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource)"));

        Assert.Single(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidScopeVariableNameProducesParseException()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(foo, action, resource);"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("principal", parse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DuplicateAnnotationsProduceError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("@id(\"a\") @id(\"b\") permit(principal, action, resource);"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("Duplicate annotation", parse.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateRecordKeyProducesError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { {a: 1, a: 2} };"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("Duplicate record key", parse.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownMethodProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { context.unknown() };"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("`unknown` is not a method", parse.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownExtensionFunctionProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { not_an_extension_fn() };"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("`not_an_extension_fn` is not a function", parse.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtensionMethodUsedAsFunctionProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { isIpv4() };"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("`isIpv4` is a method, not a function", parse.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownExtensionMethodProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { context.not_an_extension_method() };"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("`not_an_extension_method` is not a method", parse.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtensionFunctionUsedAsMethodProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { context.ip() };"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("`ip` is a function, not a method", parse.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidLikeOperandProducesError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { context.name like 1 };"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("string literal", parse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidEntityUidInScopeProducesError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal == User::id, action, resource);"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("string literal", parse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseDepthLimitIsEnforced()
    {
        string nested = new string('(', 300) + "true" + new string(')', 300);
        string policy = $"permit(principal, action, resource) when {{ {nested} }};";

        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(policy));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("Maximum parse depth", parse.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseErrorsAreCappedAtTen()
    {
        StringBuilder source = new();
        for (int i = 0; i < 20; i++)
        {
            source.Append("permit(principal, action, resource) when { };\n");
        }

        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(source.ToString()));

        Assert.Equal(10, ex.InnerExceptions.Count);
    }

    [Fact]
    public void InvalidEscapeFromTokenizerIsReported()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { \"\\q\" };"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("Invalid escape sequence", parse.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnterminatedCommentFromTokenizerIsReported()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource); /*"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("Comment not terminated", parse.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorIncludesLineAndColumnInformation()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource)\nwhen { };"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Equal(2, parse.Position.Line);
        Assert.True(parse.Position.Column >= 1);
    }

    [Fact]
    public void ChainedHasTrailingDotProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { principal has a.b. };"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("identifier after '.'", parse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultiplePolicies_FirstInvalid_SecondStillParsed()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() =>
            CedarParser.ParsePolicies("invalid_stuff; permit(principal, action, resource);"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.NotNull(parse);
    }

    [Fact]
    public void MultiplePolicies_TwoInvalid_BothErrorsReported()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() =>
            CedarParser.ParsePolicies("broken1; broken2; permit(principal, action, resource);"));

        Assert.True(ex.InnerExceptions.Count >= 2);
    }

    [Fact]
    public void SynchronizeToNextPolicy_FindsKeywordWithoutSemicolon()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() =>
            CedarParser.ParsePolicies("garbage_no_semicolon permit(principal, action, resource);"));

        Assert.Single(ex.InnerExceptions);
    }

    [Fact]
    public void MaxDepthExceeded_ThrowsParseException()
    {
        string nested = new string('(', 300) + "1" + new string(')', 300);
        string input = $"permit(principal, action, resource) when {{ {nested} == 1 }};";

        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(input));

        ParseException parse = Assert.IsType<ParseException>(Assert.Single(ex.InnerExceptions));
        Assert.Contains("depth", parse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("permit(principal, action, resource) when { {false: 43} };", "identifier or string")]
    [InlineData("permit(principal, action, resource) when { {} has false };", "identifier or string")]
    [InlineData("permit(principal == false::\"42\", action, resource);", "entity type identifier")]
    [InlineData("permit(principal, action, resource) when { context.false };", "identifier after '.'")]
    public void ReservedKeywordsCannotBeUsedWhereIdentifiersAreExpected(string policy, string expectedMessage)
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(policy));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains(expectedMessage, parse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingScopeOpenParenProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit;"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void MissingPrincipalInScopeProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(resource, action);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void MissingResourceAndActionProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void MissingResourceProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void EofInScopeProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void MissingActionScopeProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, resource);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void MissingScopeEndParenProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource;"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void MissingEntityAfterOperatorProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal =="));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void StringLiteralAsEntityProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal == \"alice\", action, resource);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void IncompleteEntityPathProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal == User::, action, resource);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void IntegerAfterColonColonProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal == User::123, action, resource);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidEntityInActionSetProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action in [invalidEntity], resource);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Theory]
    [InlineData("@")]
    [InlineData("@\"annotate\"")]
    [InlineData("@annotate(")]
    [InlineData("@annotate[\"\"]")]
    [InlineData("@annotate(\"test\"]")]
    [InlineData("@annotate(test)")]
    public void InvalidAnnotationProducesParseError(string input)
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(input));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Theory]
    [InlineData("permit(principal, action, resource) when")]
    [InlineData("permit(principal, action, resource) when {")]
    [InlineData("permit(principal, action, resource) when {}")]
    [InlineData("permit(principal, action, resource) when { true")]
    public void InvalidConditionProducesParseError(string input)
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(input));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void HexIntegerLiteralProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { 0xabcd };"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void UnaryPlusProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { +resource.bar };"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Theory]
    [InlineData("permit(principal, action, resource) when { if }")]
    [InlineData("permit(principal, action, resource) when { if true }")]
    [InlineData("permit(principal, action, resource) when { if true then }")]
    [InlineData("permit(principal, action, resource) when { if true then principal }")]
    [InlineData("permit(principal, action, resource) when { if true then principal else }")]
    public void InvalidIfThenElseProducesParseError(string input)
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(input));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Theory]
    [InlineData("permit(principal, action, resource) when { resource.")]
    [InlineData("permit(principal, action, resource) when { resource.bar.123 };")]
    [InlineData("permit(principal, action, resource) when { resource.bar[")]
    public void InvalidAccessProducesParseError(string input)
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(input));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Theory]
    [InlineData("permit(principal, action, resource) when { resource.getTag(42)}")]
    [InlineData("permit(principal, action, resource) when { resource.hasTag(42)}")]
    [InlineData("permit(principal, action, resource) when { resource.hasTag(true)}")]
    [InlineData("permit(principal, action, resource) when { \"blue\".hasTag(\"true\")}")]
    [InlineData("permit(principal, action, resource) when { 42.hasTag(\"true\")}")]
    [InlineData("permit(principal, action, resource) when { true.hasTag(\"true\")}")]
    public void InvalidTagOperationsProduceParseError(string input)
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(input));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidHasOperandProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { resource.name has 123 };"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidActionEqRhsProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action == Foo, resource);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidActionInRhsProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action in Foo, resource);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Theory]
    [InlineData("permit(principal, action, resource) when { {")]
    [InlineData("permit(principal, action, resource) when { {123: \"value\"} };")]
    [InlineData("permit(principal, action, resource) when { {\"key\" \"value\"} };")]
    [InlineData("permit(principal, action, resource) when { {\"key\":")]
    [InlineData("permit(principal, action, resource) when { {\"key1\": \"value1\" \"key2\": \"value2\" };")]
    public void InvalidRecordLiteralProducesParseError(string input)
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(input));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidIsTypeProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal is 1, action, resource);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void VeryNegativeLongOverflowProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { -9223372036823454775808 < -9224323372036854775807 };"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void VeryPositiveLongOverflowProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { 9223372036823454775808 < 9224323372036854775807 };"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Theory]
    [InlineData("permit(principal, action, resource) when { context.set.isEmpty(\"foo\") };", "isEmpty")]
    [InlineData("permit(principal, action, resource) when { context.set.contains() };", "contains")]
    public void IncorrectMethodArityProducesParseError(string input, string expectedMessage)
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(input));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains(expectedMessage, parse.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidPrimaryProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { foobar };"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidStringEscapeInAnnotationProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("@bananas(\"\\*\") permit(principal, action, resource);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidStringEscapeInEntityIdProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal == User::\"\\*\", action, resource);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidStringEscapeInHasProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { context has \"\\*\" };"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidPatternInLikeProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { context.key like \"\\u{DFFF}\" };"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidStringEscapeInPrimaryProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { context.key == \"\\*\" };"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidStringEscapeInBracketAccessProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { context[\"\\*\"] == 42 };"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidStringEscapeInRecordKeyProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { { \"\\*\":42 } };"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Theory]
    [InlineData("permit(principal, action, resource) when { resource.bar(")]
    [InlineData("permit(principal, action, resource) when { resource.bar(]")]
    [InlineData("permit(principal, action, resource) when { resource.bar(,)")]
    [InlineData("permit(principal, action, resource) when { resource.bar[baz]")]
    [InlineData("permit(principal, action, resource) when { resource.bar[\"baz\")")]
    public void InvalidMethodCallOrBracketAccessProducesParseError(string input)
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(input));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Theory]
    [InlineData("permit(principal, action, resource) when { abcd")]
    [InlineData("permit(principal, action, resource) when { abcd(")]
    [InlineData("permit(principal, action, resource) when { abcd::")]
    [InlineData("permit(principal, action, resource) when { abcd::123")]
    [InlineData("permit(principal, action, resource) when { abcd(123")]
    public void IncompleteExtensionFunctionOrEntityProducesParseError(string input)
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(input));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void EofAfterAddOperatorProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { resource.foo +"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void EofAfterInOperatorProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { resource.name in"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void EofAfterHasKeywordProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { resource.name has"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void EofAfterLikeKeywordProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { resource.name like"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void EofInSetLiteralProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { ["));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void MismatchedBracesInPrimaryProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { ( }"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Theory]
    [InlineData("permit(principal, action in [User::\"alice\", invalidEntity], resource);")]
    [InlineData("permit(principal, action in [User::\"alice\";], resource);")]
    [InlineData("permit(principal, action in [User::\"alice\"")]
    public void InvalidActionEntityListProducesParseError(string input)
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(input));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidIsLongTypeProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal is X::1, action, resource);"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void EofAfterEntityColonColonProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal == User::"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void MissingEffectWithAnnotationProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("@id(\"test\")"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Theory]
    [InlineData("permit(principal is T in error);")]
    [InlineData("permit(principal in error);")]
    [InlineData("permit(principal, action, resource == error);")]
    [InlineData("permit(principal, action, resource is T in error);")]
    [InlineData("permit(principal, action, resource in error);")]
    public void InvalidScopeEntityReferencesProduceParseError(string input)
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies(input));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void EofAfterOrOperatorProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { true ||"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void EofAfterAndOperatorProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { true &&"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void EofAfterIsKeywordInConditionProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { context is"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void EofAfterIsInExpressionProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { context is T in"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void EofAfterMultiplyOperatorProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { 42 *"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void ClosingParenInsteadOfBraceProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { (42}"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void ClosingBraceInFunctionCallProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { ip(}"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void MissingCommaInFunctionCallProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { ip(42 42)"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void UnexpectedTokenAfterEntityOrExtFunProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { A::B 42 }"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidStringEscapeInExtensionFunctionProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { principal == User::\"\\*\" };"));

        Assert.NotEmpty(ex.InnerExceptions);
    }

    [Fact]
    public void InvalidResourceIsWithStringProducesParseError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource is \"error\");"));

        Assert.NotEmpty(ex.InnerExceptions);
    }
}

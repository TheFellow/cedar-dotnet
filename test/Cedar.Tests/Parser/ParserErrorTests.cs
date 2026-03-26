using System;
using System.Text;
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
    public void UnknownMethodProducesError()
    {
        AggregateException ex = Assert.Throws<AggregateException>(() => CedarParser.ParsePolicies("permit(principal, action, resource) when { context.unknown() };"));

        ParseException parse = Assert.Single(ex.InnerExceptions) as ParseException ?? throw new InvalidOperationException();
        Assert.Contains("Unknown method", parse.Message, StringComparison.Ordinal);
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
}

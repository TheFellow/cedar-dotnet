using System;
using System.Collections.Generic;
using Cedar.Ast;
using Cedar.Core;
using Cedar.Experimental;
using Cedar.Types;
using Xunit;

namespace Cedar.Experimental.Tests;

public sealed class NodeEvaluationTests
{
    private static readonly EntityUid Alice = new(new EntityType("User"), new CedarString("alice"));
    private static readonly EntityUid Read = new(new EntityType("Action"), new CedarString("read"));
    private static readonly EntityUid Doc1 = new(new EntityType("Document"), new CedarString("doc1"));
    private static readonly EntityUid Group = new(new EntityType("Group"), new CedarString("admins"));
    private static readonly EntityUid Tagged = new(new EntityType("Service"), new CedarString("svc"));

    [Fact]
    public void EvaluatesAttributeAccess()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Record(new Values.RecordElement("level", Values.Long(42))).Access("level"),
            new EvalEnv());

        Assert.Equal(new CedarLong(42), value);
    }

    [Fact]
    public void EvaluatesHasTag()
    {
        EntityMap entities = new(
        [
            new Entity(Tagged, new EntityUidSet(), new CedarRecord(), Record(("critical", new CedarBool(true))))
        ]);

        ICedarData value = NodeEvaluation.Evaluate(
            Values.EntityUid(Tagged).HasTag(Values.String("critical")),
            new EvalEnv(entities: entities));

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesIfThenElse()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Boolean(true).IfThenElse(Values.Long(1), Values.Long(2)),
            new EvalEnv());

        Assert.Equal(new CedarLong(1), value);
    }

    [Fact]
    public void EvaluatesIsInAgainstEntityHierarchy()
    {
        EntityMap entities = new(
        [
            new Entity(Alice, new EntityUidSet([Group]), new CedarRecord(), new CedarRecord())
        ]);

        ICedarData value = NodeEvaluation.Evaluate(
            Values.EntityUid(Alice).IsIn(new EntityType("User"), Values.EntityUid(Group)),
            new EvalEnv(entities: entities));

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesPrincipalVariableFromEnvironment()
    {
        ICedarData value = NodeEvaluation.Evaluate(Variables.Principal(), new EvalEnv(principal: Alice));

        Assert.Equal(Alice, value);
    }

    [Fact]
    public void EvaluatesContextVariableFromEnvironment()
    {
        CedarRecord context = Record(("level", new CedarLong(7)));

        ICedarData value = NodeEvaluation.Evaluate(Variables.Context(), new EvalEnv(context: context));

        Assert.Equal(context, value);
    }

    [Fact]
    public void EvaluatesSetContains()
    {
        ICedarData value = NodeEvaluation.Evaluate(
            Values.Set(Values.Long(1), Values.Long(2), Values.Long(3)).Contains(Values.Long(2)),
            new EvalEnv());

        Assert.Equal(CedarBool.True, value);
    }

    [Fact]
    public void EvaluatesExtensionCalls()
    {
        ICedarData value = NodeEvaluation.Evaluate(ExtensionOperators.Ip(Values.String("127.0.0.1/32")), new EvalEnv());

        Assert.Equal(CedarIpAddress.Parse("127.0.0.1/32"), value);
    }

    [Fact]
    public void UnknownExtensionThrows()
    {
        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(Operators.ExtensionCall("unknown", Values.String("x")), new EvalEnv()));

        Assert.Contains("function does not exist", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialErrorNodeThrowsStoredMessage()
    {
        Exception exception = Assert.ThrowsAny<Exception>(() => NodeEvaluation.Evaluate(PartialEvaluation.PartialError("boom"), new EvalEnv()));

        Assert.Equal("boom", exception.Message);
    }

    private static CedarRecord Record(params (string Key, ICedarData Value)[] entries)
    {
        Dictionary<CedarString, ICedarData> result = [];
        foreach ((string key, ICedarData value) in entries)
        {
            result.Add(new CedarString(key), value);
        }

        return new CedarRecord(result);
    }
}

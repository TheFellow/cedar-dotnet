using System;
using System.IO;
using Cedar.Experimental;
using Cedar.Types;
using Xunit;

namespace Cedar.Experimental.Tests;

public sealed class DotWriterTests
{
    [Fact]
    public void EmptyGraph_WritesPrelude()
    {
        string dot = EntityGraphDotWriter.ToDot(new EntityMap());

        Assert.Contains("strict digraph", dot, StringComparison.Ordinal);
        Assert.Contains("ordering=\"out\"", dot, StringComparison.Ordinal);
        Assert.DoesNotContain("->", dot, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesNodesAndEdges()
    {
        EntityUid group = new(new EntityType("Group"), new CedarString("admins"));
        EntityUid alice = new(new EntityType("User"), new CedarString("alice"));
        EntityUid bob = new(new EntityType("User"), new CedarString("bob"));
        EntityMap entities = new(
        [
            new Entity(group, new EntityUidSet(), new CedarRecord(), new CedarRecord()),
            new Entity(alice, new EntityUidSet([group]), new CedarRecord(), new CedarRecord()),
            new Entity(bob, new EntityUidSet(), new CedarRecord(), new CedarRecord())
        ]);

        string dot = EntityGraphDotWriter.ToDot(entities);

        Assert.Contains("\"cluster_Group\"", dot, StringComparison.Ordinal);
        Assert.Contains("\"cluster_User\"", dot, StringComparison.Ordinal);
        Assert.Contains("\"User::\\\"alice\\\"\" -> \"Group::\\\"admins\\\"\"", dot, StringComparison.Ordinal);
    }

    [Fact]
    public void QuotesIdentifiersAndLabels()
    {
        EntityUid quoted = new(new EntityType("User"), new CedarString("alice\"ops"));
        EntityMap entities = new([new Entity(quoted, new EntityUidSet(), new CedarRecord(), new CedarRecord())]);

        string dot = EntityGraphDotWriter.ToDot(entities);

        Assert.Contains("alice\\\"ops", dot, StringComparison.Ordinal);
        Assert.Contains($"\"{quoted.Type.Value}\"", dot, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdersClustersByType()
    {
        EntityUid user = new(new EntityType("User"), new CedarString("alice"));
        EntityUid group = new(new EntityType("Group"), new CedarString("admins"));
        EntityMap entities = new(
        [
            new Entity(user, new EntityUidSet(), new CedarRecord(), new CedarRecord()),
            new Entity(group, new EntityUidSet(), new CedarRecord(), new CedarRecord())
        ]);

        string dot = EntityGraphDotWriter.ToDot(entities);

        Assert.True(dot.IndexOf("\"cluster_Group\"", StringComparison.Ordinal) < dot.IndexOf("\"cluster_User\"", StringComparison.Ordinal));
    }

    [Fact]
    public void NoEdgesWhenNoParents()
    {
        EntityMap entities = new(
        [
            new Entity(new EntityUid(new EntityType("TypeA"), new CedarString("a1")), new EntityUidSet(), new CedarRecord(), new CedarRecord()),
            new Entity(new EntityUid(new EntityType("TypeB"), new CedarString("b1")), new EntityUidSet(), new CedarRecord(), new CedarRecord())
        ]);

        string dot = EntityGraphDotWriter.ToDot(entities);

        Assert.DoesNotContain("->", dot, StringComparison.Ordinal);
    }

    [Fact]
    public void WriterFailuresPropagate()
    {
        EntityUid group = new(new EntityType("Group"), new CedarString("admins"));
        EntityMap entities = new([new Entity(group, new EntityUidSet(), new CedarRecord(), new CedarRecord())]);
        ThrowingWriter writer = new();

        IOException exception = Assert.Throws<IOException>(() => EntityGraphDotWriter.Write(writer, entities));

        Assert.Equal("write failed", exception.Message);
    }

    private sealed class ThrowingWriter : StringWriter
    {
        public override void WriteLine(string? value)
        {
            throw new IOException("write failed");
        }
    }
}

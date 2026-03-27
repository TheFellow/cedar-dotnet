using System;
using System.Linq;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

public sealed class SchemaResolverTests
{
    [Fact]
    public void Resolve_ActionHasCorrectEntityUid()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar("action view;");

        ResolvedSchema resolved = document.Resolve();
        ResolvedAction action = Assert.Single(resolved.Actions).Value;

        Assert.Equal(new EntityUid(new EntityType("Action"), new CedarString("view")), action.Entity.Uid);
        Assert.Empty(action.Entity.Parents);
    }

    [Fact]
    public void Resolve_ActionParentsStoredAsEntityUidSet()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar("action edit in [view];\naction view;");

        ResolvedSchema resolved = document.Resolve();
        ResolvedAction action = resolved.Actions[new EntityUid(new EntityType("Action"), new CedarString("edit"))];
        EntityUid expectedParent = new(new EntityType("Action"), new CedarString("view"));

        Assert.True(action.Entity.Parents.Contains(expectedParent));
        Assert.Equal(expectedParent, Assert.Single(action.Entity.Parents));
    }

    [Fact]
    public void Resolve_ActionWithQualifiedParent()
    {
        const string cedar =
            """
            namespace NS {
            	action view;
            	action edit in [NS::Action::"view"];
            }
            """;

        SchemaDocument document = SchemaDocument.UnmarshalCedar(cedar);

        ResolvedSchema resolved = document.Resolve();
        ResolvedAction action = resolved.Actions[new EntityUid(new EntityType("NS::Action"), new CedarString("edit"))];
        EntityUid expectedParent = new(new EntityType("NS::Action"), new CedarString("view"));

        Assert.True(action.Entity.Parents.Contains(expectedParent));
        Assert.Equal(expectedParent, Assert.Single(action.Entity.Parents));
    }

    [Fact]
    public void Resolve_UndefinedParentThrows()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar("action edit in [ghost];");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => document.Resolve());

        Assert.Contains("undefined parent action", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_CycleDetected()
    {
        SchemaDocument document = SchemaDocument.UnmarshalCedar("action a in [b];\naction b in [a];");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => document.Resolve());

        Assert.Contains("cycle detected in action hierarchy", exception.Message, StringComparison.Ordinal);
    }
}
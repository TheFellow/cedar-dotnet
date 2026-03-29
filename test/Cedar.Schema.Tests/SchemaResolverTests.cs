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

    [Fact]
    public void Resolve_ResolvesEntitiesEnumsAndNamespaces()
    {
        const string cedar =
            """
            entity Group;
            entity User in [Group] {
                name: String,
            };

            entity Role enum ["admin", "user"];

            namespace App {
                entity Document in [User] {
                    owner: User,
                };

                entity Status enum ["draft", "live"];
            }
            """;

        ResolvedSchema resolved = SchemaDocument.UnmarshalCedar(cedar).Resolve();

        ResolvedEntity user = resolved.Entities[new EntityType("User")];
        Assert.Equal(new EntityType("Group"), Assert.Single(user.ParentTypes));
        Assert.IsType<ResolvedStringType>(user.Shape.Attributes["name"].Type);

        ResolvedEnum role = resolved.Enums[new EntityType("Role")];
        Assert.Equal(
            [new EntityUid(new EntityType("Role"), new CedarString("admin")), new EntityUid(new EntityType("Role"), new CedarString("user"))],
            role.Values);

        ResolvedEntity document = resolved.Entities[new EntityType("App::Document")];
        Assert.Equal(new EntityType("User"), Assert.Single(document.ParentTypes));
        Assert.Equal(new EntityType("User"), Assert.IsType<ResolvedEntityType>(document.Shape.Attributes["owner"].Type).Name);

        ResolvedNamespace ns = resolved.Namespaces["App"];
        Assert.Equal("App", ns.Name);
        Assert.Empty(ns.Annotations);
    }

    [Fact]
    public void Resolve_InlinesCommonTypesAndResolvesActionContextPath()
    {
        const string cedar =
            """
            type Address = {
                street: String,
                zip: Long,
            };

            type Context = {
                requester: User,
                addr: Address,
            };

            entity User;
            entity Photo {
                metadata: Address,
            };

            action view appliesTo {
                principal: User,
                resource: Photo,
                context: Context,
            };
            """;

        ResolvedSchema resolved = SchemaDocument.UnmarshalCedar(cedar).Resolve();

        ResolvedRecordType metadata = Assert.IsType<ResolvedRecordType>(resolved.Entities[new EntityType("Photo")].Shape.Attributes["metadata"].Type);
        Assert.IsType<ResolvedStringType>(metadata.Attributes["street"].Type);
        Assert.IsType<ResolvedLongType>(metadata.Attributes["zip"].Type);

        ResolvedAppliesTo appliesTo = Assert.IsType<ResolvedAppliesTo>(resolved.Actions[new EntityUid(new EntityType("Action"), new CedarString("view"))].AppliesTo);
        ResolvedRecordType context = appliesTo.Context;
        Assert.Equal(new EntityType("User"), Assert.IsType<ResolvedEntityType>(context.Attributes["requester"].Type).Name);

        ResolvedRecordType address = Assert.IsType<ResolvedRecordType>(context.Attributes["addr"].Type);
        Assert.IsType<ResolvedStringType>(address.Attributes["street"].Type);
        Assert.IsType<ResolvedLongType>(address.Attributes["zip"].Type);
    }

    [Fact]
    public void Resolve_ResolvesCrossNamespaceParentsAndBareFallback()
    {
        const string cedar =
            """
            entity User;

            namespace App {
                entity Team;

                entity Document in [User, Team] {
                    owner: User,
                    team: Team,
                };
            }
            """;

        ResolvedSchema resolved = SchemaDocument.UnmarshalCedar(cedar).Resolve();

        ResolvedEntity document = resolved.Entities[new EntityType("App::Document")];
        Assert.Equal([new EntityType("User"), new EntityType("App::Team")], document.ParentTypes);
        Assert.Equal(new EntityType("User"), Assert.IsType<ResolvedEntityType>(document.Shape.Attributes["owner"].Type).Name);
        Assert.Equal(new EntityType("App::Team"), Assert.IsType<ResolvedEntityType>(document.Shape.Attributes["team"].Type).Name);
    }

    [Fact]
    public void Resolve_ResolvesExtensionTypesInEntityAndContext()
    {
        const string cedar =
            """
            entity User {
                ip: ipaddr,
                amount: decimal,
                ts: datetime,
                ttl: duration,
            };

            entity Document;

            action view appliesTo {
                principal: User,
                resource: Document,
                context: {
                    sourceIp: ipaddr,
                }
            };
            """;

        ResolvedSchema resolved = SchemaDocument.UnmarshalCedar(cedar).Resolve();

        ResolvedEntity user = resolved.Entities[new EntityType("User")];
        Assert.Equal("ipaddr", Assert.IsType<ResolvedExtensionType>(user.Shape.Attributes["ip"].Type).Name.Value);
        Assert.Equal("decimal", Assert.IsType<ResolvedExtensionType>(user.Shape.Attributes["amount"].Type).Name.Value);
        Assert.Equal("datetime", Assert.IsType<ResolvedExtensionType>(user.Shape.Attributes["ts"].Type).Name.Value);
        Assert.Equal("duration", Assert.IsType<ResolvedExtensionType>(user.Shape.Attributes["ttl"].Type).Name.Value);

        ResolvedAction action = resolved.Actions[new EntityUid(new EntityType("Action"), new CedarString("view"))];
        Assert.Equal("ipaddr", Assert.IsType<ResolvedExtensionType>(action.AppliesTo!.Context.Attributes["sourceIp"].Type).Name.Value);
    }

    [Fact]
    public void Resolve_ShadowingRejectsNamespacedType()
    {
        const string cedar =
            """
            entity User;

            namespace App {
                entity User;
            }
            """;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => SchemaDocument.UnmarshalCedar(cedar).Resolve());

        Assert.Contains("illegally shadows", exception.Message, StringComparison.Ordinal);
        Assert.Contains("App::User", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_ShadowingRejectsNamespacedAction()
    {
        const string cedar =
            """
            action view;

            namespace App {
                action view;
            }
            """;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => SchemaDocument.UnmarshalCedar(cedar).Resolve());

        Assert.Contains("illegally shadows", exception.Message, StringComparison.Ordinal);
        Assert.Contains("App::Action", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_CommonTypeCycleDetected()
    {
        const string cedar =
            """
            type A = B;
            type B = C;
            type C = A;
            """;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => SchemaDocument.UnmarshalCedar(cedar).Resolve());

        Assert.Contains("cycle detected in common type definitions", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_QualifiedTypeReferencePrefersNamespacedCommonTypeThenBareEntity()
    {
        const string cedar =
            """
            entity User;

            namespace App {
                type LocalShape = {
                    kind: Long,
                };

                entity Doc {
                    local: LocalShape,
                    owner: User,
                };
            }
            """;

        ResolvedSchema resolved = SchemaDocument.UnmarshalCedar(cedar).Resolve();

        ResolvedEntity doc = resolved.Entities[new EntityType("App::Doc")];
        ResolvedRecordType local = Assert.IsType<ResolvedRecordType>(doc.Shape.Attributes["local"].Type);
        Assert.IsType<ResolvedLongType>(local.Attributes["kind"].Type);
        Assert.Equal(new EntityType("User"), Assert.IsType<ResolvedEntityType>(doc.Shape.Attributes["owner"].Type).Name);
    }
}

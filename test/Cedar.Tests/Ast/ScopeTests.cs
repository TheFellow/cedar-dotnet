using Cedar.Ast.Internal;
using Cedar.Types;
using Xunit;

namespace Cedar.Tests.Ast;

public sealed class ScopeTests
{
    [Fact]
    public void ScopeAllConstructs()
    {
        ScopeAll scope = new();

        Assert.IsType<ScopeAll>(scope);
    }

    [Fact]
    public void ScopeEqStoresEntity()
    {
        EntityUid entity = new(new EntityType("User"), new CedarString("alice"));

        ScopeEq scope = new(entity);

        Assert.Equal(entity, scope.Entity);
    }

    [Fact]
    public void ScopeInStoresEntity()
    {
        EntityUid entity = new(new EntityType("Group"), new CedarString("admins"));

        ScopeIn scope = new(entity);

        Assert.Equal(entity, scope.Entity);
    }

    [Fact]
    public void ScopeInSetStoresEntities()
    {
        EntityUid[] entities =
        [
            new EntityUid(new EntityType("Action"), new CedarString("read")),
            new EntityUid(new EntityType("Action"), new CedarString("write"))
        ];

        ScopeInSet scope = new(entities);

        Assert.Equal(2, scope.Entities.Length);
        Assert.Equal(entities[0], scope.Entities[0]);
        Assert.Equal(entities[1], scope.Entities[1]);
    }

    [Fact]
    public void ScopeIsStoresType()
    {
        ScopeIs scope = new(new EntityType("Document"));

        Assert.Equal("Document", scope.Type.Value);
    }

    [Fact]
    public void ScopeIsInStoresTypeAndEntity()
    {
        EntityUid container = new(new EntityType("Folder"), new CedarString("prod"));

        ScopeIsIn scope = new(new EntityType("Document"), container);

        Assert.Equal("Document", scope.Type.Value);
        Assert.Equal(container, scope.Entity);
    }
}

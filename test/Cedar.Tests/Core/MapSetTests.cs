using System.Linq;
using Cedar.Core.Internal.MapSet;
using Xunit;

namespace Cedar.Tests.Core;

public sealed class MapSetTests
{
    [Fact]
    public void BuilderAddsUniqueItems()
    {
        MapSetBuilder<int> builder = new();

        Assert.True(builder.Add(1));
        Assert.True(builder.Add(2));
        Assert.Equal(2, builder.Count);
    }

    [Fact]
    public void BuilderDeduplicatesDuplicateItems()
    {
        MapSetBuilder<int> builder = new();

        Assert.True(builder.Add(1));
        Assert.False(builder.Add(1));
        Assert.Equal(1, builder.Count);
    }

    [Fact]
    public void ImmutableSetContainsStoredItem()
    {
        ImmutableMapSet<int> set = new MapSetBuilder<int>([1, 2, 3]).Build();

        Assert.True(set.Contains(2));
    }

    [Fact]
    public void ImmutableSetDoesNotContainMissingItem()
    {
        ImmutableMapSet<int> set = new MapSetBuilder<int>([1, 2, 3]).Build();

        Assert.False(set.Contains(4));
    }

    [Fact]
    public void ImmutableSetReportsCount()
    {
        ImmutableMapSet<int> set = new MapSetBuilder<int>([1, 2, 3]).Build();

        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void EqualReturnsTrueForSameItems()
    {
        ImmutableMapSet<int> left = new MapSetBuilder<int>([1, 2, 3]).Build();
        ImmutableMapSet<int> right = new MapSetBuilder<int>([3, 2, 1]).Build();

        Assert.True(left.Equal(right));
    }

    [Fact]
    public void EqualReturnsFalseForDifferentSizes()
    {
        ImmutableMapSet<int> left = new MapSetBuilder<int>([1, 2, 3]).Build();
        ImmutableMapSet<int> right = new MapSetBuilder<int>([1, 2]).Build();

        Assert.False(left.Equal(right));
    }

    [Fact]
    public void EnumeratorVisitsAllItems()
    {
        ImmutableMapSet<int> set = new MapSetBuilder<int>([1, 2, 3]).Build();

        Assert.Equal(3, set.Count());
    }

    [Fact]
    public void BuilderCanStartFromEnumerable()
    {
        MapSetBuilder<string> builder = new(["a", "b"]);

        Assert.Equal(2, builder.Count);
    }

    [Fact]
    public void BuildCapturesSnapshotBeforeFurtherMutations()
    {
        MapSetBuilder<int> builder = new([1]);
        ImmutableMapSet<int> set = builder.Build();

        builder.Add(2);

        Assert.True(set.Contains(1));
        Assert.False(set.Contains(2));
    }

    [Fact]
    public void EmptyImmutableSetIsEqualToAnotherEmptySet()
    {
        Assert.True(new ImmutableMapSet<int>().Equal(new ImmutableMapSet<int>()));
    }

    [Fact]
    public void EmptyImmutableSetHasZeroCount()
    {
        Assert.Empty(new ImmutableMapSet<int>());
    }

    [Fact]
    public void EqualReturnsFalseForDifferentItems()
    {
        ImmutableMapSet<int> left = new MapSetBuilder<int>([1, 2, 3]).Build();
        ImmutableMapSet<int> right = new MapSetBuilder<int>([1, 2, 4]).Build();

        Assert.False(left.Equal(right));
    }

    [Fact]
    public void EqualReturnsTrueForSelfReference()
    {
        ImmutableMapSet<int> set = new MapSetBuilder<int>([1, 2, 3]).Build();

        Assert.True(set.Equal(set));
    }

    [Fact]
    public void EqualReturnsFalseForNull()
    {
        ImmutableMapSet<int> set = new MapSetBuilder<int>([1, 2, 3]).Build();

        Assert.False(set.Equal(null));
    }

    [Fact]
    public void EmptySetContainsNothing()
    {
        ImmutableMapSet<int> set = new MapSetBuilder<int>().Build();

        Assert.Empty(set);
        Assert.False(set.Contains(1));
    }

    [Fact]
    public void BuilderDeduplicatesFromEnumerable()
    {
        MapSetBuilder<int> builder = new([1, 1, 2, 2, 3]);
        ImmutableMapSet<int> set = builder.Build();

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
        Assert.True(set.Contains(3));
    }
}

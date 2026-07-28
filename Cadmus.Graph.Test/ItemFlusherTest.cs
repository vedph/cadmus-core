using System;
using System.Collections.Generic;
using Xunit;

namespace Cadmus.Graph.Test;

public sealed class ItemFlusherTest
{
    [Fact]
    public void Ctor_SizeLessThanOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ItemFlusher<int>(_ => { }, 0));
    }

    [Fact]
    public void Ctor_NullFlush_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ItemFlusher<int>(null!));
    }

    [Fact]
    public void Add_ReachesSize_TriggersFlush()
    {
        List<IList<int>> flushed = [];
        using ItemFlusher<int> flusher = new(items => flushed.Add(items), 2);

        flusher.Add(1);
        flusher.Add(2);

        Assert.Single(flushed);
        Assert.Equal([1, 2], flushed[0]);
    }

    [Fact]
    public void Add_BelowSize_DoesNotFlushYet()
    {
        List<IList<int>> flushed = [];
        using ItemFlusher<int> flusher = new(items => flushed.Add(items), 3);

        flusher.Add(1);

        Assert.Empty(flushed);
    }

    [Fact]
    public void Dispose_FlushesRemainingItems()
    {
        List<IList<int>> flushed = [];
        ItemFlusher<int> flusher = new(items => flushed.Add(items), 10);
        flusher.Add(1);
        flusher.Add(2);

        flusher.Dispose();

        Assert.Single(flushed);
        Assert.Equal(2, flushed[0].Count);
    }

    [Fact]
    public void Dispose_Twice_IsNoop()
    {
        int flushCount = 0;
        ItemFlusher<int> flusher = new(_ => flushCount++, 10);
        flusher.Add(1);

        flusher.Dispose();
        flusher.Dispose();

        Assert.Equal(1, flushCount);
    }
}

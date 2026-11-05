using Cadmus.Core;
using Cadmus.Core.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Index.Test;

public sealed class ItemIndexerTest
{
    private static List<IItem> GetItems(int count) =>
        Enumerable.Range(1, count).Select(n => (IItem)new Item
        {
            Id = $"{n:00000000}-0000-0000-0000-000000000000",
            Title = "Item " + n,
            Description = "Item",
            FacetId = "default",
            SortKey = $"{n:000}",
            CreatorId = "zeus",
            UserId = "zeus"
        }).ToList();

    [Fact]
    public void Ctor_NullWriter_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ItemIndexer(null!));
    }

    [Fact]
    public async Task AddItemAsync_Null_Throws()
    {
        ItemIndexer indexer = new(new FakeItemIndexWriter());
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => indexer.AddItemAsync(null!));
    }

    [Fact]
    public async Task AddItemAsync_Ok()
    {
        FakeItemIndexWriter writer = new();
        ItemIndexer indexer = new(writer);
        IItem item = GetItems(1)[0];

        await indexer.AddItemAsync(item);

        Assert.Equal(["write:" + item.Id], writer.Calls);
    }

    [Fact]
    public async Task ClearAsync_Ok()
    {
        FakeItemIndexWriter writer = new();
        await new ItemIndexer(writer).ClearAsync();
        Assert.Equal(["clear"], writer.Calls);
    }

    [Fact]
    public async Task BuildAsync_NullRepository_Throws()
    {
        ItemIndexer indexer = new(new FakeItemIndexWriter());
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => indexer.BuildAsync(null!));
    }

    [Fact]
    public async Task BuildAsync_Empty_Nope()
    {
        FakeItemIndexWriter writer = new();
        int count = await new ItemIndexer(writer).BuildAsync(
            FakeRepositoryProxy.Create([]));
        Assert.Equal(0, count);
        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task BuildAsync_Pages_WritesBatches()
    {
        FakeItemIndexWriter writer = new();
        List<IItem> items = GetItems(250);
        RecordingProgress progress = new();

        int count = await new ItemIndexer(writer).BuildAsync(
            FakeRepositoryProxy.Create(items), new ItemFilter(), progress);

        Assert.Equal(250, count);
        Assert.Equal(["writes:100", "writes:100", "writes:50"], writer.Calls);
        Assert.Equal(items.Select(i => i.Id), writer.WrittenIds);
        Assert.Equal([(100, 40), (200, 80), (250, 100)], progress.Reports);
    }

    [Fact]
    public async Task BuildAsync_MissingItem_Skipped()
    {
        FakeItemIndexWriter writer = new();
        List<IItem> items = GetItems(3);

        await new ItemIndexer(writer).BuildAsync(
            FakeRepositoryProxy.Create(items, [items[1].Id]));

        Assert.Equal([items[0].Id, items[2].Id], writer.WrittenIds);
    }

    [Fact]
    public async Task BuildAsync_AllMissingInPage_NoWrite()
    {
        FakeItemIndexWriter writer = new();
        List<IItem> items = GetItems(2);

        await new ItemIndexer(writer).BuildAsync(
            FakeRepositoryProxy.Create(items, items.Select(i => i.Id)));

        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task BuildAsync_FailingItem_FallbackSkipsIt()
    {
        List<IItem> items = GetItems(3);
        FakeItemIndexWriter writer = new();
        writer.FailingIds.Add(items[1].Id);

        int count = await new ItemIndexer(writer).BuildAsync(
            FakeRepositoryProxy.Create(items));

        Assert.Equal(3, count);
        Assert.Equal(["writes:3",
            "write:" + items[0].Id,
            "write:" + items[1].Id,
            "write:" + items[2].Id], writer.Calls);
        Assert.Equal([items[0].Id, items[2].Id], writer.WrittenIds);
    }

    [Fact]
    public async Task BuildAsync_Cancelled_Throws()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ItemIndexer(new FakeItemIndexWriter()).BuildAsync(
                FakeRepositoryProxy.Create(GetItems(2)), cancel: cts.Token));
    }

#pragma warning disable CS0618 // Type or member is obsolete
    [Fact]
    public void Build_Legacy_Ok()
    {
        FakeItemIndexWriter writer = new();
        RecordingProgress progress = new();
        new ItemIndexer(writer).Build(FakeRepositoryProxy.Create(GetItems(2)),
            new ItemFilter(), CancellationToken.None, progress);
        Assert.Equal(2, writer.WrittenIds.Count);
        Assert.Single(progress.Reports);
    }

    [Fact]
    public void Build_LegacyCancelled_Stops()
    {
        FakeItemIndexWriter writer = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();
        new ItemIndexer(writer).Build(FakeRepositoryProxy.Create(GetItems(2)),
            new ItemFilter(), cts.Token);
        Assert.Empty(writer.WrittenIds);
    }

    [Fact]
    public async Task AddItemAndClear_Legacy_Ok()
    {
        FakeItemIndexWriter writer = new();
        ItemIndexer indexer = new(writer);
        IItem item = GetItems(1)[0];

        indexer.AddItem(item);
        await indexer.Clear();

        Assert.Equal(["write:" + item.Id, "clear"], writer.Calls);
    }
#pragma warning restore CS0618 // Type or member is obsolete
}

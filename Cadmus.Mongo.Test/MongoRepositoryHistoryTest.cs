using System;
using System.Linq;
using System.Threading.Tasks;
using Cadmus.Core;
using Cadmus.Core.Storage;
using Cadmus.General.Parts;
using Fusi.Tools.Data;
using Xunit;

namespace Cadmus.Mongo.Test;

/// <summary>
/// Tests for the history members of <see cref="MongoCadmusRepository"/>.
/// </summary>
[Collection(nameof(NonParallelResourceCollection))]
public sealed class MongoRepositoryHistoryTest
{
    // history records are sorted by time: ensure distinct times
    private static Task TickAsync() => Task.Delay(20);

    private static Item CreateItem(string id, string facetId, string title,
        string? groupId = null, int flags = 0, string userId = "zeus") => new()
        {
            Id = id,
            Title = title,
            Description = "Some description",
            FacetId = facetId,
            GroupId = groupId,
            Flags = flags,
            CreatorId = userId,
            UserId = userId
        };

    /// <summary>
    /// Seeds 3 items: A (alpha, created and updated), B (beta, created),
    /// C (alpha, created and deleted). History items are thus (newest
    /// first): C deleted, C created, B created, A updated, A created.
    /// </summary>
    private static async Task<MongoCadmusRepository> SeedHistoryItemsAsync()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();

        Item a = CreateItem("a", "alpha", "Alpha Item", "g1", 1, "zeus");
        repository.AddItem(a);
        await TickAsync();
        a.Title = "Alpha Item Updated";
        repository.AddItem(a);
        await TickAsync();
        repository.AddItem(CreateItem("b", "beta", "Beta Item", "g2", 2, "hera"));
        await TickAsync();
        repository.AddItem(CreateItem("c", "alpha", "Gamma Item", "g1", 3, "zeus"));
        await TickAsync();
        repository.DeleteItem("c", "hera");
        return repository;
    }

    #region History items
    [Fact]
    public async Task GetHistoryItems_All_SortedByTimeDescending()
    {
        MongoCadmusRepository repository = await SeedHistoryItemsAsync();

        DataPage<HistoryItemInfo> page = repository.GetHistoryItems(
            new HistoryItemFilter { PageSize = 0 });

        Assert.Equal(5, page.Total);
        Assert.Equal(5, page.Items.Count);
        Assert.Equal(["c", "c", "b", "a", "a"],
            page.Items.Select(i => i.ReferenceId));
        Assert.Equal([EditStatus.Deleted, EditStatus.Created,
            EditStatus.Created, EditStatus.Updated, EditStatus.Created],
            page.Items.Select(i => i.Status));
        // the deletion is attributed to the deleting user
        Assert.Equal("hera", page.Items[0].UserId);
    }

    [Fact]
    public async Task GetHistoryItems_Paged_Ok()
    {
        MongoCadmusRepository repository = await SeedHistoryItemsAsync();

        DataPage<HistoryItemInfo> page = repository.GetHistoryItems(
            new HistoryItemFilter { PageNumber = 2, PageSize = 2 });

        Assert.Equal(5, page.Total);
        Assert.Equal(["b", "a"], page.Items.Select(i => i.ReferenceId));
    }

    [Fact]
    public async Task GetHistoryItems_ByFacet_MatchesFacetOnly()
    {
        MongoCadmusRepository repository = await SeedHistoryItemsAsync();

        DataPage<HistoryItemInfo> page = repository.GetHistoryItems(
            new HistoryItemFilter { FacetId = "beta" });

        HistoryItemInfo info = Assert.Single(page.Items);
        Assert.Equal("b", info.ReferenceId);
        Assert.Equal("beta", info.FacetId);
    }

    [Fact]
    public async Task GetHistoryItems_ByTitle_ContainsIgnoringCase()
    {
        MongoCadmusRepository repository = await SeedHistoryItemsAsync();

        DataPage<HistoryItemInfo> page = repository.GetHistoryItems(
            new HistoryItemFilter { Title = "ITEM UPDATED" });

        HistoryItemInfo info = Assert.Single(page.Items);
        Assert.Equal("a", info.ReferenceId);
        Assert.Equal(EditStatus.Updated, info.Status);
    }

    [Fact]
    public async Task GetHistoryItems_ByDescription_ContainsIgnoringCase()
    {
        MongoCadmusRepository repository = await SeedHistoryItemsAsync();

        Assert.Equal(5, repository.GetHistoryItems(
            new HistoryItemFilter { Description = "SOME" }).Total);
        Assert.Equal(0, repository.GetHistoryItems(
            new HistoryItemFilter { Description = "none" }).Total);
    }

    [Fact]
    public async Task GetHistoryItems_ByReferenceAndStatus_Ok()
    {
        MongoCadmusRepository repository = await SeedHistoryItemsAsync();

        DataPage<HistoryItemInfo> page = repository.GetHistoryItems(
            new HistoryItemFilter
            {
                ReferenceId = "c",
                Status = EditStatus.Deleted
            });

        HistoryItemInfo info = Assert.Single(page.Items);
        Assert.Equal("c", info.ReferenceId);
        Assert.Equal(EditStatus.Deleted, info.Status);
    }

    [Fact]
    public async Task GetHistoryItems_ByGroupAndUser_Ok()
    {
        MongoCadmusRepository repository = await SeedHistoryItemsAsync();

        DataPage<HistoryItemInfo> page = repository.GetHistoryItems(
            new HistoryItemFilter
            {
                GroupId = "g1",
                UserId = "zeus"
            });

        // A created, A updated, C created (C deleted is by hera)
        Assert.Equal(3, page.Total);
        Assert.All(page.Items, i => Assert.Equal("g1", i.GroupId));
    }

    [Theory]
    [InlineData(FlagMatching.BitsAllSet, 3, 2)]
    [InlineData(FlagMatching.BitsAnySet, 2, 3)]
    [InlineData(FlagMatching.BitsAllClear, 2, 2)]
    [InlineData(FlagMatching.BitsAnyClear, 3, 3)]
    public async Task GetHistoryItems_ByFlags_Ok(FlagMatching matching,
        int flags, int expected)
    {
        // flags: A=1 (x2), B=2, C=3 (x2)
        MongoCadmusRepository repository = await SeedHistoryItemsAsync();

        DataPage<HistoryItemInfo> page = repository.GetHistoryItems(
            new HistoryItemFilter
            {
                Flags = flags,
                FlagMatching = matching
            });

        Assert.Equal(expected, page.Total);
    }

    [Fact]
    public async Task GetHistoryItems_ByTime_Ok()
    {
        MongoCadmusRepository repository = await SeedHistoryItemsAsync();
        DataPage<HistoryItemInfo> all = repository.GetHistoryItems(
            new HistoryItemFilter());
        // newest first: take the time of B created
        DateTime time = all.Items[2].TimeModified;

        Assert.Equal(3, repository.GetHistoryItems(
            new HistoryItemFilter { MinModified = time }).Total);
        Assert.Equal(3, repository.GetHistoryItems(
            new HistoryItemFilter { MaxModified = time }).Total);
        Assert.Equal(1, repository.GetHistoryItems(
            new HistoryItemFilter
            {
                MinModified = time,
                MaxModified = time
            }).Total);
    }

    [Fact]
    public async Task GetHistoryItem_Existing_Ok()
    {
        MongoCadmusRepository repository = await SeedHistoryItemsAsync();
        HistoryItemInfo info = repository.GetHistoryItems(
            new HistoryItemFilter { ReferenceId = "b" }).Items[0];

        HistoryItem? item = repository.GetHistoryItem(info.Id);

        Assert.NotNull(item);
        Assert.Equal(info.Id, item.Id);
        Assert.Equal("b", item.ReferenceId);
        Assert.Equal("Beta Item", item.Title);
        Assert.Equal("beta", item.FacetId);
        Assert.Equal("g2", item.GroupId);
        Assert.Equal(2, item.Flags);
        Assert.Equal("hera", item.UserId);
        Assert.Equal(EditStatus.Created, item.Status);
    }

    [Fact]
    public void GetHistoryItem_NotExisting_Null()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();

        Assert.Null(repository.GetHistoryItem("not-existing"));
    }

    [Fact]
    public async Task DeleteHistoryItem_Existing_Deleted()
    {
        MongoCadmusRepository repository = await SeedHistoryItemsAsync();
        string id = repository.GetHistoryItems(
            new HistoryItemFilter { ReferenceId = "b" }).Items[0].Id;

        repository.DeleteHistoryItem(id);

        Assert.Null(repository.GetHistoryItem(id));
        Assert.Equal(4, repository.GetHistoryItems(new HistoryItemFilter())
            .Total);
    }

    [Fact]
    public void AddHistoryItem_NotExisting_AddedWithItsIds()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();
        HistoryItem item = new("h1", "item-001")
        {
            Title = "Title",
            Description = "Description",
            FacetId = "alpha",
            SortKey = "title",
            UserId = "zeus",
            Status = EditStatus.Updated
        };

        repository.AddHistoryItem(item);

        HistoryItem? added = repository.GetHistoryItem("h1");
        Assert.NotNull(added);
        Assert.Equal("h1", added.Id);
        Assert.Equal("item-001", added.ReferenceId);
        Assert.Equal("Title", added.Title);
        Assert.Equal(EditStatus.Updated, added.Status);
    }

    [Fact]
    public void AddHistoryItem_Existing_Updated()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();
        HistoryItem item = new("h1", "item-001")
        {
            Title = "Title",
            FacetId = "alpha"
        };
        repository.AddHistoryItem(item);

        item.Title = "Changed";
        repository.AddHistoryItem(item);

        DataPage<HistoryItemInfo> page = repository.GetHistoryItems(
            new HistoryItemFilter());
        HistoryItemInfo info = Assert.Single(page.Items);
        Assert.Equal("h1", info.Id);
        Assert.Equal("Changed", info.Title);
    }
    #endregion

    #region History parts
    /// <summary>
    /// Seeds a note part created, updated (by hera) and deleted (by hera),
    /// plus a categories part created in another item. History parts are thus
    /// (newest first): categories created, note deleted, note updated, note
    /// created.
    /// </summary>
    private static async Task<MongoCadmusRepository> SeedHistoryPartsAsync()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();

        NotePart note = new()
        {
            Id = "note",
            ItemId = "item-001",
            RoleId = "comment",
            Text = "First",
            CreatorId = "zeus",
            UserId = "zeus"
        };
        repository.AddPart(note);
        await TickAsync();
        note.Text = "Second";
        note.UserId = "hera";
        repository.AddPart(note);
        await TickAsync();
        repository.DeletePart("note", "hera");
        await TickAsync();
        repository.AddPart(new CategoriesPart
        {
            Id = "categories",
            ItemId = "item-002",
            CreatorId = "zeus",
            UserId = "zeus",
            Categories = { "x" }
        });
        return repository;
    }

    [Fact]
    public async Task GetHistoryParts_All_SortedByTimeDescending()
    {
        MongoCadmusRepository repository = await SeedHistoryPartsAsync();

        DataPage<HistoryPartInfo> page = repository.GetHistoryParts(
            new HistoryPartFilter { PageSize = 0 });

        Assert.Equal(4, page.Total);
        Assert.Equal(["categories", "note", "note", "note"],
            page.Items.Select(p => p.ReferenceId));
        Assert.Equal([EditStatus.Created, EditStatus.Deleted,
            EditStatus.Updated, EditStatus.Created],
            page.Items.Select(p => p.Status));
    }

    [Fact]
    public async Task GetHistoryParts_Filtered_Ok()
    {
        MongoCadmusRepository repository = await SeedHistoryPartsAsync();

        Assert.Equal(3, repository.GetHistoryParts(new HistoryPartFilter
        {
            ItemIds = ["item-001"]
        }).Total);
        Assert.Equal(4, repository.GetHistoryParts(new HistoryPartFilter
        {
            ItemIds = ["item-001", "item-002"]
        }).Total);
        Assert.Equal(1, repository.GetHistoryParts(new HistoryPartFilter
        {
            TypeId = "it.vedph.categories"
        }).Total);
        Assert.Equal(3, repository.GetHistoryParts(new HistoryPartFilter
        {
            RoleId = "comment"
        }).Total);
        Assert.Equal(3, repository.GetHistoryParts(new HistoryPartFilter
        {
            ReferenceId = "note"
        }).Total);
        Assert.Equal(1, repository.GetHistoryParts(new HistoryPartFilter
        {
            ReferenceId = "note",
            Status = EditStatus.Updated
        }).Total);
        Assert.Equal(2, repository.GetHistoryParts(new HistoryPartFilter
        {
            UserId = "hera"
        }).Total);
    }

    [Fact]
    public async Task GetHistoryParts_ByTime_Ok()
    {
        MongoCadmusRepository repository = await SeedHistoryPartsAsync();
        DataPage<HistoryPartInfo> all = repository.GetHistoryParts(
            new HistoryPartFilter());
        // newest first: take the time of note updated
        DateTime time = all.Items[2].TimeModified;

        Assert.Equal(3, repository.GetHistoryParts(
            new HistoryPartFilter { MinModified = time }).Total);
        Assert.Equal(2, repository.GetHistoryParts(
            new HistoryPartFilter { MaxModified = time }).Total);
    }

    [Fact]
    public async Task GetHistoryParts_SortExpressions_Ok()
    {
        MongoCadmusRepository repository = await SeedHistoryPartsAsync();

        DataPage<HistoryPartInfo> page = repository.GetHistoryParts(
            new HistoryPartFilter
            {
                ReferenceId = "note",
                SortExpressions = [Tuple.Create("status", false)]
            });

        Assert.Equal([EditStatus.Deleted, EditStatus.Updated,
            EditStatus.Created], page.Items.Select(p => p.Status));
    }

    [Fact]
    public async Task GetHistoryPart_AsIPart_Ok()
    {
        MongoCadmusRepository repository = await SeedHistoryPartsAsync();
        HistoryPartInfo info = repository.GetHistoryParts(new HistoryPartFilter
        {
            ReferenceId = "note",
            Status = EditStatus.Updated
        }).Items[0];

        HistoryPart<IPart>? part = repository.GetHistoryPart<IPart>(info.Id);

        Assert.NotNull(part);
        Assert.Equal(info.Id, part.Id);
        Assert.Equal("note", part.ReferenceId);
        Assert.Equal(info.TimeModified, part.TimeModified);
        Assert.Equal("hera", part.UserId);
        Assert.Equal(EditStatus.Updated, part.Status);
        NotePart note = Assert.IsType<NotePart>(part.Part);
        Assert.Equal("Second", note.Text);
        Assert.Equal("note", note.Id);
    }

    [Fact]
    public async Task GetHistoryPart_Typed_Ok()
    {
        MongoCadmusRepository repository = await SeedHistoryPartsAsync();
        HistoryPartInfo info = repository.GetHistoryParts(new HistoryPartFilter
        {
            ReferenceId = "note",
            Status = EditStatus.Created
        }).Items[0];

        HistoryPart<NotePart>? part = repository.GetHistoryPart<NotePart>(
            info.Id);

        Assert.NotNull(part);
        Assert.Equal("First", part.Part.Text);
        Assert.Equal(EditStatus.Created, part.Status);
    }

    [Fact]
    public async Task GetHistoryPart_WrongType_Throws()
    {
        MongoCadmusRepository repository = await SeedHistoryPartsAsync();
        HistoryPartInfo info = repository.GetHistoryParts(new HistoryPartFilter
        {
            ReferenceId = "note"
        }).Items[0];

        Assert.Throws<InvalidCastException>(
            () => repository.GetHistoryPart<CategoriesPart>(info.Id));
    }

    [Fact]
    public void GetHistoryPart_NotExisting_Null()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();

        Assert.Null(repository.GetHistoryPart<IPart>("not-existing"));
    }

    [Fact]
    public async Task DeleteHistoryPart_Existing_Deleted()
    {
        MongoCadmusRepository repository = await SeedHistoryPartsAsync();
        string id = repository.GetHistoryParts(new HistoryPartFilter
        {
            ReferenceId = "categories"
        }).Items[0].Id;

        repository.DeleteHistoryPart(id);

        Assert.Null(repository.GetHistoryPart<IPart>(id));
        Assert.Equal(3, repository.GetHistoryParts(new HistoryPartFilter())
            .Total);
    }

    [Fact]
    public async Task DeleteItem_WithParts_PartsStoredAsDeletedByUser()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();

        // item-001 has 3 parts
        repository.DeleteItem("item-001", "hera");

        DataPage<HistoryPartInfo> page = repository.GetHistoryParts(
            new HistoryPartFilter { ItemIds = ["item-001"] });
        Assert.Equal(3, page.Total);
        Assert.All(page.Items, p =>
        {
            Assert.Equal(EditStatus.Deleted, p.Status);
            Assert.Equal("hera", p.UserId);
        });
        Assert.Equal(["part-001", "part-002", "part-003"],
            page.Items.Select(p => p.ReferenceId).Order());
        // the deleted contents are kept
        HistoryPart<IPart>? part = repository.GetHistoryPart<IPart>(
            page.Items.First(p => p.ReferenceId == "part-002").Id);
        Assert.Equal("Some notes.",
            Assert.IsType<NotePart>(part!.Part).Text);

        Assert.Empty(repository.GetParts(
            new PartFilter { ItemIds = ["item-001"] }).Items);
        Assert.Null(repository.GetItem("item-001"));
    }
    #endregion
}

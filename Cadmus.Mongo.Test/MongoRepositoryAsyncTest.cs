using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Layers;
using Cadmus.Core.Storage;
using Cadmus.General.Parts;
using Cadmus.TestBase;
using Fusi.Tools.Data;
using Xunit;

namespace Cadmus.Mongo.Test;

/// <summary>
/// Tests for the asynchronous members of <see cref="MongoCadmusRepository"/>.
/// They are called via <see cref="ICadmusRepository"/>, to ensure that the
/// repository implements them rather than inheriting their default
/// implementations, which wrap the synchronous members. The synchronous
/// members share the same code, and are tested in more detail elsewhere.
/// </summary>
[Collection(nameof(NonParallelResourceCollection))]
public sealed class MongoRepositoryAsyncTest
{
    private static ICadmusRepository Prepare()
    {
        TestDb.Prepare();
        return TestDb.CreateRepository();
    }

    [Fact]
    public void AsyncMembers_AllImplementedByRepository()
    {
        // every async member of the interface is mapped to a method of
        // the repository, rather than to the interface default
        var map = typeof(MongoCadmusRepository)
            .GetInterfaceMap(typeof(ICadmusRepository));
        for (int i = 0; i < map.InterfaceMethods.Length; i++)
        {
            if (!map.InterfaceMethods[i].Name.EndsWith("Async")) continue;
            Assert.Equal(typeof(MongoCadmusRepository),
                map.TargetMethods[i].DeclaringType);
        }
    }

    [Fact]
    public async Task Flags_Ok()
    {
        ICadmusRepository repository = Prepare();

        Assert.Equal(2, (await repository.GetFlagDefinitionsAsync()).Count);
        await repository.AddFlagDefinitionAsync(new FlagDefinition
        {
            Id = 4,
            Label = "Gamma"
        });
        Assert.Equal("Gamma",
            (await repository.GetFlagDefinitionAsync(4))?.Label);
        await repository.DeleteFlagDefinitionAsync(4);
        Assert.Null(await repository.GetFlagDefinitionAsync(4));
    }

    [Fact]
    public async Task Facets_Ok()
    {
        ICadmusRepository repository = Prepare();

        Assert.Equal(["Alpha", "Beta"],
            (await repository.GetFacetDefinitionsAsync()).Select(f => f.Label));
        await repository.AddFacetDefinitionAsync(new FacetDefinition
        {
            Id = "gamma",
            Label = "Gamma"
        });
        Assert.Equal("Gamma",
            (await repository.GetFacetDefinitionAsync("gamma"))?.Label);
        await repository.DeleteFacetDefinitionAsync("gamma");
        Assert.Null(await repository.GetFacetDefinitionAsync("gamma"));
    }

    [Fact]
    public async Task Thesauri_Ok()
    {
        ICadmusRepository repository = Prepare();
        Thesaurus colors = new("colors@en");
        colors.AddEntry(new ThesaurusEntry("r", "red"));
        await repository.AddThesaurusAsync(colors);
        await repository.AddThesaurusAsync(
            new Thesaurus("tints@en") { TargetId = "colors@en" });

        Assert.Equal(["colors@en", "tints@en"],
            await repository.GetThesaurusIdsAsync());
        DataPage<Thesaurus> page = await repository.GetThesauriAsync(
            new ThesaurusFilter { IsAlias = true });
        Assert.Equal("tints@en", Assert.Single(page.Items).Id);
        Assert.Equal(["tints@en"],
            await repository.GetThesaurusAliasesAsync("colors@en"));
        Assert.Equal("red",
            (await repository.GetThesaurusAsync("tints@it"))?.Entries[0].Value);

        await repository.DeleteThesaurusAsync("tints@en");
        Assert.Empty(await repository.GetThesaurusAliasesAsync("colors@en"));
    }

    [Fact]
    public async Task Items_Ok()
    {
        ICadmusRepository repository = Prepare();

        DataPage<ItemInfo> page = await repository.GetItemsAsync(
            new ItemFilter { FacetId = "alpha", PageSize = 3 });
        Assert.Equal(10, page.Total);
        Assert.Equal(["item-001", "item-003", "item-005"],
            page.Items.Select(i => i.Id));

        IItem? item = await repository.GetItemAsync("item-001");
        Assert.NotNull(item);
        Assert.Equal(3, item.Parts.Count);
        Assert.Empty((await repository.GetItemAsync("item-001", false))!.Parts);

        item.Title = "Changed";
        await repository.AddItemAsync(item);
        Assert.Equal("Changed",
            (await repository.GetItemAsync("item-001", false))!.Title);

        await repository.SetItemFlagsAsync(["item-001", "item-002"], 64);
        await repository.SetItemGroupIdAsync(["item-001", "item-002"], "new");
        page = await repository.GetItemsAsync(new ItemFilter
        {
            Flags = 64,
            GroupId = "new"
        });
        Assert.Equal(2, page.Total);

        await repository.DeleteItemAsync("item-001", "zeus");
        Assert.Null(await repository.GetItemAsync("item-001"));
        Assert.Empty(await repository.GetItemPartsAsync(["item-001"]));
    }

    [Fact]
    public async Task HistoryItems_Ok()
    {
        ICadmusRepository repository = Prepare();
        IItem item = (await repository.GetItemAsync("item-002", false))!;
        await repository.AddItemAsync(item);

        DataPage<HistoryItemInfo> page = await repository.GetHistoryItemsAsync(
            new HistoryItemFilter { ReferenceId = "item-002" });
        HistoryItemInfo info = Assert.Single(page.Items);
        Assert.Equal(EditStatus.Updated, info.Status);
        Assert.Equal("item-002",
            (await repository.GetHistoryItemAsync(info.Id))?.ReferenceId);

        await repository.DeleteHistoryItemAsync(info.Id);
        Assert.Null(await repository.GetHistoryItemAsync(info.Id));
    }

    [Fact]
    public async Task ExportAndImportItem_Ok()
    {
        ICadmusRepository repository = Prepare();
        StringWriter writer = new();

        Assert.False(await repository.ExportItemAsync("not-existing", writer));
        Assert.True(await repository.ExportItemAsync("item-001", writer));

        // importing an existing item is not allowed
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.ImportItemAsync(new StringReader(writer.ToString())));

        // once deleted, it is imported with new IDs
        await repository.DeleteItemAsync("item-001", "zeus");
        IItem imported = await repository.ImportItemAsync(
            new StringReader(writer.ToString()), "hera");

        Assert.NotEqual("item-001", imported.Id);
        Assert.Equal(3, imported.Parts.Count);
        Assert.DoesNotContain(imported.Parts,
            p => p.Id is "part-001" or "part-002" or "part-003");
        IItem? saved = await repository.GetItemAsync(imported.Id);
        Assert.NotNull(saved);
        Assert.Equal("hera", saved.CreatorId);
        Assert.Equal(3, saved.Parts.Count);
        Assert.All(saved.Parts, p => Assert.Equal("hera", p.UserId));
    }

    [Fact]
    public async Task GetDistinctGroupIdsAndLayersCount_Ok()
    {
        ICadmusRepository repository = Prepare();

        DataPage<string> page = await repository.GetDistinctGroupIdsAsync(
            new PagingOptions { PageNumber = 2, PageSize = 5 }, "G");
        Assert.Equal(11, page.Total);
        Assert.Equal(["g05", "g06", "g07", "g08", "g09"], page.Items);

        Assert.Equal(1, await repository.GetGroupLayersCountAsync("g00"));
    }

    [Fact]
    public async Task Parts_Ok()
    {
        ICadmusRepository repository = Prepare();

        DataPage<PartInfo> page = await repository.GetPartsAsync(
            new PartFilter { ItemIds = ["item-001"] });
        Assert.Equal(3, page.Total);

        IList<IPart> parts = await repository.GetItemPartsAsync(["item-001"],
            "it.vedph.note");
        Assert.IsType<NotePart>(Assert.Single(parts));

        IList<LayerPartInfo> layers = await repository.GetItemLayerInfoAsync(
            "item-001", false);
        Assert.Equal(2, Assert.Single(layers).FragmentCount);

        NotePart? note = await repository.GetPartAsync<NotePart>("part-002");
        Assert.NotNull(note);
        Assert.Equal("item-001", await repository.GetPartItemIdAsync("part-002"));
        Assert.Equal("Odd", await repository.GetPartCreatorIdAsync("part-002"));
        Assert.Null(await repository.GetPartCreatorIdAsync("not-existing"));
        Assert.Contains("Some notes.",
            await repository.GetPartContentAsync("part-002"));

        note.Text = "Changed.";
        await repository.AddPartAsync(note);
        Assert.Equal("Changed.",
            (await repository.GetPartAsync<NotePart>("part-002"))!.Text);

        note.Text = "From content.";
        await repository.AddPartFromContentAsync(TestHelper.SerializePart(note));
        Assert.Equal("From content.",
            (await repository.GetPartAsync<NotePart>("part-002"))!.Text);

        await repository.SetPartThesaurusScopeAsync(["part-002"], "scope");
        Assert.Equal("scope",
            (await repository.GetPartAsync<IPart>("part-002"))!.ThesaurusScope);

        await repository.DeletePartAsync("part-002", "zeus");
        Assert.Null(await repository.GetPartAsync<IPart>("part-002"));
    }

    [Fact]
    public async Task HistoryParts_Ok()
    {
        ICadmusRepository repository = Prepare();
        await repository.DeletePartAsync("part-002", "zeus");

        DataPage<HistoryPartInfo> page = await repository.GetHistoryPartsAsync(
            new HistoryPartFilter { ReferenceId = "part-002" });
        HistoryPartInfo info = Assert.Single(page.Items);
        Assert.Equal(EditStatus.Deleted, info.Status);

        HistoryPart<IPart>? part =
            await repository.GetHistoryPartAsync<IPart>(info.Id);
        Assert.IsType<NotePart>(part?.Part);

        await repository.DeleteHistoryPartAsync(info.Id);
        Assert.Null(await repository.GetHistoryPartAsync<IPart>(info.Id));
    }

    [Fact]
    public async Task Layers_Ok()
    {
        ICadmusRepository repository = Prepare();

        // part-003 is a layer in an item without base text
        Assert.Equal(2,
            await repository.GetLayerPartBreakChanceAsync("part-003", 0));
        Assert.Equal(0,
            await repository.GetLayerPartBreakChanceAsync("not-existing", 0));
        Assert.Empty(await repository.GetLayerPartHintsAsync("part-003"));

        string? json = await repository.ApplyLayerPartPatchesAsync("part-003",
            "patcher", ["del 1.2"]);
        Assert.NotNull(json);
        var layer = await repository.GetPartAsync<
            TokenTextLayerPart<CommentLayerFragment>>("part-003");
        Assert.Equal("1.3", Assert.Single(layer!.Fragments).Location);
        Assert.Equal("patcher", layer.UserId);
    }

    [Fact]
    public async Task Settings_Ok()
    {
        ICadmusRepository repository = Prepare();

        await repository.AddSettingAsync("s", "{\"a\":1}");
        Assert.Equal("{ \"a\" : 1 }", await repository.GetSettingAsync("s"));
        await repository.DeleteSettingAsync("s");
        Assert.Null(await repository.GetSettingAsync("s"));
    }

    [Fact]
    public async Task AnyMember_Canceled_Throws()
    {
        ICadmusRepository repository = Prepare();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetItemsAsync(new ItemFilter(), cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.AddPartAsync(new NotePart
            {
                ItemId = "item-001",
                CreatorId = "zeus",
                UserId = "zeus"
            }, true, cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.ExportItemAsync("item-001", new StringWriter(),
                true, cts.Token));

        // nothing was saved
        Assert.Equal(3, (await repository.GetPartsAsync(new PartFilter()))
            .Total);
    }

    [Fact]
    public async Task InvalidArguments_Throw()
    {
        ICadmusRepository repository = Prepare();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => repository.GetItemAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => repository.AddPartAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.GetItemPartsAsync([]));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => repository.DeleteItemAsync("item-001", null!));
    }
}

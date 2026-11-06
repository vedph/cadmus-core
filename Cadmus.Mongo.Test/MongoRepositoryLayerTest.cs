using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Layers;
using Cadmus.Core.Storage;
using Cadmus.General.Parts;
using Xunit;

namespace Cadmus.Mongo.Test;

/// <summary>
/// Tests for the text layers members of <see cref="MongoCadmusRepository"/>.
/// </summary>
[Collection(nameof(NonParallelResourceCollection))]
public sealed class MongoRepositoryLayerTest
{
    // times are compared with milliseconds precision: ensure that saves
    // happen at clearly distinct times
    private static void Tick() => Thread.Sleep(50);

    private static FacetDefinition GetLayeredFacet()
    {
        FacetDefinition facet = new()
        {
            Id = "layered",
            Label = "layered",
        };
        facet.PartDefinitions.Add(new PartDefinition
        {
            Name = "text",
            TypeId = "it.vedph.token-text",
            RoleId = PartBase.BASE_TEXT_ROLE_ID,
        });
        facet.PartDefinitions.Add(new PartDefinition
        {
            Name = "comments",
            TypeId = "it.vedph.token-text-layer",
            RoleId = "fr.it.vedph.comment",
        });
        return facet;
    }

    private sealed class LayeredItem
    {
        public required IItem Item { get; init; }
        public required TokenTextPart Text { get; init; }
        public TokenTextLayerPart<CommentLayerFragment>? Layer { get; set; }
    }

    private static LayeredItem AddLayeredItem(MongoCadmusRepository repository,
        string text)
    {
        repository.AddFacetDefinition(GetLayeredFacet());
        Item item = new()
        {
            Title = "Test",
            Description = "Test",
            FacetId = "layered",
            CreatorId = "zeus",
            UserId = "zeus"
        };
        repository.AddItem(item);

        TokenTextPart textPart = new()
        {
            ItemId = item.Id,
            CreatorId = item.CreatorId,
            UserId = item.UserId,
            Citation = "1"
        };
        textPart.Lines.Add(new TextLine { Y = 1, Text = text });
        repository.AddPart(textPart);
        return new LayeredItem { Item = item, Text = textPart };
    }

    private static TokenTextLayerPart<CommentLayerFragment> AddLayer(
        MongoCadmusRepository repository, LayeredItem item,
        params string[] locations)
    {
        TokenTextLayerPart<CommentLayerFragment> layer = new()
        {
            ItemId = item.Item.Id,
            CreatorId = "zeus",
            UserId = "zeus"
        };
        foreach (string location in locations)
        {
            layer.AddFragment(new CommentLayerFragment
            {
                Location = location,
                Text = "Comment on " + location
            });
        }
        repository.AddPart(layer);
        item.Layer = layer;
        return layer;
    }

    private static void SaveText(MongoCadmusRepository repository,
        TokenTextPart part, string? text = null, string? citation = null)
    {
        if (text != null) part.Lines[0].Text = text;
        if (citation != null) part.Citation = citation;
        repository.AddPart(part);
    }

    /// <summary>
    /// Adds noise to the history of parts, so that the history versions
    /// of the base text are not the first ones in the collection.
    /// </summary>
    private static void AddHistoryNoise(MongoCadmusRepository repository)
    {
        for (int i = 0; i < 5; i++)
        {
            repository.AddPart(new NotePart
            {
                ItemId = "item-002",
                Text = "Noise " + i,
                CreatorId = "zeus",
                UserId = "zeus"
            });
        }
    }

    [Fact]
    public void GetLayerPartBreakChance_TextChangedBeforeLayerThenSaved_0()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();
        AddHistoryNoise(repository);
        LayeredItem item = AddLayeredItem(repository, "alpha beta");
        Tick();
        SaveText(repository, item.Text, text: "alpha gamma");
        Tick();
        AddLayer(repository, item, "1.1");
        Tick();
        // saved after the layer, without changing the text
        SaveText(repository, item.Text, citation: "2");
        Tick();
        SaveText(repository, item.Text, citation: "3");

        // the last change of text happened before the layer was saved
        Assert.Equal(0, repository.GetLayerPartBreakChance(item.Layer!.Id, 0));
        // ...but within the tolerance interval
        Assert.Equal(1, repository.GetLayerPartBreakChance(item.Layer!.Id, 10));
    }

    [Fact]
    public void GetLayerPartBreakChance_TextChangedAfterLayerThenSaved_1()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();
        AddHistoryNoise(repository);
        LayeredItem item = AddLayeredItem(repository, "alpha beta");
        Tick();
        AddLayer(repository, item, "1.1");
        Tick();
        SaveText(repository, item.Text, text: "alpha gamma");
        Tick();
        // saved again, without changing the text
        SaveText(repository, item.Text, citation: "2");

        Assert.Equal(1, repository.GetLayerPartBreakChance(item.Layer!.Id, 0));
    }

    [Fact]
    public void GetLayerPartBreakChance_OrphanLayer_2()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();
        TokenTextLayerPart<CommentLayerFragment> layer = new()
        {
            ItemId = "not-existing",
            CreatorId = "zeus",
            UserId = "zeus"
        };
        repository.AddPart(layer);

        Assert.Equal(2, repository.GetLayerPartBreakChance(layer.Id, 0));
    }

    [Fact]
    public void GetLayerPartBreakChance_BaseTextWithoutText_1()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();
        LayeredItem item = AddLayeredItem(repository, "alpha");
        TokenTextLayerPart<CommentLayerFragment> layer =
            AddLayer(repository, item, "1.1");
        Tick();
        // replace the base text with a part having no text
        repository.DeletePart(item.Text.Id, "zeus");
        repository.AddPart(new CategoriesPart
        {
            ItemId = item.Item.Id,
            RoleId = PartBase.BASE_TEXT_ROLE_ID,
            CreatorId = "zeus",
            UserId = "zeus"
        });

        // the text cannot be compared: the base text was saved after the
        // layer, so this is potentially broken
        Assert.Equal(1, repository.GetLayerPartBreakChance(layer.Id, 0));
        Assert.Empty(repository.GetLayerPartHints(layer.Id));
    }

    [Fact]
    public void GetLayerPartHints_SeveralVersionsBeforeLayer_UsesLatest()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();
        // the text gets changed twice before the layer is saved
        LayeredItem item = AddLayeredItem(repository, "alpha beta gamma delta");
        Tick();
        SaveText(repository, item.Text, text: "Hello world!");
        Tick();
        AddLayer(repository, item, "1.1", "1.2");
        Tick();
        // delete the first token after the layer was saved
        SaveText(repository, item.Text, text: "world!");

        IList<LayerHint> hints = repository.GetLayerPartHints(item.Layer!.Id);

        // hints are relative to "Hello world!", not to the original text
        Assert.Equal(2, hints.Count);
        Assert.Equal("1.1", hints[0].Location);
        Assert.Equal(2, hints[0].ImpactLevel);
        Assert.Equal("del 1.1", hints[0].PatchOperation);
        Assert.Equal("1.2", hints[1].Location);
        Assert.Equal("mov 1.2 1.1", hints[1].PatchOperation);
    }

    [Fact]
    public void GetLayerPartHints_NotExisting_Empty()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();

        Assert.Empty(repository.GetLayerPartHints("not-existing"));
    }

    [Fact]
    public void GetLayerPartHints_NoTextPart_Empty()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();
        LayeredItem item = AddLayeredItem(repository, "alpha beta");
        AddLayer(repository, item, "1.1");
        repository.DeletePart(item.Text.Id, "zeus");

        Assert.Empty(repository.GetLayerPartHints(item.Layer!.Id));
    }

    [Fact]
    public void ApplyLayerPartPatches_Existing_SavedByUserWithHistory()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();
        LayeredItem item = AddLayeredItem(repository, "alpha beta");
        TokenTextLayerPart<CommentLayerFragment> layer =
            AddLayer(repository, item, "1.1", "1.2");
        DateTime saved = repository.GetParts(new PartFilter
        {
            ItemIds = [item.Item.Id],
            TypeId = layer.TypeId
        }).Items[0].TimeModified;
        Tick();

        string? json = repository.ApplyLayerPartPatches(layer.Id, "patcher",
            ["del 1.1", "mov 1.2 1.1"]);

        // the returned content is patched, and has the new user and time
        Assert.NotNull(json);
        TokenTextLayerPart<CommentLayerFragment>? returned =
            JsonSerializer.Deserialize<TokenTextLayerPart<CommentLayerFragment>>(
            json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        Assert.NotNull(returned);
        Assert.Equal("patcher", returned.UserId);
        Assert.True(returned.TimeModified > saved);
        CommentLayerFragment fr = Assert.Single(returned.Fragments);
        Assert.Equal("1.1", fr.Location);
        Assert.Equal("Comment on 1.2", fr.Text);

        // the stored part agrees with the returned content
        var stored = repository.GetPart<
            TokenTextLayerPart<CommentLayerFragment>>(layer.Id)!;
        Assert.Equal("patcher", stored.UserId);
        Assert.Equal("zeus", stored.CreatorId);
        Assert.Equal(returned.TimeModified, stored.TimeModified);
        Assert.Equal("1.1", Assert.Single(stored.Fragments).Location);

        PartInfo info = repository.GetParts(new PartFilter
        {
            ItemIds = [item.Item.Id],
            TypeId = layer.TypeId
        }).Items[0];
        Assert.Equal("patcher", info.UserId);
        Assert.Equal(returned.TimeModified, info.TimeModified,
            TimeSpan.FromMilliseconds(1));

        // the patch is in history
        HistoryPartInfo history = repository.GetHistoryParts(
            new HistoryPartFilter { ReferenceId = layer.Id }).Items[0];
        Assert.Equal(EditStatus.Updated, history.Status);
        Assert.Equal("patcher", history.UserId);
        HistoryPart<IPart>? historyPart =
            repository.GetHistoryPart<IPart>(history.Id);
        Assert.Single(Assert.IsType<
            TokenTextLayerPart<CommentLayerFragment>>(historyPart!.Part)
            .Fragments);
    }

    [Fact]
    public void ApplyLayerPartPatches_NotExisting_Null()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();

        Assert.Null(repository.ApplyLayerPartPatches("not-existing", "zeus",
            ["del 1.1"]));
        Assert.Equal(0, repository.GetHistoryParts(
            new HistoryPartFilter()).Total);
    }

    [Fact]
    public void GetItemLayerInfo_Absent_AddsMissingLayers()
    {
        TestDb.Prepare();
        MongoCadmusRepository repository = TestDb.CreateRepository();
        LayeredItem item = AddLayeredItem(repository, "alpha beta");

        IList<LayerPartInfo> infos = repository.GetItemLayerInfo(
            item.Item.Id, true);

        LayerPartInfo info = Assert.Single(infos);
        Assert.True(info.IsAbsent);
        Assert.Null(info.Id);
        Assert.Equal("fr.it.vedph.comment", info.RoleId);

        // once added, the layer is no more absent
        AddLayer(repository, item, "1.1");
        infos = repository.GetItemLayerInfo(item.Item.Id, true);
        info = Assert.Single(infos);
        Assert.False(info.IsAbsent);
        Assert.Equal(item.Layer!.Id, info.Id);
        Assert.Equal(1, info.FragmentCount);
    }
}

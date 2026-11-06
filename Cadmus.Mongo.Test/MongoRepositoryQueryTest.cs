using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Layers;
using Cadmus.Core.Storage;
using Cadmus.General.Parts;
using Fusi.Tools.Data;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Cadmus.Mongo.Test;

/// <summary>
/// Tests for the reading and writing members of
/// <see cref="MongoCadmusRepository"/> not covered by the storage-agnostic
/// tests in <see cref="TestBase.CadmusRepositoryTestBase"/>.
/// The test database has 20 items (<c>item-001</c>-<c>item-020</c>, titled
/// <c>Item N</c>, with flags N, group <c>gNN</c> = N/2, user <c>Odd</c> or
/// <c>Even</c>, modified on 2015-12-N), and 3 parts in item 1.
/// </summary>
[Collection(nameof(NonParallelResourceCollection))]
public sealed class MongoRepositoryQueryTest
{
    private static MongoCadmusRepository Prepare()
    {
        TestDb.Prepare();
        return TestDb.CreateRepository();
    }

    [Fact]
    public void AnyMember_NotConfigured_Throws()
    {
        MongoCadmusRepository repository = new(
            new StandardPartTypeProvider(new TagAttributeToTypeMap()),
            new StandardItemSortKeyBuilder());

        Assert.Throws<InvalidOperationException>(
            () => repository.GetFlagDefinitions());
    }

    #region Thesauri
    private static Thesaurus CreateThesaurus(string id, string? targetId = null)
    {
        Thesaurus thesaurus = new(id) { TargetId = targetId };
        if (targetId == null)
            thesaurus.AddEntry(new ThesaurusEntry("x", id));
        return thesaurus;
    }

    [Theory]
    // exact match always wins
    [InlineData("colors@it", "colors@it colors@eng colors@en", "colors@it")]
    // fallback order: eng, en (valid thesaurus IDs always have a language)
    [InlineData("colors@it", "colors@eng colors@en", "colors@eng")]
    [InlineData("colors@it", "colors@en", "colors@en")]
    // fallback also for English and for no language
    [InlineData("colors@en", "colors@eng", "colors@eng")]
    [InlineData("colors", "colors@en", "colors@en")]
    // no match
    [InlineData("colors@it", "colors@fr shapes@en", null)]
    public void GetThesaurus_LanguageFallback_Ok(string id, string existing,
        string? expected)
    {
        MongoCadmusRepository repository = Prepare();
        foreach (string existingId in existing.Split(' '))
            repository.AddThesaurus(CreateThesaurus(existingId));

        Thesaurus? thesaurus = repository.GetThesaurus(id);

        Assert.Equal(expected, thesaurus?.Id);
        if (expected != null)
            Assert.Equal(expected, thesaurus!.Entries[0].Value);
    }

    [Fact]
    public void GetThesaurus_AliasChain_Target()
    {
        MongoCadmusRepository repository = Prepare();
        repository.AddThesaurus(CreateThesaurus("a@en", "b@en"));
        repository.AddThesaurus(CreateThesaurus("b@en", "t@en"));
        repository.AddThesaurus(CreateThesaurus("t@en"));

        Thesaurus? thesaurus = repository.GetThesaurus("a@en");

        Assert.NotNull(thesaurus);
        Assert.Equal("t@en", thesaurus.Id);
        Assert.Null(thesaurus.TargetId);
        Assert.Single(thesaurus.Entries);
    }

    [Fact]
    public void GetThesaurus_AliasWithLanguageFallback_Target()
    {
        MongoCadmusRepository repository = Prepare();
        // the alias is found by fallback, and its target too
        repository.AddThesaurus(CreateThesaurus("a@en", "t@it"));
        repository.AddThesaurus(CreateThesaurus("t@en"));

        Assert.Equal("t@en", repository.GetThesaurus("a@it")?.Id);
    }

    [Fact]
    public void GetThesaurus_AliasToMissing_Null()
    {
        MongoCadmusRepository repository = Prepare();
        repository.AddThesaurus(CreateThesaurus("a@en", "missing@en"));

        Assert.Null(repository.GetThesaurus("a@en"));
    }

    [Fact]
    public void GetThesaurus_AliasCycle_Null()
    {
        MongoCadmusRepository repository = Prepare();
        repository.AddThesaurus(CreateThesaurus("a@en", "b@en"));
        repository.AddThesaurus(CreateThesaurus("b@en", "a@en"));

        Assert.Null(repository.GetThesaurus("a@en"));
    }

    [Fact]
    public void GetThesaurusIds_FilterWithRegexChars_Literal()
    {
        MongoCadmusRepository repository = Prepare();
        repository.AddThesaurus(CreateThesaurus("a.b@en"));
        repository.AddThesaurus(CreateThesaurus("axb@en"));
        repository.AddThesaurus(CreateThesaurus("a(b@en"));

        Assert.Equal(["a.b@en"], repository.GetThesaurusIds(
            new ThesaurusFilter { Id = "a.b", PageSize = 0 }));
        Assert.Equal(["a(b@en"], repository.GetThesaurusIds(
            new ThesaurusFilter { Id = "a(", PageSize = 0 }));
    }

    [Fact]
    public void GetThesaurusIds_Paged_Ok()
    {
        MongoCadmusRepository repository = Prepare();
        foreach (string id in new[] { "e@en", "d@en", "c@en", "b@en", "a@en" })
            repository.AddThesaurus(CreateThesaurus(id));

        Assert.Equal(["a@en", "b@en", "c@en", "d@en", "e@en"],
            repository.GetThesaurusIds());
        Assert.Equal(["c@en", "d@en"], repository.GetThesaurusIds(
            new ThesaurusFilter { PageNumber = 2, PageSize = 2 }));
    }

    [Fact]
    public void GetThesaurusIds_ByLanguage_ExactLanguage()
    {
        MongoCadmusRepository repository = Prepare();
        repository.AddThesaurus(CreateThesaurus("a@en"));
        repository.AddThesaurus(CreateThesaurus("b@eng"));
        repository.AddThesaurus(CreateThesaurus("c@it"));

        Assert.Equal(["a@en"], repository.GetThesaurusIds(
            new ThesaurusFilter { Language = "en", PageSize = 0 }));
    }

    [Fact]
    public void GetThesauri_NoMatch_Empty()
    {
        MongoCadmusRepository repository = Prepare();

        DataPage<Thesaurus> page = repository.GetThesauri(
            new ThesaurusFilter { Id = "none" });

        Assert.Equal(0, page.Total);
        Assert.Empty(page.Items);
    }

    [Fact]
    public void GetThesauri_PageSize0_All()
    {
        MongoCadmusRepository repository = Prepare();
        for (int i = 0; i < 25; i++)
            repository.AddThesaurus(CreateThesaurus($"t{i:00}@en"));

        DataPage<Thesaurus> page = repository.GetThesauri(
            new ThesaurusFilter { PageSize = 0 });

        Assert.Equal(25, page.Total);
        Assert.Equal(25, page.Items.Count);
        Assert.Equal("t00@en", page.Items[0].Id);
    }
    #endregion

    #region Items
    [Theory]
    [InlineData("ITEM 1", 11)]
    [InlineData("item 20", 1)]
    [InlineData("(", 0)]
    [InlineData("Item.", 0)]
    public void GetItems_ByTitle_ContainsIgnoringCase(string title, int count)
    {
        MongoCadmusRepository repository = Prepare();

        DataPage<ItemInfo> page = repository.GetItems(new ItemFilter
        {
            Title = title,
            PageSize = 0
        });

        Assert.Equal(count, page.Total);
        Assert.Equal(count, page.Items.Count);
    }

    [Fact]
    public void GetItems_ByDescription_ContainsIgnoringCase()
    {
        MongoCadmusRepository repository = Prepare();

        Assert.Equal(1, repository.GetItems(new ItemFilter
        {
            Description = "OF ITEM 20"
        }).Total);
    }

    [Fact]
    public void GetItems_ByGroup_Contains()
    {
        MongoCadmusRepository repository = Prepare();

        // g00-g09: items 1-19
        Assert.Equal(19, repository.GetItems(new ItemFilter
        {
            GroupId = "g0"
        }).Total);
        Assert.Equal(1, repository.GetItems(new ItemFilter
        {
            GroupId = "g10"
        }).Total);
    }

    [Theory]
    [InlineData(FlagMatching.BitsAllSet, 3, 5)]     // 3 7 11 15 19
    [InlineData(FlagMatching.BitsAnySet, 16, 5)]    // 16-20
    [InlineData(FlagMatching.BitsAllClear, 1, 10)]  // even
    [InlineData(FlagMatching.BitsAnyClear, 3, 15)]  // 20 - 5 with 3 set
    public void GetItems_ByFlags_Ok(FlagMatching matching, int flags,
        int count)
    {
        MongoCadmusRepository repository = Prepare();

        Assert.Equal(count, repository.GetItems(new ItemFilter
        {
            Flags = flags,
            FlagMatching = matching
        }).Total);
    }

    [Fact]
    public void GetItems_ByUserAndTime_Ok()
    {
        MongoCadmusRepository repository = Prepare();

        Assert.Equal(10, repository.GetItems(new ItemFilter
        {
            UserId = "Odd"
        }).Total);
        Assert.Equal(10, repository.GetItems(new ItemFilter
        {
            MinModified = new DateTime(2015, 12, 11, 0, 0, 0, DateTimeKind.Utc)
        }).Total);
        Assert.Equal(5, repository.GetItems(new ItemFilter
        {
            MaxModified = new DateTime(2015, 12, 5, 0, 0, 0, DateTimeKind.Utc)
        }).Total);
        // 11 13 15
        Assert.Equal(3, repository.GetItems(new ItemFilter
        {
            UserId = "Odd",
            MinModified = new DateTime(2015, 12, 11, 0, 0, 0, DateTimeKind.Utc),
            MaxModified = new DateTime(2015, 12, 15, 0, 0, 0, DateTimeKind.Utc)
        }).Total);
    }

    [Fact]
    public void GetItems_PageSize0_AllSorted()
    {
        MongoCadmusRepository repository = Prepare();

        DataPage<ItemInfo> page = repository.GetItems(
            new ItemFilter { PageSize = 0 });

        Assert.Equal(20, page.Total);
        Assert.Equal(20, page.Items.Count);
        Assert.Equal(Enumerable.Range(1, 20).Select(i => $"item-{i:000}"),
            page.Items.Select(i => i.Id));
    }

    [Fact]
    public void GetItem_UnknownPartType_Throws()
    {
        MongoCadmusRepository repository = Prepare();
        TestDb.GetDatabase().GetCollection<MongoPart>(MongoPart.COLLECTION)
            .InsertOne(new MongoPart
            {
                Id = "unknown",
                ItemId = "item-002",
                TypeId = "it.vedph.unknown",
                Content = new BsonDocument("id", "unknown")
            });

        Assert.Throws<InvalidOperationException>(
            () => repository.GetItem("item-002"));
        // no parts, no error
        Assert.NotNull(repository.GetItem("item-002", false));
    }

    [Theory]
    // a part which is not an object
    [InlineData("{\"id\":\"x\",\"facetId\":\"alpha\",\"parts\":[1]}")]
    // a part whose type ID is not a string
    [InlineData("{\"id\":\"x\",\"facetId\":\"alpha\"," +
        "\"parts\":[{\"typeId\":1}]}")]
    // an item with an empty facet
    [InlineData("{\"id\":\"x\",\"facetId\":\"\"}")]
    public void ImportItem_InvalidDocument_Throws(string json)
    {
        MongoCadmusRepository repository = Prepare();

        Assert.Throws<System.IO.InvalidDataException>(
            () => repository.ImportItem(new System.IO.StringReader(json)));
        Assert.Null(repository.GetItem("x"));
    }

    [Fact]
    public void AddItem_Existing_KeepsCreatorAndBuildsSortKey()
    {
        MongoCadmusRepository repository = Prepare();
        IItem item = repository.GetItem("item-001", false)!;
        DateTime created = item.TimeCreated;
        item.CreatorId = "intruder";
        item.TimeCreated = DateTime.UtcNow;
        item.Title = "The New Title";

        repository.AddItem(item);

        IItem saved = repository.GetItem("item-001", false)!;
        Assert.Equal("Odd", saved.CreatorId);
        Assert.Equal(created, saved.TimeCreated);
        Assert.Equal("the new title", saved.SortKey);
        Assert.True(saved.TimeModified > created);
    }

    [Fact]
    public void GetDistinctGroupIds_PageSize0_All()
    {
        MongoCadmusRepository repository = Prepare();

        DataPage<string> page = repository.GetDistinctGroupIdsAsync(
            new PagingOptions { PageSize = 0 }).GetAwaiter().GetResult();

        Assert.Equal(11, page.Total);
        Assert.Equal(Enumerable.Range(0, 11).Select(i => $"g{i:00}"),
            page.Items);
    }

    [Fact]
    public void GetDistinctGroupIds_FilterWithRegexChars_Literal()
    {
        MongoCadmusRepository repository = Prepare();
        repository.SetItemGroupId(["item-001"], "a.b");

        DataPage<string> page = repository.GetDistinctGroupIdsAsync(
            new PagingOptions { PageSize = 0 }, ".")
            .GetAwaiter().GetResult();

        Assert.Equal(["a.b"], page.Items);
    }

    [Fact]
    public void GetGroupLayersCount_NoGroupItems_0()
    {
        MongoCadmusRepository repository = Prepare();

        Assert.Equal(0, repository.GetGroupLayersCountAsync("not-existing")
            .GetAwaiter().GetResult());
    }

    [Fact]
    public void GetGroupLayersCount_EmptyLayersExcluded()
    {
        MongoCadmusRepository repository = Prepare();
        // item-001 is in g00 and has a layer with 2 fragments; add an empty
        // layer to item-001 and a non-empty one to item-002 (g01)
        repository.AddPart(new TokenTextLayerPart<CommentLayerFragment>
        {
            ItemId = "item-001",
            RoleId = PartBase.FR_PREFIX + "it.vedph.comment:empty",
            CreatorId = "zeus",
            UserId = "zeus"
        });
        TokenTextLayerPart<CommentLayerFragment> layer = new()
        {
            ItemId = "item-002",
            CreatorId = "zeus",
            UserId = "zeus"
        };
        layer.AddFragment(new CommentLayerFragment { Location = "1.1" });
        repository.AddPart(layer);

        Assert.Equal(1, repository.GetGroupLayersCountAsync("g00")
            .GetAwaiter().GetResult());
        Assert.Equal(1, repository.GetGroupLayersCountAsync("g01")
            .GetAwaiter().GetResult());
    }
    #endregion

    #region Parts
    private static void SeedHierarchy(MongoCadmusRepository repository)
    {
        // items 1-4 with hierarchy parts having Y=1,2,2,3 and X=1,2,1,1
        int[] ys = [1, 2, 2, 3];
        int[] xs = [1, 2, 1, 1];
        for (int i = 0; i < 4; i++)
        {
            repository.AddPart(new HierarchyPart
            {
                Id = $"h{i + 1}",
                ItemId = $"item-00{i + 1}",
                Y = ys[i],
                X = xs[i],
                CreatorId = "zeus",
                UserId = "zeus"
            });
        }
    }

    [Fact]
    public void GetParts_SortByContent_Ok()
    {
        MongoCadmusRepository repository = Prepare();
        SeedHierarchy(repository);

        DataPage<PartInfo> page = repository.GetParts(new PartFilter
        {
            TypeId = "it.vedph.hierarchy",
            SortExpressions =
            [
                Tuple.Create("content.y", false),
                Tuple.Create("content.x", true)
            ]
        });

        Assert.Equal(["h4", "h3", "h2", "h1"], page.Items.Select(p => p.Id));
    }

    [Fact]
    public void GetParts_SortByProperties_IdAsTieBreaker()
    {
        MongoCadmusRepository repository = Prepare();
        SeedHierarchy(repository);

        // all the parts have the same type and creator
        DataPage<PartInfo> page = repository.GetParts(new PartFilter
        {
            TypeId = "it.vedph.hierarchy",
            PageNumber = 2,
            PageSize = 2,
            SortExpressions =
            [
                Tuple.Create("TypeId", true),
                Tuple.Create("creatorId", true)
            ]
        });

        Assert.Equal(4, page.Total);
        Assert.Equal(["h3", "h4"], page.Items.Select(p => p.Id));
    }

    [Fact]
    public void GetParts_SortByUnknownProperty_Throws()
    {
        MongoCadmusRepository repository = Prepare();

        Assert.Throws<ArgumentException>(() => repository.GetParts(
            new PartFilter
            {
                SortExpressions = [Tuple.Create("NotAProperty", true)]
            }));
    }

    [Fact]
    public void GetParts_PageSize0_All()
    {
        MongoCadmusRepository repository = Prepare();
        SeedHierarchy(repository);

        DataPage<PartInfo> page = repository.GetParts(
            new PartFilter { PageSize = 0 });

        Assert.Equal(7, page.Total);
        Assert.Equal(7, page.Items.Count);
    }

    [Fact]
    public void GetParts_ByUserAndTime_Ok()
    {
        MongoCadmusRepository repository = Prepare();
        // the seeded parts were saved before this time
        DateTime before = DateTime.UtcNow;
        System.Threading.Thread.Sleep(20);
        SeedHierarchy(repository);

        // the seeded parts are by Odd; hierarchy parts by zeus
        Assert.Equal(4, repository.GetParts(new PartFilter
        {
            UserId = "zeus"
        }).Total);
        Assert.Equal(4, repository.GetParts(new PartFilter
        {
            MinModified = before
        }).Total);
        Assert.Equal(0, repository.GetParts(new PartFilter
        {
            MaxModified = before.AddYears(-50)
        }).Total);
    }

    [Fact]
    public void GetPart_AsIPart_Ok()
    {
        MongoCadmusRepository repository = Prepare();

        IPart? part = repository.GetPart<IPart>("part-002");

        Assert.Equal("Some notes.", Assert.IsType<NotePart>(part).Text);
    }

    [Fact]
    public void GetPart_WrongType_Throws()
    {
        MongoCadmusRepository repository = Prepare();

        Assert.Throws<InvalidCastException>(
            () => repository.GetPart<CategoriesPart>("part-002"));
    }

    [Fact]
    public void GetPart_UnknownType_Throws()
    {
        MongoCadmusRepository repository = Prepare();
        TestDb.GetDatabase().GetCollection<MongoPart>(MongoPart.COLLECTION)
            .InsertOne(new MongoPart
            {
                Id = "unknown",
                ItemId = "item-002",
                TypeId = "it.vedph.unknown",
                Content = new BsonDocument("id", "unknown")
            });

        Assert.Throws<InvalidOperationException>(
            () => repository.GetPart<IPart>("unknown"));
    }

    [Fact]
    public void GetPartItemId_Ok()
    {
        MongoCadmusRepository repository = Prepare();

        Assert.Equal("item-001", repository.GetPartItemId("part-002"));
        Assert.Null(repository.GetPartItemId("not-existing"));
    }

    [Theory]
    [InlineData("{\"id\":\"x\"}")]
    [InlineData("{\"id\":\"x\",\"typeId\":null}")]
    [InlineData("[]")]
    [InlineData("{\"typeId\":\"it.vedph.unknown\"}")]
    public void AddPartFromContent_Invalid_Throws(string content)
    {
        MongoCadmusRepository repository = Prepare();

        Assert.Throws<InvalidOperationException>(
            () => repository.AddPartFromContent(content));
    }

    [Fact]
    public void AddPart_Existing_KeepsCreator()
    {
        MongoCadmusRepository repository = Prepare();
        NotePart part = repository.GetPart<NotePart>("part-002")!;
        DateTime created = part.TimeCreated;
        part.CreatorId = "intruder";
        part.TimeCreated = DateTime.UtcNow;
        part.UserId = "Even";
        part.Text = "Changed.";

        repository.AddPart(part);

        NotePart saved = repository.GetPart<NotePart>("part-002")!;
        Assert.Equal("Odd", saved.CreatorId);
        // the database stores times with milliseconds precision
        Assert.Equal(created, saved.TimeCreated, TimeSpan.FromMilliseconds(1));
        Assert.Equal("Even", saved.UserId);
        Assert.Equal("Changed.", saved.Text);
        // the part info and the content agree
        PartInfo info = repository.GetParts(new PartFilter
        {
            UserId = "Even"
        }).Items.Single();
        Assert.Equal("part-002", info.Id);
        Assert.Equal("Odd", info.CreatorId);
    }

    [Fact]
    public void SetPartThesaurusScope_Null_Cleared()
    {
        MongoCadmusRepository repository = Prepare();
        repository.SetPartThesaurusScope(["part-001"], "scope");

        repository.SetPartThesaurusScope(["part-001"], null);

        Assert.Equal(0, repository.GetParts(new PartFilter
        {
            ThesaurusScope = "scope"
        }).Total);
        Assert.Null(repository.GetPart<IPart>("part-001")!.ThesaurusScope);
    }

    [Fact]
    public void SetPartThesaurusScope_TypedPartUpdated()
    {
        MongoCadmusRepository repository = Prepare();

        repository.SetPartThesaurusScope(["part-001"], "scope");

        Assert.Equal("scope",
            repository.GetPart<IPart>("part-001")!.ThesaurusScope);
        Assert.Null(repository.GetPart<IPart>("part-002")!.ThesaurusScope);
    }

    [Fact]
    public void GetItemLayerInfo_LayerWithoutFragments_Count0()
    {
        MongoCadmusRepository repository = Prepare();
        TestDb.GetDatabase().GetCollection<MongoPart>(MongoPart.COLLECTION)
            .InsertOne(new MongoPart
            {
                Id = "odd-layer",
                ItemId = "item-001",
                TypeId = "it.vedph.token-text-layer",
                RoleId = PartBase.FR_PREFIX + "zzz",
                Content = new BsonDocument("id", "odd-layer")
            });

        IList<LayerPartInfo> infos = repository.GetItemLayerInfo("item-001",
            false);

        Assert.Equal(2, infos.Count);
        Assert.Equal("part-003", infos[0].Id);
        Assert.Equal(2, infos[0].FragmentCount);
        Assert.Equal("Odd", infos[0].CreatorId);
        Assert.Equal("item-001", infos[0].ItemId);
        Assert.False(infos[0].IsAbsent);
        Assert.Equal("odd-layer", infos[1].Id);
        Assert.Equal(0, infos[1].FragmentCount);
    }
    #endregion

    #region Settings
    [Fact]
    public void GetSetting_NotExisting_Null()
    {
        MongoCadmusRepository repository = Prepare();

        Assert.Null(repository.GetSetting("not-existing"));
    }

    [Fact]
    public void AddSetting_NotExisting_AddedWithoutId()
    {
        MongoCadmusRepository repository = Prepare();

        repository.AddSetting("editor", "{\"size\":12,\"names\":[\"a\",\"b\"]," +
            "\"nested\":{\"on\":true}}");

        string? json = repository.GetSetting("editor");
        Assert.NotNull(json);
        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("_id", out _));
        Assert.Equal(12, doc.RootElement.GetProperty("size").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("names").GetArrayLength());
        Assert.True(doc.RootElement.GetProperty("nested").GetProperty("on")
            .GetBoolean());
    }

    [Fact]
    public void AddSetting_Existing_Replaced()
    {
        MongoCadmusRepository repository = Prepare();
        repository.AddSetting("editor", "{\"size\":12,\"old\":1}");

        repository.AddSetting("editor", "{\"size\":14}");

        using JsonDocument doc = JsonDocument.Parse(
            repository.GetSetting("editor")!);
        Assert.Equal(14, doc.RootElement.GetProperty("size").GetInt32());
        Assert.False(doc.RootElement.TryGetProperty("old", out _));
    }

    [Fact]
    public void AddSetting_ValueWithOtherId_StoredUnderKey()
    {
        MongoCadmusRepository repository = Prepare();
        repository.AddSetting("a", "{\"size\":12}");

        // e.g. a setting got from "a" and saved as "b"
        repository.AddSetting("b", "{\"_id\":\"a\",\"size\":14}");

        Assert.Contains("12", repository.GetSetting("a"));
        Assert.Contains("14", repository.GetSetting("b"));
    }

    [Fact]
    public void DeleteSetting_Existing_Deleted()
    {
        MongoCadmusRepository repository = Prepare();
        repository.AddSetting("editor", "{\"size\":12}");

        repository.DeleteSetting("editor");

        Assert.Null(repository.GetSetting("editor"));
        // deleting a missing setting does nothing
        repository.DeleteSetting("editor");
    }

    [Fact]
    public void AddSetting_InvalidArgs_Throws()
    {
        MongoCadmusRepository repository = Prepare();

        Assert.Throws<ArgumentNullException>(
            () => repository.AddSetting(null!, "{}"));
        Assert.Throws<ArgumentException>(
            () => repository.AddSetting("", "{}"));
        Assert.Throws<ArgumentException>(
            () => repository.AddSetting("key", ""));
    }
    #endregion
}

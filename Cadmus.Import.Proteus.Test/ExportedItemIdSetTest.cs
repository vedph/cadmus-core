using Cadmus.Core;
using Cadmus.General.Parts;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Proteus.Test;

public sealed class ExportedItemIdSetTest
{
    private const string JSON = """
        [
          {
            "id": "i1",
            "title": "Item 1",
            "metadataPartId": "m1",
            "eids": ["a", "b"]
          },
          {
            "id": "i2",
            "title": "Item 2"
          },
          {
            "id": "i3",
            "title": "Item 3",
            "metadataPartId": "m3",
            "eids": []
          }
        ]
        """;

    private static ExportedItemId Ei(string id, params string[] eids) =>
        new() { Id = id, Title = "T" + id, MetadataPartId = "m" + id, Eids = eids };

    #region Loading
    [Fact]
    public void Parse_Valid_Ok()
    {
        ExportedItemIdSet set = ExportedItemIdSet.Parse(JSON);

        Assert.Equal(3, set.Count);
        Assert.Equal(["i1", "i2", "i3"], set.Items.Select(i => i.Id));

        ExportedItemId i1 = set.Items[0];
        Assert.Equal("Item 1", i1.Title);
        Assert.Equal("m1", i1.MetadataPartId);
        Assert.Equal(["a", "b"], i1.Eids);

        ExportedItemId i2 = set.Items[1];
        Assert.Null(i2.MetadataPartId);
        Assert.Empty(i2.Eids);

        Assert.Equal("m3", set.Items[2].MetadataPartId);
        Assert.Empty(set.Items[2].Eids);
    }

    [Fact]
    public void Parse_EmptyArray_Empty()
    {
        ExportedItemIdSet set = ExportedItemIdSet.Parse("[]");

        Assert.Empty(set);
        Assert.Empty(set.Eids);
        Assert.Empty(set.ItemsById);
    }

    [Fact]
    public void Parse_NullsInData_Normalized()
    {
        ExportedItemIdSet set = ExportedItemIdSet.Parse(
            """[{ "id": "i1", "title": null, "eids": ["a", null, ""] },""" +
            """ { "id": "i2", "eids": null }]""");

        Assert.Equal("", set.Items[0].Title);
        Assert.Equal(["a"], set.Items[0].Eids);
        Assert.Empty(set.Items[1].Eids);
        Assert.Equal(["a"], set.Eids);
    }

    [Fact]
    public void Parse_CaseInsensitiveNamesAndUnknownProperties_Ok()
    {
        ExportedItemIdSet set = ExportedItemIdSet.Parse(
            """[{ "ID": "i1", "Title": "x", "extra": 1, "EIDS": ["a"] }]""");

        Assert.Equal("i1", set.Items[0].Id);
        Assert.Equal(["a"], set.Items[0].Eids);
    }

    [Fact]
    public void Parse_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => ExportedItemIdSet.Parse(null!));
    }

    [Fact]
    public void Parse_NullRoot_Throws()
    {
        Assert.Throws<InvalidDataException>(
            () => ExportedItemIdSet.Parse("null"));
    }

    [Fact]
    public void Parse_NullItem_Throws()
    {
        Assert.Throws<InvalidDataException>(
            () => ExportedItemIdSet.Parse("""[{ "id": "i1" }, null]"""));
    }

    [Theory]
    [InlineData("""[{ "id": null }]""")]
    [InlineData("""[{ "id": "" }]""")]
    public void Parse_NoId_Throws(string json)
    {
        Assert.Throws<InvalidDataException>(
            () => ExportedItemIdSet.Parse(json));
    }

    [Fact]
    public void Parse_MissingId_Throws()
    {
        // id is required
        Assert.Throws<JsonException>(
            () => ExportedItemIdSet.Parse("""[{ "title": "x" }]"""));
    }

    [Fact]
    public void Parse_DuplicateId_Throws()
    {
        Assert.Throws<InvalidDataException>(() => ExportedItemIdSet.Parse(
            """[{ "id": "i1" }, { "id": "i1" }]"""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("[{")]
    public void Parse_InvalidJson_Throws(string json)
    {
        Assert.Throws<JsonException>(() => ExportedItemIdSet.Parse(json));
    }

    [Fact]
    public void Load_Stream_LeftOpen()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(JSON));

        ExportedItemIdSet set = ExportedItemIdSet.Load(stream);

        Assert.Equal(3, set.Count);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Load_TextReader_Ok()
    {
        using StringReader reader = new(JSON);

        ExportedItemIdSet set = ExportedItemIdSet.Load(reader);

        Assert.Equal(3, set.Count);
    }

    [Fact]
    public async Task LoadAsync_Stream_Ok()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(JSON));

        ExportedItemIdSet set = await ExportedItemIdSet.LoadAsync(stream,
            cancel: TestContext.Current.CancellationToken);

        Assert.Equal(3, set.Count);
    }

    [Fact]
    public async Task Load_File_SyncAndAsync_Ok()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, JSON,
                TestContext.Current.CancellationToken);

            Assert.Equal(3, ExportedItemIdSet.Load(path).Count);
            Assert.Equal(3, (await ExportedItemIdSet.LoadAsync(path,
                cancel: TestContext.Current.CancellationToken)).Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Load_ExporterOutput_RoundTrip()
    {
        string path = Path.Combine(Path.GetTempPath(),
            $"cadmus-item-ids-{Guid.NewGuid():N}.json");
        try
        {
            // export two items, one with metadata
            Item item1 = new() { Id = "g1", Title = "Ἀχιλλεύς \"1\"" };
            item1.Parts.Add(new MetadataPart
            {
                Id = "gm1",
                ItemId = item1.Id,
                Metadata =
                [
                    new Metadatum { Name = "eid", Value = "achilles" },
                    new Metadatum { Name = "x", Value = "y" },
                ],
            });
            Item item2 = new() { Id = "g2", Title = "Item 2" };
            CadmusEntrySetContext context = new();
            context.Items.AddRange([item1, item2]);

            ItemIdEntrySetExporter exporter = new();
            exporter.Configure(new ItemIdEntrySetExporterOptions
            {
                OutputPath = path,
            });
            await exporter.OpenAsync();
            await exporter.ExportAsync(new EntrySet(context),
                new EntryRegionSet());
            await exporter.CloseAsync();

            // load it back
            ExportedItemIdSet set = await ExportedItemIdSet.LoadAsync(path,
                cancel: TestContext.Current.CancellationToken);

            Assert.Equal(2, set.Count);
            ExportedItemId? achilles = set.Get("achilles");
            Assert.NotNull(achilles);
            Assert.Equal("g1", achilles.Id);
            Assert.Equal("Ἀχιλλεύς \"1\"", achilles.Title);
            Assert.Equal("gm1", achilles.MetadataPartId);
            Assert.Null(set.GetById("g2")!.MetadataPartId);
        }
        finally
        {
            File.Delete(path);
        }
    }
    #endregion

    #region Constructor
    [Fact]
    public void Ctor_NullItems_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ExportedItemIdSet(null!));
    }

    [Fact]
    public void Ctor_MergedSets_Ok()
    {
        ExportedItemIdSet a = new([Ei("1", "a")]);
        ExportedItemIdSet b = new([Ei("2", "b")]);

        ExportedItemIdSet merged = new(a.Concat(b));

        Assert.Equal(["1", "2"], merged.Select(i => i.Id));
        Assert.Equal("2", merged.Get("b")!.Id);
    }

    [Fact]
    public void Ctor_NullEidInCodeBuiltItem_Skipped()
    {
        ExportedItemIdSet set = new([Ei("1", "a", null!, "")]);

        Assert.Equal(["a"], set.Eids);
    }
    #endregion

    #region Lookup
    [Fact]
    public void Get_Existing_Ok()
    {
        ExportedItemIdSet set = ExportedItemIdSet.Parse(JSON);

        Assert.Equal("i1", set.Get("a")!.Id);
        Assert.Equal("i1", set.Get("b")!.Id);
    }

    [Fact]
    public void Get_NotExisting_Null()
    {
        ExportedItemIdSet set = ExportedItemIdSet.Parse(JSON);

        Assert.Null(set.Get("z"));
        Assert.False(set.ContainsEid("z"));
        Assert.False(set.TryGet("z", out ExportedItemId? item));
        Assert.Null(item);
        Assert.Empty(set.GetAll("z"));
    }

    [Fact]
    public void Lookup_NullArgs_Throw()
    {
        ExportedItemIdSet set = ExportedItemIdSet.Parse(JSON);

        Assert.Throws<ArgumentNullException>(() => set.Get(null!));
        Assert.Throws<ArgumentNullException>(() => set.TryGet(null!, out _));
        Assert.Throws<ArgumentNullException>(() => set.GetAll(null!));
        Assert.Throws<ArgumentNullException>(() => set.ContainsEid(null!));
        Assert.Throws<ArgumentNullException>(() => set.GetById(null!));
    }

    [Fact]
    public void TryGet_Existing_True()
    {
        ExportedItemIdSet set = ExportedItemIdSet.Parse(JSON);

        Assert.True(set.TryGet("b", out ExportedItemId? item));
        Assert.Equal("i1", item.Id);
        Assert.True(set.ContainsEid("b"));
    }

    [Fact]
    public void Get_DefaultComparer_CaseSensitive()
    {
        ExportedItemIdSet set = ExportedItemIdSet.Parse(JSON);

        Assert.Null(set.Get("A"));
    }

    [Fact]
    public void Get_CustomComparer_Honored()
    {
        ExportedItemIdSet set = ExportedItemIdSet.Parse(JSON,
            StringComparer.OrdinalIgnoreCase);

        Assert.Equal("i1", set.Get("A")!.Id);
        Assert.Same(StringComparer.OrdinalIgnoreCase, set.EidComparer);
    }

    [Fact]
    public void GetById_Ok()
    {
        ExportedItemIdSet set = ExportedItemIdSet.Parse(JSON);

        Assert.Equal("Item 2", set.GetById("i2")!.Title);
        Assert.Null(set.GetById("zz"));
        Assert.Equal(["i1", "i2", "i3"], set.ItemsById.Keys.Order());
    }

    [Fact]
    public void Eids_AllDistinct()
    {
        ExportedItemIdSet set = new([Ei("1", "a", "b"), Ei("2", "b", "c")]);

        Assert.Equal(["a", "b", "c"], set.Eids.Order());
    }

    [Fact]
    public void DuplicateEids_FirstWinsAndAllListed()
    {
        ExportedItemIdSet set = new(
            [Ei("1", "a", "dup"), Ei("2", "dup"), Ei("3", "c")]);

        Assert.Equal("1", set.Get("dup")!.Id);
        Assert.Equal(["1", "2"], set.GetAll("dup").Select(i => i.Id));

        List<KeyValuePair<string, IReadOnlyList<ExportedItemId>>> dupes =
            [.. set.GetDuplicateEids()];
        KeyValuePair<string, IReadOnlyList<ExportedItemId>> dupe =
            Assert.Single(dupes);
        Assert.Equal("dup", dupe.Key);
        Assert.Equal(2, dupe.Value.Count);
    }

    [Fact]
    public void SameEidRepeatedInItem_NotADuplicate()
    {
        ExportedItemIdSet set = new([Ei("1", "a", "a")]);

        Assert.Single(set.GetAll("a"));
        Assert.Empty(set.GetDuplicateEids());
    }

    [Fact]
    public void Enumerate_OriginalOrder()
    {
        ExportedItemIdSet set = new([Ei("3"), Ei("1"), Ei("2")]);

        Assert.Equal(["3", "1", "2"], set.Select(i => i.Id));
    }
    #endregion
}

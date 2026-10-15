using Cadmus.Core;
using Cadmus.General.Parts;
using Proteus.Core.Regions;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Proteus.Test;

public sealed class ItemIdEntrySetExporterTest : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public ItemIdEntrySetExporterTest()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cadmus-item-id-test-" +
            Guid.NewGuid().ToString("N"));
        // use a not-yet-existing subdirectory to check it gets created
        _path = Path.Combine(_dir, "sub", "item-ids.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private ItemIdEntrySetExporter CreateExporter(bool indented = true)
    {
        ItemIdEntrySetExporter exporter = new();
        exporter.Configure(new ItemIdEntrySetExporterOptions
        {
            OutputPath = _path,
            Indented = indented,
        });
        return exporter;
    }

    private static Item CreateItem(string id, string title,
        params Metadatum[] metadata)
    {
        Item item = new()
        {
            Id = id,
            Title = title,
            FacetId = "default",
            CreatorId = "zeus",
            UserId = "zeus",
        };
        item.Parts.Add(new NotePart
        {
            ItemId = id,
            Text = "note",
        });
        if (metadata.Length > 0)
        {
            item.Parts.Add(new MetadataPart
            {
                Id = "m-" + id,
                ItemId = id,
                Metadata = [.. metadata],
            });
        }
        return item;
    }

    private static Metadatum Md(string name, string? value) =>
        new() { Name = name, Value = value };

    private static async Task ExportItemsAsync(ItemIdEntrySetExporter exporter,
        params IItem[] items)
    {
        CadmusEntrySetContext context = new();
        context.Items.AddRange(items);
        await exporter.ExportAsync(new EntrySet(context), new EntryRegionSet());
    }

    private JsonElement ReadOutput()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(_path));
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void Configure_Null_Throws()
    {
        ItemIdEntrySetExporter exporter = new();
        Assert.Throws<ArgumentNullException>(() => exporter.Configure(null!));
    }

    [Fact]
    public async Task OpenAsync_NoOutputPath_Throws()
    {
        ItemIdEntrySetExporter exporter = new();
        exporter.Configure(new ItemIdEntrySetExporterOptions { OutputPath = "" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            exporter.OpenAsync);
    }

    [Fact]
    public async Task ExportAsync_NotOpen_Throws()
    {
        ItemIdEntrySetExporter exporter = CreateExporter();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ExportItemsAsync(exporter, CreateItem("i1", "Item 1")));
    }

    [Fact]
    public async Task ExportAsync_NonCadmusContext_Throws()
    {
        ItemIdEntrySetExporter exporter = CreateExporter();
        await exporter.OpenAsync();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => exporter.ExportAsync(new EntrySet(new EntrySetContext()),
                    new EntryRegionSet()));
        }
        finally
        {
            await exporter.CloseAsync();
        }
    }

    [Fact]
    public async Task Export_NoSets_EmptyArray()
    {
        ItemIdEntrySetExporter exporter = CreateExporter();

        await exporter.OpenAsync();
        await exporter.CloseAsync();

        Assert.Equal(0, ReadOutput().GetArrayLength());
    }

    [Fact]
    public async Task Export_EmptyContext_EmptyArray()
    {
        ItemIdEntrySetExporter exporter = CreateExporter();

        await exporter.OpenAsync();
        await ExportItemsAsync(exporter);
        await exporter.CloseAsync();

        Assert.Equal(0, ReadOutput().GetArrayLength());
    }

    [Fact]
    public async Task Export_ItemWithoutMetadataPart_IdAndTitleOnly()
    {
        ItemIdEntrySetExporter exporter = CreateExporter();

        await exporter.OpenAsync();
        await ExportItemsAsync(exporter, CreateItem("i1", "Item 1"));
        await exporter.CloseAsync();

        JsonElement root = ReadOutput();
        Assert.Equal(1, root.GetArrayLength());
        JsonElement item = root[0];
        Assert.Equal("i1", item.GetProperty("id").GetString());
        Assert.Equal("Item 1", item.GetProperty("title").GetString());
        Assert.False(item.TryGetProperty("metadataPartId", out _));
        Assert.False(item.TryGetProperty("eids", out _));
    }

    [Fact]
    public async Task Export_ItemWithMetadataPart_OnlyNonEmptyEidsWritten()
    {
        ItemIdEntrySetExporter exporter = CreateExporter();

        await exporter.OpenAsync();
        await ExportItemsAsync(exporter, CreateItem("i1", "Item 1",
            Md("eid", "alpha"),
            Md("other", "x"),
            Md("EID", "wrong-case"),
            Md("eid", ""),
            Md("eid", null),
            Md("eid", "beta")));
        await exporter.CloseAsync();

        JsonElement item = ReadOutput()[0];
        Assert.Equal("m-i1", item.GetProperty("metadataPartId").GetString());
        string?[] eids = [.. item.GetProperty("eids").EnumerateArray()
            .Select(e => e.GetString())];
        Assert.Equal(["alpha", "beta"], eids);
    }

    [Fact]
    public async Task Export_MetadataPartWithoutEids_EmptyEidsArray()
    {
        ItemIdEntrySetExporter exporter = CreateExporter();

        await exporter.OpenAsync();
        await ExportItemsAsync(exporter, CreateItem("i1", "Item 1",
            Md("other", "x")));
        await exporter.CloseAsync();

        JsonElement item = ReadOutput()[0];
        Assert.Equal("m-i1", item.GetProperty("metadataPartId").GetString());
        Assert.Equal(0, item.GetProperty("eids").GetArrayLength());
    }

    [Fact]
    public async Task Export_MultipleMetadataParts_DefaultRolePreferred()
    {
        Item item = CreateItem("i1", "Item 1");
        item.Parts.Add(new MetadataPart
        {
            Id = "m-role",
            ItemId = item.Id,
            RoleId = "role",
            Metadata = [Md("eid", "from-role")],
        });
        item.Parts.Add(new MetadataPart
        {
            Id = "m-default",
            ItemId = item.Id,
            Metadata = [Md("eid", "from-default")],
        });
        ItemIdEntrySetExporter exporter = CreateExporter();

        await exporter.OpenAsync();
        await ExportItemsAsync(exporter, item);
        await exporter.CloseAsync();

        JsonElement json = ReadOutput()[0];
        Assert.Equal("m-default", json.GetProperty("metadataPartId").GetString());
        Assert.Equal("from-default", json.GetProperty("eids")[0].GetString());
    }

    [Fact]
    public async Task Export_MultipleSets_AllItemsInOrder()
    {
        ItemIdEntrySetExporter exporter = CreateExporter(indented: false);

        await exporter.OpenAsync();
        await ExportItemsAsync(exporter,
            CreateItem("i1", "Item 1", Md("eid", "a")),
            CreateItem("i2", "Item 2"));
        await ExportItemsAsync(exporter);
        await ExportItemsAsync(exporter,
            CreateItem("i3", "Ἀχιλλεύς \"quoted\"", Md("eid", "c")));
        await exporter.CloseAsync();

        JsonElement root = ReadOutput();
        Assert.Equal(["i1", "i2", "i3"],
            root.EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        Assert.Equal("Ἀχιλλεύς \"quoted\"",
            root[2].GetProperty("title").GetString());
        // non-ASCII text is not escaped in output
        Assert.Contains("Ἀχιλλεύς", File.ReadAllText(_path));
    }

    [Fact]
    public async Task Export_Reopen_OverwritesOutput()
    {
        ItemIdEntrySetExporter exporter = CreateExporter();

        await exporter.OpenAsync();
        await ExportItemsAsync(exporter, CreateItem("i1", "Item 1"));
        await exporter.CloseAsync();

        await exporter.OpenAsync();
        await ExportItemsAsync(exporter, CreateItem("i2", "Item 2"));
        await exporter.CloseAsync();

        JsonElement root = ReadOutput();
        Assert.Equal(1, root.GetArrayLength());
        Assert.Equal("i2", root[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task CloseAsync_Twice_NoThrow()
    {
        ItemIdEntrySetExporter exporter = CreateExporter();

        await exporter.OpenAsync();
        await exporter.CloseAsync();
        await exporter.CloseAsync();

        Assert.Equal(0, ReadOutput().GetArrayLength());
    }

    [Fact]
    public async Task Export_Disabled_NoOutput()
    {
        ItemIdEntrySetExporter exporter = new();
        exporter.Configure(new ItemIdEntrySetExporterOptions
        {
            OutputPath = _path,
            IsDisabled = true,
        });

        await exporter.OpenAsync();
        await ExportItemsAsync(exporter, CreateItem("i1", "Item 1"));
        await exporter.CloseAsync();

        Assert.False(File.Exists(_path));
    }
}

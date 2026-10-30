using Cadmus.Core.Config;
using Cadmus.Core;
using Cadmus.Mongo;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Cadmus.General.Parts;
using Proteus.Core.Regions;
using MongoDB.Driver;

namespace Cadmus.Import.Proteus.Test;

// this test requires MongoDB

[Collection(nameof(NonParallelResourceCollection))]
public sealed class MongoEntrySetExporterTest
{
    // each test project uses its own database, as test projects can run
    // in parallel
    private const string DB_NAME = "cadmus-import-proteus-test";
    private const string CONNECTION_STRING = "mongodb://localhost:27017/" + DB_NAME;

    private static void ClearDatabase()
    {
        MongoClient client = new(CONNECTION_STRING);
        client.DropDatabase(DB_NAME);
    }

    private MongoCadmusRepository CreateRepository()
    {
        TagAttributeToTypeMap map = new();
        map.Add([typeof(NotePart).Assembly]);
        MongoCadmusRepository repository = new(
            new StandardPartTypeProvider(map),
            new StandardItemSortKeyBuilder());

        repository.Configure(new MongoCadmusRepositoryOptions
        {
            ConnectionString = CONNECTION_STRING,
        });

        return repository;
    }

    [Fact]
    public async Task ExportAsync_Ok()
    {
        ClearDatabase();

        // build data context
        CadmusEntrySetContext context = new();
        string[] guids = [ "061054da-3c43-41d5-806c-2bd0e4957eff",
            "7c3d45dd-0376-48a3-a060-659c1cd9d6f2" ];

        for (int n = 1; n <= 2; n++)
        {
            // item
            Item item = new()
            {
                Id = guids[n - 1],
                Title = $"Item {n}",
                Description = $"Item nr.{n}",
                FacetId = "default",
                TimeCreated = DateTime.Now,
                CreatorId = "zeus",
                UserId = "zeus",
            };

            // parts
            NotePart part = new()
            {
                ItemId = item.Id,
                Tag = "tag",
                Text = $"Note about {n}",
                CreatorId = "zeus",
                UserId = "zeus",
            };
            item.Parts.Add(part);

            context.Items.Add(item);
        }

        // create exporter
        MongoEntrySetExporter exporter = new();
        exporter.Configure(new MongoEntrySetExporterOptions
        {
            ConnectionString = CONNECTION_STRING
        });

        // export
        await exporter.OpenAsync();
        await exporter.ExportAsync(new EntrySet(context), new EntryRegionSet());
        await exporter.CloseAsync();

        // check
        MongoCadmusRepository repository = CreateRepository();
        for (int n = 1; n <= 2; n++)
        {
            IItem? item = repository.GetItem(guids[n - 1], true);
            Assert.NotNull(item);
            Assert.Equal($"Item {n}", item!.Title);
            Assert.Equal($"Item nr.{n}", item.Description);
            Assert.Equal("default", item.FacetId);
            Assert.Equal($"item {n}", item.SortKey);
            Assert.Equal("zeus", item.CreatorId);
            Assert.Equal("zeus", item.UserId);
            Assert.Single(item.Parts);
            // note part
            NotePart? part = item.Parts[0] as NotePart;
            Assert.NotNull(part);
            Assert.Equal("tag", part!.Tag);
            Assert.Equal($"Note about {n}", part.Text);
            Assert.Equal(item.Id, part.ItemId);
            Assert.Equal("zeus", part.CreatorId);
            Assert.Equal("zeus", part.UserId);
        }
    }

    private static Item CreateItem(string id, string title) => new()
    {
        Id = id,
        Title = title,
        Description = title,
        FacetId = "default",
        TimeCreated = DateTime.Now,
        CreatorId = "zeus",
        UserId = "zeus",
    };

    private static NotePart CreateNotePart(string itemId, string text,
        string? roleId = null) => new()
    {
        ItemId = itemId,
        RoleId = roleId,
        Text = text,
        CreatorId = "zeus",
        UserId = "zeus",
    };

    private static CategoriesPart CreateCategoriesPart(string itemId,
        params string[] categories)
    {
        CategoriesPart part = new()
        {
            ItemId = itemId,
            CreatorId = "zeus",
            UserId = "zeus",
        };
        part.Categories.UnionWith(categories);
        return part;
    }

    private static async Task ExportAsync(CadmusEntrySetContext context,
        bool merge)
    {
        MongoEntrySetExporter exporter = new();
        exporter.Configure(new MongoEntrySetExporterOptions
        {
            ConnectionString = CONNECTION_STRING,
            IsMergeEnabled = merge
        });
        await exporter.OpenAsync();
        await exporter.ExportAsync(new EntrySet(context), new EntryRegionSet());
        await exporter.CloseAsync();
    }

    private const string ITEM_ID = "061054da-3c43-41d5-806c-2bd0e4957eff";

    [Fact]
    public async Task ExportAsync_MergeNotExisting_Added()
    {
        ClearDatabase();

        CadmusEntrySetContext context = new();
        Item item = CreateItem(ITEM_ID, "Item");
        item.Parts.Add(CreateNotePart(ITEM_ID, "note"));
        context.Items.Add(item);

        await ExportAsync(context, true);

        MongoCadmusRepository repository = CreateRepository();
        IItem? stored = repository.GetItem(ITEM_ID, true);
        Assert.NotNull(stored);
        Assert.Equal("Item", stored!.Title);
        Assert.Single(stored.Parts);
        Assert.Equal("note", ((NotePart)stored.Parts[0]).Text);
    }

    [Fact]
    public async Task ExportAsync_MergeExisting_PartsAddedAndReplaced()
    {
        ClearDatabase();

        // 1st pass: item with note
        CadmusEntrySetContext context = new();
        Item item = CreateItem(ITEM_ID, "Item");
        NotePart oldNote = CreateNotePart(ITEM_ID, "old note");
        item.Parts.Add(oldNote);
        context.Items.Add(item);
        await ExportAsync(context, true);

        // 2nd pass: same item ID with new note (replacing old) and categories
        context = new();
        item = CreateItem(ITEM_ID, "Item 2nd pass");
        NotePart newNote = CreateNotePart(ITEM_ID, "new note");
        item.Parts.Add(newNote);
        CategoriesPart categories = CreateCategoriesPart(ITEM_ID, "alpha");
        item.Parts.Add(categories);
        context.Items.Add(item);
        await ExportAsync(context, true);

        MongoCadmusRepository repository = CreateRepository();
        IItem? stored = repository.GetItem(ITEM_ID, true);
        Assert.NotNull(stored);
        // existing item metadata is preserved
        Assert.Equal("Item", stored!.Title);
        Assert.Equal(2, stored.Parts.Count);

        NotePart? note = stored.Parts.OfType<NotePart>().SingleOrDefault();
        Assert.NotNull(note);
        Assert.Equal(newNote.Id, note!.Id);
        Assert.Equal("new note", note.Text);
        Assert.Null(repository.GetPart<NotePart>(oldNote.Id));

        CategoriesPart? cats = stored.Parts.OfType<CategoriesPart>()
            .SingleOrDefault();
        Assert.NotNull(cats);
        Assert.Equal(categories.Id, cats!.Id);
        Assert.Equal("alpha", Assert.Single(cats.Categories));
    }

    [Fact]
    public async Task ExportAsync_MergeExistingDifferentRole_PartAdded()
    {
        ClearDatabase();

        // 1st pass: item with note without role
        CadmusEntrySetContext context = new();
        Item item = CreateItem(ITEM_ID, "Item");
        item.Parts.Add(CreateNotePart(ITEM_ID, "note"));
        context.Items.Add(item);
        await ExportAsync(context, true);

        // 2nd pass: note with role
        context = new();
        item = CreateItem(ITEM_ID, "Item");
        item.Parts.Add(CreateNotePart(ITEM_ID, "role note", "comment"));
        context.Items.Add(item);
        await ExportAsync(context, true);

        MongoCadmusRepository repository = CreateRepository();
        IItem? stored = repository.GetItem(ITEM_ID, true);
        Assert.NotNull(stored);
        Assert.Equal(2, stored!.Parts.Count);
        Assert.Contains(stored.Parts, p => p.RoleId == null
            && ((NotePart)p).Text == "note");
        Assert.Contains(stored.Parts, p => p.RoleId == "comment"
            && ((NotePart)p).Text == "role note");
    }
}

using Cadmus.Core;
using Fusi.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Index.PgSql.Test;

[Collection(nameof(DbCollection))]
public sealed class PgSqlItemIndexWriterTest
{
    private const string ITEM1 = "00000000-0000-0000-0000-000000000001";
    private const string ITEM2 = "00000000-0000-0000-0000-000000000002";
    private const string PART1 = "10000000-0000-0000-0000-000000000001";
    private const string PART2 = "10000000-0000-0000-0000-000000000002";

    private static async Task<PgSqlItemIndexWriter> GetClearedWriterAsync()
    {
        PgSqlItemIndexWriter writer = TestHelper.GetWriter();
        await writer.CreateIndexAsync();
        await writer.ClearAsync();
        return writer;
    }

    private static Task<long> CountPinsAsync(string? partId = null) =>
        partId == null
            ? TestHelper.CountAsync("SELECT COUNT(*) FROM pin")
            : TestHelper.CountAsync(
                "SELECT COUNT(*) FROM pin WHERE part_id=$1",
                TestHelper.DB_NAME, partId);

    private static Task<List<string>> GetPinsAsync(string itemId) =>
        TestHelper.ListAsync("SELECT name || '=' || value FROM pin " +
            "WHERE item_id=$1 ORDER BY name, value", TestHelper.DB_NAME, itemId);

    private static Item GetItemWithParts()
    {
        Item item = TestHelper.GetItem(ITEM1, "Alpha", "a", 1);
        TestHelper.AddPart(item, PART1).AddPin("tag", "odd").AddPin("year", "1200");
        TestHelper.AddPart(item, PART2, "it.vedph.categories", "fr.x")
            .AddPin("category", "lit");
        return item;
    }

    #region Configuration
    [Fact]
    public void Configure_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PgSqlItemIndexWriter().Configure(null!));
        Assert.Throws<ArgumentNullException>(
            () => new PgSqlItemIndexWriter().ConnectionString = null!);
    }

    [Fact]
    public async Task Write_NoConnectionString_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new PgSqlItemIndexWriter().WriteItemAsync(
                TestHelper.GetItem(ITEM1, "A", "a")));
    }

    [Fact]
    public void GetPgSqlSchema_Ok()
    {
        Assert.Equal(PgSqlIndexSchema.GetSql(),
            PgSqlItemIndexWriter.GetPgSqlSchema());
    }
    #endregion

    #region Arguments
    [Fact]
    public async Task NullArguments_Throw()
    {
        PgSqlItemIndexWriter writer = TestHelper.GetWriter();
        Item item = TestHelper.GetItem(ITEM1, "A", "a");
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => writer.WriteItemAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => writer.WriteItemsAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => writer.WritePartAsync(null!, new TestPart()));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => writer.WritePartAsync(item, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => writer.DeleteItemAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => writer.DeletePartAsync(null!));
    }

    [Fact]
    public async Task InvalidIds_Throw()
    {
        PgSqlItemIndexWriter writer = TestHelper.GetWriter();
        await Assert.ThrowsAsync<ArgumentException>(
            () => writer.WriteItemAsync(TestHelper.GetItem("", "A", "a")));
        await Assert.ThrowsAsync<ArgumentException>(
            () => writer.WriteItemsAsync(
                [TestHelper.GetItem(new string('x', 37), "A", "a")]));

        Item item = TestHelper.GetItem(ITEM1, "A", "a");
        TestPart part = TestHelper.AddPart(item, "");
        await Assert.ThrowsAsync<ArgumentException>(
            () => writer.WritePartAsync(item, part));
    }
    #endregion

    #region WriteItem
    [Fact]
    public async Task WriteItem_NoParts_Ok()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        Item item = TestHelper.GetItem(ITEM1, "Alpha", "a", 3);
        item.GroupId = "g";

        await writer.WriteItemAsync(item);

        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM item WHERE id=$1 AND title='Alpha' AND " +
            "description='Description of Alpha' AND facet_id='default' AND " +
            "group_id='g' AND sort_key='a' AND flags=3 AND creator_id='zeus' " +
            "AND user_id='zeus' AND time_created='2024-01-01T00:00:00Z' AND " +
            "time_modified='2024-01-02T00:00:00Z'", TestHelper.DB_NAME, ITEM1));
        Assert.Equal(0, await CountPinsAsync());
    }

    [Fact]
    public async Task WriteItem_Parts_Ok()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();

        await writer.WriteItemAsync(GetItemWithParts());

        Assert.Equal(["category=lit", "tag=odd", "year=1200"],
            await GetPinsAsync(ITEM1));
        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM pin WHERE part_id=$1 AND " +
            "part_type_id='it.vedph.categories' AND part_role_id='fr.x'",
            TestHelper.DB_NAME, PART2));
        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM pin WHERE part_id=$1 AND name='tag' AND " +
            "part_role_id IS NULL AND time_indexed > now() - interval '1 hour'",
            TestHelper.DB_NAME, PART1));
    }

    [Fact]
    public async Task WriteItem_Existing_UpdatesAndReplacesPartPins()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        await writer.WriteItemAsync(GetItemWithParts());

        // rewrite the item with only one of its parts, changed
        Item item = TestHelper.GetItem(ITEM1, "Alpha changed", "z", 0);
        TestHelper.AddPart(item, PART1).AddPin("tag", "even");
        await writer.WriteItemAsync(item);

        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM item WHERE title='Alpha changed' AND " +
            "sort_key='z' AND flags=0"));
        // pins of PART1 replaced, pins of PART2 untouched
        Assert.Equal(["category=lit", "tag=even"], await GetPinsAsync(ITEM1));
    }

    [Fact]
    public async Task WriteItem_ExistingWithoutParts_KeepsPins()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        await writer.WriteItemAsync(GetItemWithParts());

        await writer.WriteItemAsync(TestHelper.GetItem(ITEM1, "B", "b"));

        Assert.Equal(3, (await GetPinsAsync(ITEM1)).Count);
        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM item WHERE title='B'"));
    }

    [Fact]
    public async Task WriteItem_Unchanged_NoNewRowVersion()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        Item item = TestHelper.GetItem(ITEM1, "A", "a");
        await writer.WriteItemAsync(item);
        string? xmin = (await TestHelper.ScalarAsync(
            "SELECT xmin::text FROM item WHERE id=$1", TestHelper.DB_NAME, ITEM1))
            ?.ToString();

        await writer.WriteItemAsync(item);

        Assert.Equal(xmin, (await TestHelper.ScalarAsync(
            "SELECT xmin::text FROM item WHERE id=$1", TestHelper.DB_NAME, ITEM1))
            ?.ToString());
    }

    [Fact]
    public async Task WriteItem_LongAndInvalidValues_Fitted()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        Item item = TestHelper.GetItem(ITEM1, new string('t', 600), "a");
        item.Description = new string('d', 1200);
        item.FacetId = new string('f', 200);
        item.CreatorId = null!;
        item.UserId = null!;
        item.SortKey = null!;
        TestHelper.AddPart(item, PART1, new string('p', 150), new string('r', 150))
            .AddPin(new string('n', 150), new string('v', 600))
            .AddPin("nul", "a\0b")
            .AddPin("null", null)
            .AddPin("", "no name")
            .AddPin(null, "no name");

        await writer.WriteItemAsync(item);

        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM item WHERE length(title)=500 AND " +
            "length(description)=1000 AND length(facet_id)=100 AND " +
            "creator_id IS NULL AND user_id IS NULL AND sort_key=''"));
        Assert.Equal(3, await CountPinsAsync());
        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM pin WHERE length(name)=100 AND " +
            "length(value)=500 AND length(part_type_id)=100 AND " +
            "length(part_role_id)=100"));
        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM pin WHERE name='nul' AND value='ab'"));
        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM pin WHERE name='null' AND value=''"));
    }

    [Fact]
    public async Task WriteItem_PinIdsFromItemAndPart()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        Item item = TestHelper.GetItem(ITEM1, "A", "a");
        TestPart part = TestHelper.AddPart(item, PART1);
        part.Pins.Add(new DataPin
        {
            ItemId = "other",
            PartId = "other",
            Name = "x",
            Value = "y"
        });

        await writer.WriteItemAsync(item);

        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM pin WHERE item_id=$1 AND part_id=$2",
            TestHelper.DB_NAME, ITEM1, PART1));
    }

    [Fact]
    public async Task WriteItem_LocalTimes_StoredAsUtc()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        Item item = TestHelper.GetItem(ITEM1, "A", "a");
        DateTime utc = new(2024, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        item.TimeCreated = utc.ToLocalTime();
        item.TimeModified = new DateTime(2024, 3, 1, 12, 0, 0);

        await writer.WriteItemAsync(item);

        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM item WHERE " +
            "time_created='2024-03-01T12:00:00Z' AND " +
            "time_modified='2024-03-01T12:00:00Z'"));
    }

    [Fact]
    public async Task WriteItem_ConcurrentSamePart_NoDuplicates()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        Item item = GetItemWithParts();
        IPart part = item.Parts[0];

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            TestHelper.GetWriter().WritePartAsync(item, part)));

        Assert.Equal(2, await CountPinsAsync(PART1));
    }
    #endregion

    #region WriteItems
    [Fact]
    public async Task WriteItems_Batches_Ok()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        List<IItem> items = [];
        for (int n = 1; n <= 250; n++)
        {
            Item item = TestHelper.GetItem($"{n:00000000}-0000-0000-0000-000000000000",
                "Item " + n, $"{n:000}");
            TestHelper.AddPart(item, $"{n:00000000}-1111-0000-0000-000000000000")
                .AddPin("n", n.ToString());
            items.Add(item);
        }
        RecordingProgress progress = new();

        await writer.WriteItemsAsync(items, progress);

        Assert.Equal(250, await TestHelper.CountAsync("SELECT COUNT(*) FROM item"));
        Assert.Equal(250, await CountPinsAsync());
        Assert.Equal([(100, 40), (200, 80), (250, 100)], progress.Reports);
    }

    [Fact]
    public async Task WriteItems_DuplicateItem_LastWins()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        Item first = TestHelper.GetItem(ITEM1, "First", "a");
        Item second = TestHelper.GetItem(ITEM1, "Second", "a");
        RecordingProgress progress = new();

        // enumerable without count: percent not reported
        await writer.WriteItemsAsync(new[] { first, second }.Select(i => i),
            progress);

        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM item WHERE title='Second'"));
        Assert.Equal([(1, 0), (2, 0)], progress.Reports);
    }

    [Fact]
    public async Task WriteItems_Empty_Nope()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        RecordingProgress progress = new();
        await writer.WriteItemsAsync([], progress);
        Assert.Empty(progress.Reports);
    }

    [Fact]
    public async Task WriteItems_Cancelled_Throws()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        using System.Threading.CancellationTokenSource cts = new();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => writer.WriteItemsAsync(
                [TestHelper.GetItem(ITEM1, "A", "a")], null, cts.Token));
    }
    #endregion

    #region WritePart
    [Fact]
    public async Task WritePart_NewItem_CreatesItem()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        Item item = GetItemWithParts();

        await writer.WritePartAsync(item, item.Parts[1]);

        Assert.Equal(1, await TestHelper.CountAsync("SELECT COUNT(*) FROM item"));
        Assert.Equal(["category=lit"], await GetPinsAsync(ITEM1));
    }

    [Fact]
    public async Task WritePart_Existing_ReplacesPinsAndUpdatesItem()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        await writer.WriteItemAsync(GetItemWithParts());

        Item item = TestHelper.GetItem(ITEM1, "Renamed", "a");
        TestPart part = TestHelper.AddPart(item, PART1).AddPin("tag", "x");
        await writer.WritePartAsync(item, part);

        Assert.Equal(["category=lit", "tag=x"], await GetPinsAsync(ITEM1));
        Assert.Equal(1, await TestHelper.CountAsync(
            "SELECT COUNT(*) FROM item WHERE title='Renamed'"));
    }

    [Fact]
    public async Task WritePart_NoPins_RemovesPins()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        Item item = GetItemWithParts();
        await writer.WriteItemAsync(item);

        ((TestPart)item.Parts[0]).Pins.Clear();
        await writer.WritePartAsync(item, item.Parts[0]);

        Assert.Equal(0, await CountPinsAsync(PART1));
        Assert.Equal(1, await CountPinsAsync(PART2));
    }
    #endregion

    #region Delete
    [Fact]
    public async Task DeleteItem_Existing_CascadesPins()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        await writer.WriteItemAsync(GetItemWithParts());
        Item other = TestHelper.GetItem(ITEM2, "B", "b");
        TestHelper.AddPart(other, "20000000-0000-0000-0000-000000000001")
            .AddPin("x", "y");
        await writer.WriteItemAsync(other);

        await writer.DeleteItemAsync(ITEM1);

        Assert.Equal(1, await TestHelper.CountAsync("SELECT COUNT(*) FROM item"));
        Assert.Equal(1, await CountPinsAsync());
    }

    [Fact]
    public async Task DeleteItem_NotExisting_Nope()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        await writer.WriteItemAsync(GetItemWithParts());

        await writer.DeleteItemAsync(ITEM2);

        Assert.Equal(1, await TestHelper.CountAsync("SELECT COUNT(*) FROM item"));
    }

    [Fact]
    public async Task DeletePart_Existing_Ok()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        await writer.WriteItemAsync(GetItemWithParts());

        await writer.DeletePartAsync(PART1);

        Assert.Equal(["category=lit"], await GetPinsAsync(ITEM1));
    }

    [Fact]
    public async Task DeletePart_NotExisting_Nope()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        await writer.WriteItemAsync(GetItemWithParts());

        await writer.DeletePartAsync("missing");

        Assert.Equal(3, await CountPinsAsync());
    }
    #endregion

    #region Clear and create
    [Fact]
    public async Task Clear_Ok()
    {
        PgSqlItemIndexWriter writer = await GetClearedWriterAsync();
        await writer.WriteItemAsync(GetItemWithParts());

        await writer.ClearAsync();

        Assert.Equal(0, await TestHelper.CountAsync("SELECT COUNT(*) FROM item"));
        Assert.Equal(0, await CountPinsAsync());
    }

    [Fact]
    public async Task Clear_NoDatabase_Nope()
    {
        const string db = "cadmus-index-pgsql-test-none";
        await TestHelper.DropDatabaseAsync(db);
        await TestHelper.GetWriter(db).ClearAsync();
        Assert.False(await PgSqlIndexSchema.ExistsAsync(
            TestHelper.GetConnectionString(db)));
    }

    [Fact]
    public async Task Clear_NoTables_Nope()
    {
        const string db = "cadmus-index-pgsql-test-empty";
        await TestHelper.DropDatabaseAsync(db);
        await TestHelper.ExecuteAsync($"CREATE DATABASE \"{db}\"", "postgres");
        try
        {
            await TestHelper.GetWriter(db).ClearAsync();
        }
        finally
        {
            await TestHelper.DropDatabaseAsync(db);
        }
    }

    [Fact]
    public async Task Write_NoDatabase_CreatesIt()
    {
        const string db = "cadmus-index-pgsql-test-new";
        await TestHelper.DropDatabaseAsync(db);
        try
        {
            PgSqlItemIndexWriter writer = TestHelper.GetWriter(db);
            writer.InitContext = "CREATE TABLE seeded (id int);";

            await writer.WriteItemAsync(GetItemWithParts());

            Assert.Equal(3, await TestHelper.CountAsync(
                "SELECT COUNT(*) FROM pin", db));
            Assert.Equal(0, await TestHelper.CountAsync(
                "SELECT COUNT(*) FROM seeded", db));

            // ensured once per process: a second write does not re-create
            await writer.CreateIndexAsync();
            await writer.DeletePartAsync(PART1);
            Assert.Equal(1, await TestHelper.CountAsync(
                "SELECT COUNT(*) FROM pin", db));
        }
        finally
        {
            await TestHelper.DropDatabaseAsync(db);
        }
    }

    [Fact]
    public async Task Write_InitFails_RetriesAndThrows()
    {
        const string db = "cadmus-index-pgsql-test-fail";
        await TestHelper.DropDatabaseAsync(db);
        try
        {
            PgSqlItemIndexWriter writer = TestHelper.GetWriter(db);
            writer.InitContext = "THIS IS NOT SQL;";

            await Assert.ThrowsAnyAsync<Exception>(
                () => writer.WriteItemAsync(TestHelper.GetItem(ITEM1, "A", "a")));
            Assert.False(await PgSqlIndexSchema.ExistsAsync(
                TestHelper.GetConnectionString(db)));

            // the failure was not cached: a fixed writer succeeds
            writer.InitContext = null;
            await writer.WriteItemAsync(TestHelper.GetItem(ITEM1, "A", "a"));
            Assert.Equal(1, await TestHelper.CountAsync(
                "SELECT COUNT(*) FROM item", db));
        }
        finally
        {
            await TestHelper.DropDatabaseAsync(db);
        }
    }
    #endregion
}

internal sealed class RecordingProgress : IProgress<ProgressReport>
{
    public List<(int Count, int Percent)> Reports { get; } = [];

    public void Report(ProgressReport value) =>
        Reports.Add((value.Count, value.Percent));
}

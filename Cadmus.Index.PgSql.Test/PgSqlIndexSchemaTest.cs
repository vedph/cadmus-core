using Cadmus.Core;
using Fusi.Tools.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Index.PgSql.Test;

[Collection(nameof(DbCollection))]
public sealed class PgSqlIndexSchemaTest
{
    private const string DB = "cadmus-index-pgsql-test-schema";

    private static string CS => TestHelper.GetConnectionString(DB);

    private static Task<List<string>> GetIndexesAsync() =>
        TestHelper.ListAsync("SELECT indexname FROM pg_indexes WHERE " +
            "schemaname='public' ORDER BY indexname", DB);

    private static readonly string[] _expectedIndexes =
    [
        "item_description_trgm_idx",
        "item_facet_id_idx",
        "item_group_id_idx",
        "item_pk",
        "item_sort_key_id_idx",
        "item_title_trgm_idx",
        "pin_item_id_idx",
        "pin_name_value_idx",
        "pin_part_id_idx",
        "pin_part_type_id_idx",
        "pin_pk",
        "pin_value_trgm_idx",
    ];

    [Fact]
    public void GetSql_Ok()
    {
        string sql = PgSqlIndexSchema.GetSql();
        Assert.Contains("CREATE TABLE IF NOT EXISTS item", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS pin", sql);
    }

    [Fact]
    public void GetDatabaseName_Ok()
    {
        Assert.Equal(DB, PgSqlIndexSchema.GetDatabaseName(CS));
        Assert.Throws<InvalidOperationException>(() =>
            PgSqlIndexSchema.GetDatabaseName("Server=localhost;"));
    }

    [Fact]
    public async Task NullArguments_Throw()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PgSqlIndexSchema.ApplyAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PgSqlIndexSchema.EnsureDatabaseAsync(null!));
    }

    [Fact]
    public async Task EnsureDatabase_NewThenExisting_Ok()
    {
        await TestHelper.DropDatabaseAsync(DB);
        try
        {
            Assert.False(await PgSqlIndexSchema.ExistsAsync(CS));

            Assert.True(await PgSqlIndexSchema.EnsureDatabaseAsync(CS,
                "INSERT INTO item VALUES ('x','t','d','f',NULL,'s',0,now()," +
                "NULL,now(),NULL);"));
            Assert.True(await PgSqlIndexSchema.ExistsAsync(CS));
            Assert.Equal(_expectedIndexes, await GetIndexesAsync());
            Assert.Equal(1, await TestHelper.CountAsync(
                "SELECT COUNT(*) FROM item", DB));

            // existing: init SQL not executed again, schema reapplied
            Assert.False(await PgSqlIndexSchema.EnsureDatabaseAsync(CS,
                "INSERT INTO item VALUES ('y','t','d','f',NULL,'s',0,now()," +
                "NULL,now(),NULL);"));
            Assert.Equal(1, await TestHelper.CountAsync(
                "SELECT COUNT(*) FROM item", DB));
            Assert.Equal(_expectedIndexes, await GetIndexesAsync());
        }
        finally
        {
            await TestHelper.DropDatabaseAsync(DB);
        }
    }

    [Fact]
    public async Task EnsureDatabase_InitFails_Dropped()
    {
        await TestHelper.DropDatabaseAsync(DB);
        await Assert.ThrowsAnyAsync<Exception>(
            () => PgSqlIndexSchema.EnsureDatabaseAsync(CS, "NOT SQL"));
        Assert.False(await PgSqlIndexSchema.ExistsAsync(CS));
    }

    [Fact]
    public async Task Apply_Concurrent_Ok()
    {
        await TestHelper.DropDatabaseAsync(DB);
        try
        {
            await PgSqlIndexSchema.EnsureDatabaseAsync(CS);
            await Task.WhenAll(
                PgSqlIndexSchema.ApplyAsync(CS),
                PgSqlIndexSchema.ApplyAsync(CS),
                PgSqlIndexSchema.ApplyAsync(CS));
            Assert.Equal(_expectedIndexes, await GetIndexesAsync());
        }
        finally
        {
            await TestHelper.DropDatabaseAsync(DB);
        }
    }

    [Fact]
    public async Task Apply_LegacyDatabase_Upgraded()
    {
        await TestHelper.DropDatabaseAsync(DB);
        try
        {
            // a database created by v16 with its data
            await TestHelper.ExecuteAsync($"CREATE DATABASE \"{DB}\"", "postgres");
            await TestHelper.ExecuteAsync(
                TestHelper.LoadResourceText("LegacySchema.pgsql"), DB);
            await TestHelper.ExecuteAsync(
                "INSERT INTO item VALUES ('a','Alpha','d','default',NULL," +
                "NULL,1,now(),'zeus',now(),'zeus');" +
                "INSERT INTO pin (item_id,part_id,part_type_id,part_role_id," +
                "name,value,time_indexed) VALUES ('a','p','t',NULL,'eid'," +
                "'x_y',now());", DB);

            await PgSqlIndexSchema.ApplyAsync(CS);

            // column types
            Assert.Equal(["item.id=character varying",
                "pin.id=bigint",
                "pin.item_id=character varying",
                "pin.part_id=character varying"],
                await TestHelper.ListAsync(
                    "SELECT table_name || '.' || column_name || '=' || data_type " +
                    "FROM information_schema.columns WHERE table_schema='public' " +
                    "AND column_name IN ('id','item_id','part_id') " +
                    "ORDER BY 1", DB));
            Assert.Equal("bigint", await TestHelper.ScalarAsync(
                "SELECT data_type FROM information_schema.sequences " +
                "WHERE sequence_name='pin_id_seq'", DB));
            Assert.Equal("NO", await TestHelper.ScalarAsync(
                "SELECT is_nullable FROM information_schema.columns WHERE " +
                "table_name='item' AND column_name='sort_key'", DB));
            Assert.Equal(1, await TestHelper.CountAsync(
                "SELECT COUNT(*) FROM information_schema.table_constraints " +
                "WHERE constraint_name='pin_fk'", DB));

            // indexes: legacy replaced
            Assert.Equal(_expectedIndexes, await GetIndexesAsync());

            // data preserved, IDs trimmed
            Assert.Equal(1, await TestHelper.CountAsync(
                "SELECT COUNT(*) FROM item WHERE id='a' AND sort_key=''", DB));
            Assert.Equal(1, await TestHelper.CountAsync(
                "SELECT COUNT(*) FROM pin WHERE item_id='a' AND part_id='p'", DB));

            // the upgraded database works
            PgSqlItemIndexWriter.ResetEnsuredDatabases();
            PgSqlItemIndexWriter writer = TestHelper.GetWriter(DB);
            Item item = TestHelper.GetItem("b", "Beta", "b");
            TestHelper.AddPart(item, "q").AddPin("eid", "x_z");
            await writer.WriteItemAsync(item);

            DataPage<ItemInfo> page = await TestHelper.GetReader(DB)
                .SearchItemsAsync("[eid^=x_]", new PagingOptions());
            Assert.Equal(2, page.Total);
            Assert.Equal("a", page.Items[0].Id);

            // applying again changes nothing
            await PgSqlIndexSchema.ApplyAsync(CS);
            Assert.Equal(_expectedIndexes, await GetIndexesAsync());
            Assert.Equal(2, await TestHelper.CountAsync(
                "SELECT COUNT(*) FROM pin", DB));
        }
        finally
        {
            await TestHelper.DropDatabaseAsync(DB);
        }
    }
}

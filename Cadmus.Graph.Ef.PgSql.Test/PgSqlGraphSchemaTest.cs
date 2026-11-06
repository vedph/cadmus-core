using Cadmus.Graph.Ef.Test;
using Fusi.DbManager.PgSql;
using Npgsql;
using Xunit;

namespace Cadmus.Graph.Ef.PgSql.Test;

[Collection(nameof(NonParallelResourceCollection))]
public sealed class PgSqlGraphSchemaTest
{
    private const string CST_TEMPLATE =
        "Server=localhost;Database={0};User Id=postgres;Password=postgres;" +
        "Include Error Detail=True";
    private const string DB_NAME = "cadmus-graph-schema-test";

    private static string ConnectionString =>
        string.Format(CST_TEMPLATE, DB_NAME);

    private static PgSqlDbManager CreateDatabase(string schema)
    {
        PgSqlDbManager manager = new(CST_TEMPLATE);
        if (manager.Exists(DB_NAME)) manager.RemoveDatabase(DB_NAME);
        manager.CreateDatabase(DB_NAME, schema, null);
        return manager;
    }

    private static async Task ExecuteAsync(string sql)
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand cmd = new(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<string>> GetIndexesAsync()
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand cmd = new(
            "SELECT indexname FROM pg_indexes WHERE schemaname='public'",
            connection);
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        HashSet<string> names = [];
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        return names;
    }

    // the indexes and procedures as created by versions before 17.1.0
    private const string LEGACY_UPDATE = """
        DROP INDEX IF EXISTS node_label_trgm_idx;
        DROP INDEX IF EXISTS uri_lookup_uri_trgm_idx;
        DROP INDEX IF EXISTS triple_o_id_p_id_idx;
        DROP INDEX IF EXISTS node_class_class_id_idx;
        DROP INDEX IF EXISTS mapping_link_child_id_idx;
        DROP INDEX IF EXISTS uri_lookup_uri_lower_idx;
        CREATE INDEX triple_o_lit_ix_idx ON triple USING btree (o_lit_ix);
        CREATE INDEX triple_o_id_idx ON triple (o_id);
        DROP PROCEDURE populate_node_class;
        """;

    [Fact]
    public async Task ApplyAsync_NewDatabase_Idempotent()
    {
        PgSqlDbManager manager = CreateDatabase(PgSqlGraphSchema.GetSql());
        try
        {
            HashSet<string> before = await GetIndexesAsync();

            await PgSqlGraphSchema.ApplyAsync(ConnectionString);
            await PgSqlGraphSchema.ApplyAsync(ConnectionString);

            Assert.Equal(before, await GetIndexesAsync());
            Assert.Contains("uri_lookup_uri_lower_idx", before);
            Assert.Contains("triple_o_id_p_id_idx", before);
            Assert.DoesNotContain("triple_o_lit_ix_idx", before);
        }
        finally
        {
            manager.RemoveDatabase(DB_NAME);
        }
    }

    [Fact]
    public async Task ApplyAsync_LegacyDatabase_Upgraded()
    {
        PgSqlDbManager manager = CreateDatabase(PgSqlGraphSchema.GetSql());
        try
        {
            await ExecuteAsync(LEGACY_UPDATE);

            await PgSqlGraphSchema.ApplyAsync(ConnectionString);

            HashSet<string> indexes = await GetIndexesAsync();
            Assert.Contains("node_label_trgm_idx", indexes);
            Assert.Contains("uri_lookup_uri_trgm_idx", indexes);
            Assert.Contains("uri_lookup_uri_lower_idx", indexes);
            Assert.Contains("triple_o_id_p_id_idx", indexes);
            Assert.Contains("node_class_class_id_idx", indexes);
            Assert.Contains("mapping_link_child_id_idx", indexes);
            Assert.DoesNotContain("triple_o_lit_ix_idx", indexes);
            Assert.DoesNotContain("triple_o_id_idx", indexes);

            // the procedure exists again
            EfPgSqlGraphRepository repository = new();
            repository.Configure(new EfGraphRepositoryOptions
            {
                ConnectionString = ConnectionString
            });
            await repository.UpdateNodeClassesAsync(default);
        }
        finally
        {
            manager.RemoveDatabase(DB_NAME);
        }
    }

    [Fact]
    public async Task ApplyAsync_LegacyDuplicateUris_NoUniqueIndex()
    {
        PgSqlDbManager manager = CreateDatabase(PgSqlGraphSchema.GetSql());
        try
        {
            await ExecuteAsync(LEGACY_UPDATE + """

                INSERT INTO uri_lookup(uri) VALUES('x:Dup');
                INSERT INTO uri_lookup(uri) VALUES('x:dup');
                """);

            // this must not fail
            await PgSqlGraphSchema.ApplyAsync(ConnectionString);

            HashSet<string> indexes = await GetIndexesAsync();
            Assert.DoesNotContain("uri_lookup_uri_lower_idx", indexes);
            Assert.Contains("uri_lookup_uri_lower_nu_idx", indexes);

            // lookups still work, picking the oldest entry
            EfPgSqlGraphRepository repository = new();
            repository.Configure(new EfGraphRepositoryOptions
            {
                ConnectionString = ConnectionString
            });
            int id = repository.LookupId("x:DUP");
            Assert.True(id > 0);
            Assert.Equal(id, repository.AddUri("x:dup"));
        }
        finally
        {
            manager.RemoveDatabase(DB_NAME);
        }
    }

    [Fact]
    public void CreateStore_Existing_SchemaUpdated()
    {
        PgSqlDbManager manager = CreateDatabase(PgSqlGraphSchema.GetSql());
        try
        {
            ExecuteAsync(LEGACY_UPDATE).GetAwaiter().GetResult();
            EfPgSqlGraphRepository repository = new();
            repository.Configure(new EfGraphRepositoryOptions
            {
                ConnectionString = ConnectionString
            });

            Assert.False(repository.CreateStore());

            Assert.Contains("node_label_trgm_idx",
                GetIndexesAsync().GetAwaiter().GetResult());
        }
        finally
        {
            manager.RemoveDatabase(DB_NAME);
        }
    }
}

using Cadmus.Core;
using Cadmus.Core.Config;
using Fusi.Tools.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Index.PgSql.Test;

/// <summary>
/// Collection for tests using the database, which cannot run in parallel.
/// </summary>
[CollectionDefinition(nameof(DbCollection), DisableParallelization = true)]
public sealed class DbCollection
{
}

/// <summary>
/// Test part with configurable pins.
/// </summary>
[Tag("it.vedph.test")]
internal sealed class TestPart : PartBase
{
    public List<DataPin> Pins { get; } = [];

    public TestPart(string? typeId = null)
    {
        if (typeId != null) TypeId = typeId;
    }

    public TestPart AddPin(string? name, string? value, string? roleId = null)
    {
        Pins.Add(new DataPin
        {
            ItemId = ItemId,
            PartId = Id,
            RoleId = roleId ?? RoleId,
            Name = name,
            Value = value
        });
        return this;
    }

    public override IEnumerable<DataPin> GetDataPins(IItem? item = null) =>
        Pins;

    public override IList<DataPinDefinition> GetDataPinDefinitions() => [];
}

internal static class TestHelper
{
    public const string CST = "Server=localhost;Database={0};" +
        "User Id=postgres;Password=postgres;Include Error Detail=True";

    public const string DB_NAME = "cadmus-index-pgsql-test";

    public static string GetConnectionString(string db = DB_NAME) =>
        string.Format(CST, db);

    public static string LoadResourceText(string name)
    {
        using StreamReader reader = new(Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"Cadmus.Index.PgSql.Test.Assets.{name}")!,
            Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static async Task DropDatabaseAsync(string db)
    {
        await using NpgsqlConnection connection = new(GetConnectionString("postgres"));
        await connection.OpenAsync();
        await using NpgsqlCommand cmd = new(
            $"DROP DATABASE IF EXISTS \"{db}\" WITH (FORCE)", connection);
        await cmd.ExecuteNonQueryAsync();
        NpgsqlConnection.ClearAllPools();
        PgSqlItemIndexWriter.ResetEnsuredDatabases();
    }

    public static async Task<object?> ScalarAsync(string sql,
        string db = DB_NAME, params object[] args)
    {
        await using NpgsqlConnection connection = new(GetConnectionString(db));
        await connection.OpenAsync();
        await using NpgsqlCommand cmd = new(sql, connection);
        foreach (object arg in args)
            cmd.Parameters.Add(new NpgsqlParameter { Value = arg });
        return await cmd.ExecuteScalarAsync();
    }

    public static async Task<long> CountAsync(string sql, string db = DB_NAME,
        params object[] args) =>
        Convert.ToInt64(await ScalarAsync(sql, db, args));

    public static async Task ExecuteAsync(string sql, string db = DB_NAME)
    {
        await using NpgsqlConnection connection = new(GetConnectionString(db));
        await connection.OpenAsync();
        await using NpgsqlCommand cmd = new(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<List<string>> ListAsync(string sql,
        string db = DB_NAME, params object[] args)
    {
        await using NpgsqlConnection connection = new(GetConnectionString(db));
        await connection.OpenAsync();
        await using NpgsqlCommand cmd = new(sql, connection);
        foreach (object arg in args)
            cmd.Parameters.Add(new NpgsqlParameter { Value = arg });
        List<string> results = [];
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(Convert.ToString(reader.GetValue(0))!);
        return results;
    }

    public static PgSqlItemIndexWriter GetWriter(string db = DB_NAME)
    {
        PgSqlItemIndexWriter writer = new();
        writer.Configure(new PgSqlIndexOptions
        {
            ConnectionString = GetConnectionString(db)
        });
        return writer;
    }

    public static PgSqlItemIndexReader GetReader(string db = DB_NAME,
        IList<FlagDefinition>? flags = null)
    {
        PgSqlItemIndexReader reader = new();
        reader.Configure(new PgSqlIndexOptions
        {
            ConnectionString = GetConnectionString(db),
            FlagDefinitions = flags
        });
        return reader;
    }

    public static Item GetItem(string id, string title, string sortKey,
        int flags = 0) => new()
    {
        Id = id,
        Title = title,
        Description = "Description of " + title,
        FacetId = "default",
        SortKey = sortKey,
        Flags = flags,
        CreatorId = "zeus",
        UserId = "zeus",
        TimeCreated = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        TimeModified = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
    };

    public static TestPart AddPart(IItem item, string partId,
        string typeId = "it.vedph.note", string? roleId = null)
    {
        TestPart part = new(typeId)
        {
            Id = partId,
            ItemId = item.Id,
            RoleId = roleId,
            CreatorId = "zeus",
            UserId = "zeus"
        };
        item.Parts.Add(part);
        return part;
    }
}

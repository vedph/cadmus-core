using Cadmus.Core;
using Cadmus.Index.Config;
using Cadmus.Index.PgSql;
using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Fusi.Tools.Data;
using Microsoft.Extensions.Hosting;
using Npgsql;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Index.Ef.PgSql.Test;

/// <summary>
/// Tests for the deprecated compatibility shim: legacy profiles and code
/// must keep working.
/// </summary>
public sealed class ItemIndexFactoryTest
{
    private const string DB = "cadmus-index-efpg-test";
    private static readonly string CS = "Server=localhost;Database=" + DB +
        ";User Id=postgres;Password=postgres;Include Error Detail=True";

    // like the factory providers of legacy API and CLI projects
    private static ItemIndexFactory GetFactory() => new(
        new HostBuilder()
            .ConfigureServices((_, services) =>
                ItemIndexFactory.ConfigureServices(services,
                    typeof(EfPgSqlItemIndexWriter).Assembly))
            .AddInMemoryJson(LoadResourceText("Config.json"))
            .Build(), CS);

    private static string LoadResourceText(string name)
    {
        using StreamReader reader = new(
            Assembly.GetExecutingAssembly().GetManifestResourceStream(
                $"Cadmus.Index.Ef.PgSql.Test.Assets.{name}")!,
            Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void LegacyTags_Resolved()
    {
        ItemIndexFactory factory = GetFactory();

        EfPgSqlItemIndexWriter writer = Assert.IsType<EfPgSqlItemIndexWriter>(
            factory.GetItemIndexWriter());
        Assert.Equal(CS, writer.ConnectionString);

        EfPgSqlItemIndexReader reader = Assert.IsType<EfPgSqlItemIndexReader>(
            factory.GetItemIndexReader());
        Assert.Equal(CS, reader.ConnectionString);
    }

    [Fact]
    public void GetPgSqlSchema_Ok()
    {
        Assert.Equal(PgSqlIndexSchema.GetSql(),
            EfPgSqlItemIndexWriter.GetPgSqlSchema());
    }

    [Fact]
    public void LegacyOptions_Ok()
    {
        EfPgSqlItemIndexWriter writer = new();
        writer.Configure(new EfIndexRepositoryOptions { ConnectionString = CS });
        Assert.Equal(CS, writer.ConnectionString);
    }

    [Fact]
    public async Task LegacyProfile_WritesAndSearches()
    {
        ItemIndexFactory factory = GetFactory();
        IItemIndexWriter writer = factory.GetItemIndexWriter()!;
        await writer.CreateIndexAsync();
        await writer.ClearAsync();

        Item item = new()
        {
            Id = "00000000-0000-0000-0000-000000000001",
            Title = "Alpha",
            Description = "A",
            FacetId = "default",
            SortKey = "a",
            Flags = 1,
            CreatorId = "zeus",
            UserId = "zeus"
        };
        // legacy synchronous-style call, as in legacy code
        await writer.WriteItem(item);

        // flags labels come from the profile
        DataPage<ItemInfo> page = await factory.GetItemIndexReader()!
            .SearchItemsAsync("[flags:to revise]", new PagingOptions());
        Assert.Equal(item.Id, Assert.Single(page.Items).Id);

        await using NpgsqlConnection connection = new(
            CS.Replace(DB, "postgres"));
        await connection.OpenAsync();
        await using NpgsqlCommand cmd = new(
            $"DROP DATABASE IF EXISTS \"{DB}\" WITH (FORCE)", connection);
        await cmd.ExecuteNonQueryAsync();
        PgSqlItemIndexWriter.ResetEnsuredDatabases();
    }
}

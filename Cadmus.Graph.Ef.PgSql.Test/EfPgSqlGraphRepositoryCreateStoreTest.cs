using System;
using Cadmus.Graph.Ef.Test;
using Fusi.DbManager.PgSql;
using Xunit;

namespace Cadmus.Graph.Ef.PgSql.Test;

[Collection(nameof(NonParallelResourceCollection))]
public sealed class EfPgSqlGraphRepositoryCreateStoreTest
{
    private const string CST_TEMPLATE =
        "Server=localhost;Database={0};User Id=postgres;Password=postgres;" +
        "Include Error Detail=True";
    private const string DB_NAME = "cadmus-graph-createstore-test";

    [Fact]
    public void CreateStore_NoConnectionString_Throws()
    {
        EfPgSqlGraphRepository repository = new();

        Assert.Throws<InvalidOperationException>(
            () => repository.CreateStore());
    }

    [Fact]
    public void CreateStore_NoDatabaseInConnectionString_Throws()
    {
        EfPgSqlGraphRepository repository = new();
        repository.Configure(new EfGraphRepositoryOptions
        {
            ConnectionString = "Server=localhost;User Id=postgres;" +
                "Password=postgres"
        });

        Assert.Throws<InvalidOperationException>(
            () => repository.CreateStore());
    }

    [Fact]
    public void CreateStore_NotExisting_CreatesAndReturnsTrue_ThenFalse()
    {
        PgSqlDbManager manager = new(CST_TEMPLATE);
        if (manager.Exists(DB_NAME)) manager.RemoveDatabase(DB_NAME);

        try
        {
            EfPgSqlGraphRepository repository = new();
            repository.Configure(new EfGraphRepositoryOptions
            {
                ConnectionString = string.Format(CST_TEMPLATE, DB_NAME)
            });

            bool created = repository.CreateStore();
            Assert.True(created);
            Assert.True(manager.Exists(DB_NAME));

            bool createdAgain = repository.CreateStore();
            Assert.False(createdAgain);
        }
        finally
        {
            if (manager.Exists(DB_NAME)) manager.RemoveDatabase(DB_NAME);
        }
    }
}

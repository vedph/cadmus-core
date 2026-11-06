using Cadmus.Graph.Ef.Test;
using Fusi.DbManager.PgSql;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cadmus.Graph.Ef.PgSql.Test;

/// <summary>
/// A repository using only the default implementations of the base class
/// (no locks, Entity Framework node insertions), as a database-agnostic
/// derived class would do.
/// </summary>
internal sealed class PlainEfGraphRepository : EfGraphRepository,
    IGraphRepository
{
    protected override CadmusGraphDbContext GetContext()
    {
        DbContextOptionsBuilder<CadmusGraphDbContext> builder = new();
        builder.UseNpgsql(ConnectionString);
        return new CadmusGraphDbContext(builder.Options);
    }

    protected override string BuildRegexMatch(string field, string pattern)
        => throw new NotSupportedException();

    public bool CreateStore(object? payload = null)
        => throw new NotSupportedException();
}

[Collection(nameof(NonParallelResourceCollection))]
public sealed class EfGraphRepositoryBaseTest
{
    private const string CST_TEMPLATE =
        "Server=localhost;Database={0};User Id=postgres;Password=postgres;" +
        "Include Error Detail=True";
    private const string DB_NAME = "cadmus-graph-base-test";
    private const string SRC = "aaaaaaaa-0000-4000-8000-000000000001";

    private static PlainEfGraphRepository GetRepository()
    {
        PgSqlDbManager manager = new(CST_TEMPLATE);
        if (manager.Exists(DB_NAME)) manager.RemoveDatabase(DB_NAME);
        manager.CreateDatabase(DB_NAME, PgSqlGraphSchema.GetSql(), null);

        PlainEfGraphRepository repository = new();
        repository.Configure(new EfGraphRepositoryOptions
        {
            ConnectionString = string.Format(CST_TEMPLATE, DB_NAME)
        });
        return repository;
    }

    [Fact]
    public void UpdateAndDelete_DefaultImplementations_Ok()
    {
        PlainEfGraphRepository repository = GetRepository();
        try
        {
            GraphSet set = new();
            set.SourceIds.Add(SRC);
            set.Nodes.Add(new UriNode
            {
                Uri = "x:event",
                Sid = SRC + "/e",
                SourceType = Node.SOURCE_PART,
                Label = "event"
            });
            set.Triples.Add(new UriTriple
            {
                SubjectUri = "x:event",
                PredicateUri = "rdf:type",
                ObjectUri = "x:birth",
                Sid = SRC + "/e"
            });

            repository.UpdateGraph(set);
            Assert.Equal("event", repository.GetNodeByUri("x:event")!.Label);
            Assert.Equal(1, repository.GetNodes(new NodeFilter
            { ClassIds = [repository.LookupId("x:birth")] }).Total);
            Assert.StartsWith("x:ts", repository.BuildUid("x:ts##", SRC));

            repository.DeleteGraphSet(SRC);
            Assert.Null(repository.GetNodeByUri("x:event"));
            Assert.Equal(0, repository.GetTriples(new TripleFilter()).Total);
        }
        finally
        {
            new PgSqlDbManager(CST_TEMPLATE).RemoveDatabase(DB_NAME);
        }
    }

    [Fact]
    public void GetContext_NoConnectionString_Throws()
    {
        EfPgSqlGraphRepository repository = new();

        Assert.Throws<InvalidOperationException>(
            () => repository.GetNode(1));
    }
}

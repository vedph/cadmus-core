using Cadmus.Mongo;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cadmus.Export.Test;

[Collection(nameof(NonParallelResourceCollection))]
public sealed class MongoItemIdCollectorTest
{
    private const string DB_NAME = "cadmus-test";
    private readonly MongoClient _client;

    public MongoItemIdCollectorTest()
    {
        _client = new MongoClient(TestHelper.CS);
    }

    private void InitDatabase(int count)
    {
        // camel case everything:
        // https://stackoverflow.com/questions/19521626/mongodb-convention-packs/19521784#19521784
        ConventionPack pack = new()
        {
            new CamelCaseElementNameConvention()
        };
        ConventionRegistry.Register("camel case", pack, _ => true);

        _client.DropDatabase(DB_NAME);
        IMongoDatabase db = _client.GetDatabase(DB_NAME);

        for (int i = 0; i < count; i++)
        {
            MongoItem item = new()
            {
                Title = $"Item {i + 1}",
                Description = "Description",
                CreatorId = "zeus",
                UserId = "zeus",
                FacetId = "text",
                SortKey = $"item{i+1:000}"
            };
            db.GetCollection<MongoItem>(MongoItem.COLLECTION).InsertOne(item);
        }
    }

    private static MongoItemIdCollector GetCollector()
    {
        MongoItemIdCollector collector = new();
        collector.Configure(new MongoItemIdCollectorOptions
        {
            ConnectionString = TestHelper.CS,
            FacetId = "text"
        });
        return collector;
    }

    [Fact]
    public void Collect_Empty_Empty()
    {
        InitDatabase(0);
        MongoItemIdCollector collector = GetCollector();

        IList<string> ids = collector.GetIds().ToList();

        Assert.Empty(ids);
    }

    [Fact]
    public void Collect_NotEmpty_Ok()
    {
        InitDatabase(3);
        MongoItemIdCollector collector = GetCollector();

        IList<string> ids = collector.GetIds().ToList();

        Assert.Equal(3, ids.Count);
    }
}

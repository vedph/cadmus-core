using System.Collections.Generic;
using System.Linq;
using Cadmus.Core.Config;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Cadmus.Mongo.Test;

[CollectionDefinition(nameof(NonParallelManagerResourceCollection),
    DisableParallelization = true)]
public class NonParallelManagerResourceCollection { }

[Collection(nameof(NonParallelManagerResourceCollection))]
public sealed class MongoDatabaseManagerTest
{
    private const string DB_NAME = "cadmus-test-manager";
    private const string CONNECTION = "mongodb://localhost:27017/" + DB_NAME;

    private static readonly MongoClient _client = new(CONNECTION);

    private static DataProfile GetProfile()
    {
        Thesaurus thesaurus = new("colors@en");
        thesaurus.AddEntry(new ThesaurusEntry("r", "red"));
        return new DataProfile
        {
            Facets =
            [
                new FacetDefinition { Id = "default", Label = "Default" }
            ],
            Flags =
            [
                new FlagDefinition { Id = 1, Label = "Flag" }
            ],
            Thesauri = [thesaurus]
        };
    }

    private static MongoDatabaseManager Reset()
    {
        _client.DropDatabase(DB_NAME);
        return new MongoDatabaseManager();
    }

    private static HashSet<string> GetIndexNames(string collection) =>
        [.. _client.GetDatabase(DB_NAME)
            .GetCollection<BsonDocument>(collection)
            .Indexes.List().ToList()
            .Select(d => d["name"].AsString)];

    [Fact]
    public void CreateDatabase_Ok()
    {
        MongoDatabaseManager manager = Reset();
        Assert.False(manager.DatabaseExists(CONNECTION));

        manager.CreateDatabase(CONNECTION, GetProfile());

        Assert.True(manager.DatabaseExists(CONNECTION));
        IMongoDatabase db = _client.GetDatabase(DB_NAME);
        Assert.Equal(1, db.GetCollection<BsonDocument>(
            MongoFacetDefinition.COLLECTION).CountDocuments(
            FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(1, db.GetCollection<BsonDocument>(
            MongoFlagDefinition.COLLECTION).CountDocuments(
            FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(1, db.GetCollection<BsonDocument>(
            MongoThesaurus.COLLECTION).CountDocuments(
            FilterDefinition<BsonDocument>.Empty));

        // indexes for the main queries
        HashSet<string> items = GetIndexNames(MongoItem.COLLECTION);
        Assert.Contains("sortKey_1__id_1", items);
        Assert.Contains("groupId_1", items);
        Assert.Contains("hierarchy_parent_tag",
            GetIndexNames(MongoPart.COLLECTION));
        Assert.Contains("referenceId_1_timeModified_-1",
            GetIndexNames(MongoHistoryPart.COLLECTION));
    }

    [Fact]
    public void EnsureIndexes_ExistingDatabase_AddsMissingIdempotently()
    {
        MongoDatabaseManager manager = Reset();
        // a legacy database with just the default ID index
        _client.GetDatabase(DB_NAME).GetCollection<BsonDocument>(
            MongoItem.COLLECTION).InsertOne(new BsonDocument("_id", "x"));
        Assert.Equal(["_id_"], GetIndexNames(MongoItem.COLLECTION));

        manager.EnsureIndexes(CONNECTION);
        HashSet<string> first = GetIndexNames(MongoItem.COLLECTION);
        manager.EnsureIndexes(CONNECTION);

        Assert.Contains("sortKey_1__id_1", first);
        Assert.Equal(first, GetIndexNames(MongoItem.COLLECTION));
        // data is untouched
        Assert.Equal(1, _client.GetDatabase(DB_NAME)
            .GetCollection<BsonDocument>(MongoItem.COLLECTION)
            .CountDocuments(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public void ClearDatabase_Ok()
    {
        MongoDatabaseManager manager = Reset();
        manager.CreateDatabase(CONNECTION, GetProfile());

        manager.ClearDatabase(CONNECTION);

        Assert.True(manager.DatabaseExists(CONNECTION));
        Assert.Equal(0, _client.GetDatabase(DB_NAME)
            .GetCollection<BsonDocument>(MongoFacetDefinition.COLLECTION)
            .CountDocuments(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public void DeleteDatabase_Ok()
    {
        MongoDatabaseManager manager = Reset();
        manager.CreateDatabase(CONNECTION, GetProfile());

        manager.DeleteDatabase(CONNECTION);

        Assert.False(manager.DatabaseExists(CONNECTION));
    }
}

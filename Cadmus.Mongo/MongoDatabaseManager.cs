using System;
using System.Collections.Generic;
using System.Linq;
using Cadmus.Core.Storage;
using Cadmus.Core.Config;
using MongoDB.Driver;

namespace Cadmus.Mongo;

/// <summary>
/// MongoDB-based database manager.
/// </summary>
public sealed class MongoDatabaseManager : MongoConsumerBase, IDatabaseManager
{
    private static CreateIndexModel<T> Index<T>(IndexKeysDefinition<T> keys,
        CreateIndexOptions<T>? options = null) => new(keys, options);

    private static void CreateIndexes(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        // creating an index which already exists with the same keys and
        // options does nothing, so this can be run on existing databases

        // items
        IndexKeysDefinitionBuilder<MongoItem> itemKeys =
            Builders<MongoItem>.IndexKeys;
        database.GetCollection<MongoItem>(MongoItem.COLLECTION)
            .Indexes.CreateMany(
            [
                Index(itemKeys.Ascending(i => i.Title)),
                Index(itemKeys.Ascending(i => i.Description)),
                Index(itemKeys.Ascending(i => i.FacetId)),
                Index(itemKeys.Ascending(i => i.SortKey)),
                Index(itemKeys.Ascending(i => i.Flags)),
                // items are paged by sort key and ID
                Index(itemKeys.Ascending(i => i.SortKey)
                    .Ascending(i => i.Id)),
                // group IDs and group layers
                Index(itemKeys.Ascending(i => i.GroupId)),
            ]);

        // parts
        IndexKeysDefinitionBuilder<MongoPart> partKeys =
            Builders<MongoPart>.IndexKeys;
        database.GetCollection<MongoPart>(MongoPart.COLLECTION)
            .Indexes.CreateMany(
            [
                Index(partKeys.Ascending(p => p.ItemId)),
                Index(partKeys.Ascending(p => p.TypeId)),
                Index(partKeys.Ascending(p => p.RoleId)),
                // hierarchy items browser: children of a parent
                Index(Builders<MongoPart>.IndexKeys
                    .Ascending("content.parentId")
                    .Ascending("content.tag"),
                    new CreateIndexOptions<MongoPart>
                    {
                        Name = "hierarchy_parent_tag",
                        PartialFilterExpression = Builders<MongoPart>.Filter
                            .Eq(p => p.TypeId,
                                MongoHierarchyItemBrowser.HIERARCHY_PART_TYPE_ID)
                    }),
            ]);

        // history-items
        IndexKeysDefinitionBuilder<MongoHistoryItem> historyItemKeys =
            Builders<MongoHistoryItem>.IndexKeys;
        database.GetCollection<MongoHistoryItem>(MongoHistoryItem.COLLECTION)
            .Indexes.CreateMany(
            [
                Index(historyItemKeys.Ascending(i => i.Title)),
                Index(historyItemKeys.Ascending(i => i.Description)),
                Index(historyItemKeys.Ascending(i => i.FacetId)),
                Index(historyItemKeys.Ascending(i => i.SortKey)),
                Index(historyItemKeys.Ascending(i => i.Flags)),
                Index(historyItemKeys.Ascending(i => i.TimeModified)),
                Index(historyItemKeys.Ascending(i => i.UserId)),
                Index(historyItemKeys.Ascending(i => i.ReferenceId)),
                Index(historyItemKeys.Ascending(i => i.Status)),
            ]);

        // history-parts
        IndexKeysDefinitionBuilder<MongoHistoryPart> historyPartKeys =
            Builders<MongoHistoryPart>.IndexKeys;
        database.GetCollection<MongoHistoryPart>(MongoHistoryPart.COLLECTION)
            .Indexes.CreateMany(
            [
                Index(historyPartKeys.Ascending(p => p.ItemId)),
                Index(historyPartKeys.Ascending(p => p.TypeId)),
                Index(historyPartKeys.Ascending(p => p.RoleId)),
                Index(historyPartKeys.Ascending(p => p.TimeModified)),
                Index(historyPartKeys.Ascending(p => p.UserId)),
                Index(historyPartKeys.Ascending(p => p.ReferenceId)),
                Index(historyPartKeys.Ascending(p => p.Status)),
                // versions of a part from the newest (layer part checks)
                Index(historyPartKeys.Ascending(p => p.ReferenceId)
                    .Descending(p => p.TimeModified)),
            ]);
    }

    /// <summary>
    /// Ensures that the database with the specified source has all the
    /// indexes used by Cadmus, creating those which are missing. Use this
    /// to upgrade a database created by a previous version. Note that on
    /// big collections creating an index may take some time.
    /// </summary>
    /// <param name="source">The database source.</param>
    /// <exception cref="ArgumentNullException">null source</exception>
    public void EnsureIndexes(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        EnsureClientCreated(source);
        CreateIndexes(Client!.GetDatabase(GetDatabaseName(source)));
    }

    /// <summary>
    /// Creates the database.
    /// </summary>
    /// <param name="source">The database source.</param>
    /// <param name="profile">The database profile.</param>
    /// <exception cref="ArgumentNullException">null source or profile
    /// </exception>
    public void CreateDatabase(string source, DataProfile profile)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);

        EnsureClientCreated(source);

        // store facets, flags, and tag sets
        IMongoDatabase db = Client!.GetDatabase(GetDatabaseName(source));

        if (profile.Facets?.Count > 0)
        {
            var collFacets = db.GetCollection<MongoFacetDefinition>(
                MongoFacetDefinition.COLLECTION);
            collFacets.InsertMany(profile.Facets.Select(
                f => new MongoFacetDefinition(f)));
        }

        if (profile.Flags?.Count > 0)
        {
            var collFlags = db.GetCollection<MongoFlagDefinition>(
                MongoFlagDefinition.COLLECTION);
            collFlags.InsertMany(profile.Flags
                .Select(f => new MongoFlagDefinition(f)));
        }

        if (profile.Thesauri?.Count > 0)
        {
            var collSets = db.GetCollection<MongoThesaurus>(
                MongoThesaurus.COLLECTION);
            collSets.InsertMany(profile.Thesauri
                .Select(s => new MongoThesaurus(s)));
        }

        // add indexes
        CreateIndexes(db);
    }

    /// <summary>
    /// Deletes the database.
    /// </summary>
    /// <param name="source">The database source.</param>
    /// <exception cref="ArgumentNullException">null source</exception>
    public void DeleteDatabase(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        EnsureClientCreated(source);
        Client!.DropDatabase(GetDatabaseName(source));
    }

    /// <summary>
    /// Clears the database.
    /// </summary>
    /// <param name="source">The database source.</param>
    public void ClearDatabase(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        EnsureClientCreated(source);
        IMongoDatabase db = Client!.GetDatabase(GetDatabaseName(source));

        // parts
        db.GetCollection<MongoPart>(MongoPart.COLLECTION)
            .DeleteMany(_ => true);
        // items
        db.GetCollection<MongoItem>(MongoItem.COLLECTION)
            .DeleteMany(_ => true);

        // history-parts
        db.GetCollection<MongoHistoryPart>(MongoHistoryPart.COLLECTION)
            .DeleteMany(_ => true);
        // history-items
        db.GetCollection<MongoHistoryItem>(MongoHistoryItem.COLLECTION)
            .DeleteMany(_ => true);

        // facets
        db.GetCollection<MongoFacetDefinition>(MongoFacetDefinition.COLLECTION)
            .DeleteMany(_ => true);
        // flags
        db.GetCollection<MongoFlagDefinition>(MongoFlagDefinition.COLLECTION)
            .DeleteMany(_ => true);
        // thesauri
        db.GetCollection<MongoThesaurus>(MongoThesaurus.COLLECTION)
            .DeleteMany(_ => true);
    }

    /// <summary>
    /// Checks whether the database exists.
    /// </summary>
    /// <param name="source">The database source.</param>
    /// <returns>true if the database exists</returns>
    /// <exception cref="ArgumentNullException">null source</exception>
    public bool DatabaseExists(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        EnsureClientCreated(source);
        string databaseName = GetDatabaseName(source);

        return Client!.ListDatabaseNames().ToList().Contains(databaseName);
    }
}

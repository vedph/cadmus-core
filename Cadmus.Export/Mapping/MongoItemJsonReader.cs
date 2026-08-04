using Cadmus.Mongo;
using Fusi.Tools.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Cadmus.Export.Mapping;

/// <summary>
/// MongoDB implementation of <see cref="IItemJsonReader"/>.
/// <para>Tag: <c>item-json-reader.mongo</c>.</para>
/// </summary>
[Tag("item-json-reader.mongo")]
public sealed class MongoItemJsonReader : MongoConsumerBase, IItemJsonReader,
    IConfigurable<MongoItemJsonReaderOptions>
{
    private readonly JsonWriterSettings _jsonSettings = new()
    {
        Indent = false
    };
    private MongoItemJsonReaderOptions? _options;
    private string? _databaseName;

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(MongoItemJsonReaderOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _databaseName = GetDatabaseName(_options.ConnectionString!);
    }

    private static bool IsPartMatch(ItemPartFilter filter, string typeId,
        string? roleId)
    {
        bool matched = filter.Clauses.Any(c =>
            (c.TypeId == null || c.TypeId == typeId) &&
            (c.RoleId == null || c.RoleId == (roleId ?? "")));

        return filter.IsInverted ? !matched : matched;
    }

    /// <summary>
    /// Reads the item with the specified identifier asynchronously.
    /// </summary>
    /// <param name="itemId">The identifier of the item to read.</param>
    /// <param name="filter">The optional filter for item parts, or null to
    /// read all parts.</param>
    /// <returns>The JSON representation of the item, or null if not found.
    /// </returns>
    /// <exception cref="ArgumentNullException">itemId</exception>
    /// <exception cref="InvalidOperationException">not configured</exception>
    public async Task<JsonDocument?> ReadAsync(string itemId,
        ItemPartFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(itemId);

        if (_options?.ConnectionString == null)
        {
            throw new InvalidOperationException(
                $"{nameof(MongoItemJsonReader)} not configured");
        }

        EnsureClientCreated(_options.ConnectionString);
        IMongoDatabase db = Client!.GetDatabase(_databaseName);

        // 1. read the item
        IMongoCollection<BsonDocument> items =
            db.GetCollection<BsonDocument>(MongoItem.COLLECTION);
        BsonDocument? itemDoc = await items.Find(
            Builders<BsonDocument>.Filter.Eq("_id", itemId))
            .FirstOrDefaultAsync();
        if (itemDoc == null) return null;

        JsonObject itemObject = (JsonObject)JsonNode.Parse(
            itemDoc.ToJson(_jsonSettings))!;

        // 2. read the item's parts, eventually filtered, and add them under
        // a "parts" array property of the item's JSON object
        IMongoCollection<BsonDocument> partCollection =
            db.GetCollection<BsonDocument>(MongoPart.COLLECTION);
        List<BsonDocument> partDocs = await partCollection.Find(
            Builders<BsonDocument>.Filter.Eq("itemId", itemId))
            .ToListAsync();

        JsonArray partsArray = [];
        foreach (BsonDocument partDoc in partDocs)
        {
            // typeId and roleId are already top-level properties of each
            // part document, so there is no need to look into its content
            // to filter parts
            string typeId = partDoc["typeId"].AsString;
            string? roleId = partDoc.Contains("roleId") &&
                partDoc["roleId"] != BsonNull.Value
                ? partDoc["roleId"].AsString : null;

            if (filter != null && !IsPartMatch(filter, typeId, roleId))
                continue;

            partsArray.Add(JsonNode.Parse(partDoc.ToJson(_jsonSettings)));
        }
        itemObject["parts"] = partsArray;

        // 3. build and return the JSON document
        return JsonDocument.Parse(itemObject.ToJsonString());
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing,
    /// or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
    }
}

/// <summary>
/// Options for <see cref="MongoItemJsonReader"/>.
/// </summary>
public class MongoItemJsonReaderOptions
{
    /// <summary>
    /// Gets or sets the MongoDB connection string, like e.g.
    /// <c>mongodb://localhost:27017/cadmus</c>.
    /// </summary>
    public string? ConnectionString { get; set; }
}

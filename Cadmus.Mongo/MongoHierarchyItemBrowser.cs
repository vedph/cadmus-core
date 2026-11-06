using Cadmus.Core;
using Cadmus.Core.Storage;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Cadmus.Mongo;

/// <summary>
/// MongoDB hierarchy-based items browser. This browser assumes that
/// each item to be browsed has at least 1 HierarchyPart,
/// representing its relationships in a single-parent hierarchy: this
/// parts tells which is the parent item, and which are the children items,
/// and the item's depth level (Y) and sibling ordinal number (X) (neither
/// of these two values are necessarily progressive).
/// The browser collects all such parts for the specified parent item ID
/// (or null for the root item), with paging, sorted first by X and then
/// by the item's sort key.
/// <para>Tag: <c>it.vedph.item-browser.mongo.hierarchy</c>.</para>
/// </summary>
/// <seealso cref="IItemBrowser" />
[Tag("it.vedph.item-browser.mongo.hierarchy")]
public sealed class MongoHierarchyItemBrowser : MongoConsumerBase,
    IItemBrowser,
    IConfigurable<ItemBrowserOptions>
{
    /// <summary>
    /// The type ID of the hierarchy part.
    /// </summary>
    public const string HIERARCHY_PART_TYPE_ID = "it.vedph.hierarchy";

    private string? _connection;
    private readonly string _partTypeId;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoHierarchyItemBrowser"/>
    /// class.
    /// </summary>
    public MongoHierarchyItemBrowser()
    {
        // the part ID of HierarchyPart
        _partTypeId = HIERARCHY_PART_TYPE_ID;
    }

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(ItemBrowserOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connection = options.ConnectionString;
    }

    private BsonDocument BuildMatchStage(string? parentId, string? tag)
    {
        BsonDocument tagFilter = tag != null
            ? new BsonDocument().Add("content.tag", tag)
            : new BsonDocument().Add("content.tag", BsonNull.Value);

        // "$match" : {
        return new BsonDocument("$match", new BsonDocument()
            // "typeId" : "it.vedph.hierarchy",
            .Add("typeId", _partTypeId)
            // "$and" : [
            .Add("$and", new BsonArray()
                // { "content.parentId": ... },
                .Add(new BsonDocument().Add("content.parentId",
                    (BsonValue)parentId ?? BsonNull.Value))
                // { "$and": [
                .Add(new BsonDocument().Add("$and", new BsonArray()
                    // { ... } ] } ] }
                    .Add(tagFilter)))));
    }

    private async Task<int> GetTotalAsync(IMongoCollection<BsonDocument> parts,
        string? parentId, string? tag)
    {
        #region MongoDB query
        // db.getCollection("parts").aggregate(
        //   [
        //     { 
        //       "$match" : { 
        //         "typeId" : "it.vedph.hierarchy", 
        //         "$and" : [
        //           { 
        //             "content.parentId" : "..."
        //           }, 
        //           { 
        //             "$and" : [
        //               { 
        //                 "content.tag" : null
        //               }
        //             ]
        //           }
        //         ]
        //       }
        //     }, 
        //     { 
        //       "$count" : "count"
        //     }
        //   ], 
        //   { 
        //     "allowDiskUse" : false
        //   }
        // );
        #endregion

        AggregateOptions aggOptions = new()
        {
            AllowDiskUse = false
        };

        PipelineDefinition<BsonDocument, BsonDocument> pipeline = new BsonDocument[]
        {
            BuildMatchStage(parentId, tag),
            new BsonDocument("$count", "count")
        };

        using var cursor = await parts.AggregateAsync(pipeline, aggOptions)
            .ConfigureAwait(false);
        await cursor.MoveNextAsync().ConfigureAwait(false);
        BsonDocument? first = cursor.Current.FirstOrDefault();
        return first?["count"].AsInt32 ?? 0;
    }

    private static string? GetBsonString(BsonDocument doc, string name) =>
        doc.TryGetValue(name, out BsonValue value) && value.IsString
            ? value.AsString : null;

    private static int GetBsonInt(BsonDocument doc, string name) =>
        doc.TryGetValue(name, out BsonValue value) && value.IsNumeric
            ? value.ToInt32() : 0;

    private static DateTime GetBsonDateTime(BsonDocument doc, string name) =>
        doc.TryGetValue(name, out BsonValue value) && value.IsValidDateTime
            ? value.ToUniversalTime() : DateTime.MinValue;

    private static ItemInfo BsonDocumentToItemInfo(BsonDocument doc)
    {
        return new ItemInfo
        {
            Id = doc["_id"].AsString,
            Title = GetBsonString(doc, "title") ?? "",
            Description = GetBsonString(doc, "description") ?? "",
            FacetId = GetBsonString(doc, "facetId") ?? "",
            GroupId = GetBsonString(doc, "groupId"),
            SortKey = GetBsonString(doc, "sortKey") ?? "",
            Flags = GetBsonInt(doc, "flags"),
            TimeCreated = GetBsonDateTime(doc, "timeCreated"),
            CreatorId = GetBsonString(doc, "creatorId") ?? "",
            TimeModified = GetBsonDateTime(doc, "timeModified"),
            UserId = GetBsonString(doc, "userId") ?? ""
        };
    }

    /// <summary>
    /// Browses the items using the specified options. Items are sorted by
    /// the X value of their hierarchy part, then by their sort key and ID.
    /// Hierarchy parts whose item is not found are skipped.
    /// </summary>
    /// <param name="database">The database name.</param>
    /// <param name="options">The paging and filtering options.
    /// You can set the page size to 0 when you want to retrieve all
    /// the items at the specified level.</param>
    /// <param name="filters">The filters dictionary: it can include
    /// <c>parentId</c>=the ID of the parent item (null or missing for
    /// root items), and <c>tag</c>=requested tag (null or missing for
    /// no tag).</param>
    /// <returns>
    /// Page of items.
    /// </returns>
    /// <exception cref="ArgumentNullException">database or options or
    /// filters</exception>
    /// <exception cref="InvalidOperationException">not configured</exception>
    public async Task<DataPage<ItemInfo>> BrowseAsync(
        string database,
        IPagingOptions options,
        IDictionary<string, string?> filters)
    {
        #region MongoDB query
        // db.getCollection("parts").aggregate(
        //   [
        //     { 
        //       "$match" : { 
        //         "typeId" : "it.vedph.hierarchy", 
        //         "$and" : [
        //           { 
        //             "content.parentId" : "..."
        //           }, 
        //           { 
        //             "$and" : [
        //               { 
        //                 "content.tag" : null
        //               }
        //             ]
        //           }
        //         ]
        //       }
        //     }, 
        //     { 
        //       "$lookup" : { 
        //         "from" : "items", 
        //         "localField" : "itemId", 
        //         "foreignField" : "_id", 
        //         "as" : "items"
        //       }
        //     }, 
        //     { 
        //       "$sort" : { 
        //         "x" : 1, 
        //         "items[0].sortKey" : 1
        //       }
        //     }, 
        //     { 
        //       "$skip" : 0
        //     }, 
        //     { 
        //       "$limit" : 20
        //     }
        //   ], 
        //   { 
        //     "allowDiskUse" : false
        //   }
        // );
        #endregion

        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(filters);

        if (_connection == null)
        {
            throw new InvalidOperationException(
                $"{nameof(MongoHierarchyItemBrowser)} not configured");
        }

        string? parentId = filters.TryGetValue("parentId", out string? p)
            ? p : null;
        string? tag = filters.TryGetValue("tag", out string? t)
            ? t : null;

        EnsureClientCreated(string.Format(CultureInfo.InvariantCulture,
            _connection, database));
        IMongoDatabase db = Client!.GetDatabase(database);
        IMongoCollection<BsonDocument> parts =
            db.GetCollection<BsonDocument>(MongoPart.COLLECTION);

        // the parts count is equal to the items count
        int total = await GetTotalAsync(parts, parentId, tag)
            .ConfigureAwait(false);
        if (total == 0)
        {
            return new DataPage<ItemInfo>(
                options.PageNumber,
                options.PageSize, 0, new List<ItemInfo>());
        }

        // get all the hierarchy parts with the specified Y level,
        // and eventually with the specified tag; get the corresponding
        // item; sort, page, and return
        AggregateOptions aggOptions = new()
        {
            AllowDiskUse = false
        };

        List<BsonDocument> stages = new(
        [
            BuildMatchStage(parentId, tag),
            new BsonDocument("$lookup", new BsonDocument()
                    .Add("from", MongoItem.COLLECTION)
                    .Add("localField", "itemId")
                    .Add("foreignField", "_id")
                    .Add("as", "items")),
            // each part has a single item (orphan parts are dropped)
            new BsonDocument("$unwind", "$items"),
            new BsonDocument("$sort", new BsonDocument()
                    .Add("content.x", 1)
                    .Add("items.sortKey", 1)
                    .Add("items._id", 1)),
            new BsonDocument("$skip",
                Math.Max(0, (options.PageNumber - 1) * options.PageSize))
        ]);

        if (options.PageSize > 0)
            stages.Add(new BsonDocument("$limit", options.PageSize));

        PipelineDefinition<BsonDocument, BsonDocument> pipeline = stages.ToArray();

        // collect all their items, in the paging range
        List<ItemInfo> items = new();
        using (var cursor = await parts.AggregateAsync(pipeline, aggOptions)
            .ConfigureAwait(false))
        {
            while (await cursor.MoveNextAsync().ConfigureAwait(false))
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    ItemInfo info = BsonDocumentToItemInfo(
                        document["items"].AsBsonDocument);

                    // add payload
                    BsonDocument content = document["content"].AsBsonDocument;
                    info.Payload = new MongoHierarchyItemBrowserPayload
                    {
                        Y = GetBsonInt(content, "y"),
                        X = GetBsonInt(content, "x"),
                        ChildCount = content.TryGetValue("childrenIds",
                            out BsonValue children) && children.IsBsonArray
                            ? children.AsBsonArray.Count : 0
                    };

                    items.Add(info);
                }
            }
        }
        return new DataPage<ItemInfo>(options.PageNumber, options.PageSize,
            total, items);
    }
}

/// <summary>
/// Payload object added to <see cref="ItemInfo"/> by
/// <see cref="MongoHierarchyItemBrowser"/>.
/// </summary>
public sealed class MongoHierarchyItemBrowserPayload
{
    /// <summary>
    /// Gets or sets the Y-level.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Gets or sets the X-position in Y.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Gets or sets the children count.
    /// </summary>
    public int ChildCount { get; set; }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return $"{Y}.{X}: {ChildCount}";
    }
}

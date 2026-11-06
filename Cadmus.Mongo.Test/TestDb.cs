using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Storage;
using Cadmus.General.Parts;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Cadmus.Mongo.Test;

/// <summary>
/// The test database shared by the repository tests. All the test classes
/// using it must belong to <see cref="NonParallelResourceCollection"/>.
/// </summary>
internal static class TestDb
{
    public const string DB_NAME = "cadmus-test";
    public const string CONNECTION = "mongodb://localhost:27017/" + DB_NAME;

    private static readonly MongoClient _client = new(CONNECTION);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static TestDb()
    {
        // ensure that the Cadmus conventions (camel case) are registered
        // before any Mongo entity class is mapped by seeding
        RuntimeHelpers.RunClassConstructor(typeof(MongoConsumerBase).TypeHandle);
    }

    public static IMongoDatabase GetDatabase() => _client.GetDatabase(DB_NAME);

    public static MongoCadmusRepository CreateRepository()
    {
        TagAttributeToTypeMap map = new();
        map.Add([typeof(NotePart).Assembly]);
        MongoCadmusRepository repository = new(
            new StandardPartTypeProvider(map),
            new StandardItemSortKeyBuilder());
        repository.Configure(new MongoCadmusRepositoryOptions
        {
            ConnectionString = CONNECTION
        });
        return repository;
    }

    private static void SeedFlags(IMongoDatabase db)
    {
        var collection = db.GetCollection<MongoFlagDefinition>(
            MongoFlagDefinition.COLLECTION);
        collection.InsertMany(new[]
        {
            new MongoFlagDefinition
            {
                Id = 1,
                Label = "Alpha",
                Description = "The alpha flag",
                ColorKey = "FF0000"
            },
            new MongoFlagDefinition
            {
                Id = 2,
                Label = "Beta",
                Description = "The beta flag",
                ColorKey = "00FF00"
            }
        });
    }

    private static void SeedFacets(IMongoDatabase db)
    {
        var collection = db.GetCollection<MongoFacetDefinition>(
            MongoFacetDefinition.COLLECTION);

        MongoFacetDefinition facetA = new()
        {
            Id = "alpha",
            Label = "Alpha",
            Description = "Alpha facet",
            ColorKey = "FF0000"
        };
        facetA.PartDefinitions.AddRange(new[]
        {
            new PartDefinition
            {
                TypeId = "categories",
                RoleId = "categories",
                Name = "Categories",
                Description = "Item's categories",
                IsRequired = true,
                ColorKey = "FF0000",
                GroupKey = "General",
                SortKey = "categories"
            },
            new PartDefinition
            {
                TypeId = "note",
                RoleId = "note",
                Name = "Note",
                Description = "Generic note",
                IsRequired = false,
                ColorKey = "00FF00",
                GroupKey = "General",
                SortKey = "note"
            }
        });

        MongoFacetDefinition facetB = new()
        {
            Id = "beta",
            Label = "Beta",
            Description = "Beta facet",
            ColorKey = "00FF00"
        };
        facetB.PartDefinitions.AddRange(new[]
        {
            new PartDefinition
            {
                TypeId = "categories",
                RoleId = "categories",
                Name = "Categories",
                Description = "Item's categories",
                IsRequired = true,
                ColorKey = "FF0000",
                GroupKey = "General",
                SortKey = "categories"
            },
            new PartDefinition
            {
                TypeId = "keywords",
                RoleId = "keywords",
                Name = "Keywords",
                Description = "Generic keywords",
                IsRequired = false,
                ColorKey = "00FF00",
                GroupKey = "General",
                SortKey = "keywords"
            }
        });

        collection.InsertMany(new[]
        {
            facetA,
            facetB
        });
    }

    private static void SeedItems(IMongoDatabase db)
    {
        var collection = db.GetCollection<MongoItem>(MongoItem.COLLECTION);

        for (int i = 1; i <= 20; i++)
        {
            string userId = (i & 1) == 1 ? "Odd" : "Even";
            DateTime dt = new(2015, 12, i, 0, 0, 0, DateTimeKind.Utc);

            MongoItem item = new()
            {
                Id = $"item-{i:000}",
                Title = $"Item {i}",
                Description = $"Description of item {i}",
                FacetId = (i & 1) == 1 ? "alpha" : "beta",
                GroupId = $"g{i / 2:00}",
                SortKey = $"item{i:000}",
                Flags = i,
                CreatorId = userId,
                UserId = userId,
                TimeCreated = dt,
                TimeModified = dt
            };
            collection.InsertOne(item);
        }
    }

    public static MongoPart CreateMongoPart(IPart part)
    {
        string content =
            JsonSerializer.Serialize(part, part.GetType(), _jsonOptions);

        return new MongoPart(part)
        {
            Content = BsonDocument.Parse(content)
        };
    }

    private static void SeedParts(IMongoDatabase db)
    {
        var collection = db.GetCollection<MongoPart>(MongoPart.COLLECTION);

        // categories
        CategoriesPart categoriesPart = new()
        {
            Id = "part-001",
            ItemId = "item-001",
            RoleId = "categories",
            CreatorId = "Odd",
            UserId = "Odd",
            Categories = {"alpha", "beta"}
        };
        collection.InsertOne(CreateMongoPart(categoriesPart));

        // note
        NotePart notePart = new()
        {
            Id = "part-002",
            ItemId = "item-001",
            RoleId = "note",
            CreatorId = "Odd",
            UserId = "Odd",
            Text = "Some notes."
        };
        collection.InsertOne(CreateMongoPart(notePart));

        // layer: comments
        TokenTextLayerPart<CommentLayerFragment> commentLayerPart =
            new()
            {
                Id = "part-003",
                ItemId = "item-001",
                CreatorId = "Odd",
                UserId = "Odd"
        };
        commentLayerPart.Fragments.AddRange(new []
        {
            new CommentLayerFragment
            {
                Location = "1.2",
                Text = "The comment to 1.2"
            },
            new CommentLayerFragment
            {
                Location = "1.3",
                Text = "The comment to 1.3"
            }
        });
        collection.InsertOne(CreateMongoPart(commentLayerPart));
    }

    private static void ClearHistory(IMongoDatabase db)
    {
        db.GetCollection<MongoHistoryPart>(MongoHistoryPart.COLLECTION)
            .DeleteMany(new BsonDocument());

        db.GetCollection<MongoHistoryItem>(MongoHistoryItem.COLLECTION)
            .DeleteMany(new BsonDocument());
    }

    public static void Prepare()
    {
        _client.DropDatabase(DB_NAME);
        IMongoDatabase db = GetDatabase();

        SeedFlags(db);
        SeedFacets(db);
        SeedItems(db);
        SeedParts(db);
        ClearHistory(db);
    }
}

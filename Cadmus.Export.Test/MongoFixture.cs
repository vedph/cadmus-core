using Cadmus.Mongo;
using CsvHelper;
using CsvHelper.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Cadmus.Export.Test;

public sealed class MongoFixture : IDisposable
{
    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    public MongoFixture()
    {
        // setup MongoDB client with connection to test database
        Client = new MongoClient("mongodb://localhost:27017");
        Database = Client.GetDatabase("test-db");
    }

    public void ClearDatabase()
    {
        // drop all collections
        Database.DropCollection(MongoItem.COLLECTION);
        Database.DropCollection(MongoPart.COLLECTION);
        Database.DropCollection(MongoHistoryItem.COLLECTION);
        Database.DropCollection(MongoHistoryPart.COLLECTION);
    }

    /// <summary>
    /// Loads mock data from an embedded CSV resource.
    /// </summary>
    /// <param name="resourceName">The name of the embedded resource (e.g., "BasicDataset.csv")</param>
    /// <exception cref="ArgumentException">Thrown when the resource is not found</exception>
    public void LoadMockData(string resourceName)
    {
        Assembly assembly = typeof(MongoFixture).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(
            $"Cadmus.Export.Test.Assets.{resourceName}")
            ?? throw new ArgumentException($"Resource {resourceName} not found");
        ClearDatabase();
        LoadDataFromCsv(stream);
    }

    private void ProcessRecords<T>(string collectionName, List<string[]>
        rawLines) where T : class
    {
        // first line should be the header
        if (rawLines.Count < 2) return;

        // join all lines to create a valid CSV
        string headerLine = rawLines[0][0];
        List<string> contentLines = [.. rawLines.Skip(1).Select(l => l[0])];
        string csvContent = $"{headerLine}\n{string.Join("\n", contentLines)}";

        // create in-memory stream
        using MemoryStream memoryStream = new(Encoding.UTF8.GetBytes(csvContent));
        using StreamReader reader = new(memoryStream);
        using CsvReader csv = new(reader,
            new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                PrepareHeaderForMatch = args => args.Header.ToLower()
            });

        // read records with typed model
        List<T> records = [.. csv.GetRecords<T>()];

        // Convert to BsonDocuments
        List<BsonDocument> documents = [];
        foreach (T record in records)
        {
            BsonDocument doc = [];

            switch (record)
            {
                case MockHistoryItem historyItem:
                    PopulateHistoryItemDocument(doc, historyItem);
                    break;
                case MockItem item:
                    PopulateItemDocument(doc, item);
                    break;
                case MockHistoryPart historyPart:
                    PopulateHistoryPartDocument(doc, historyPart);
                    break;
                case MockPart part:
                    PopulatePartDocument(doc, part);
                    break;
            }

            documents.Add(doc);
        }

        // insert the documents
        if (documents.Count > 0) InsertDocuments(collectionName, documents);
    }

    public void LoadDataFromCsv(Stream csvStream)
    {
        using StreamReader reader = new(csvStream);
        string? line;
        string currentCollection = "";
        List<string[]> records = [];

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith('#'))
            {
                // process previous collection if any
                if (records.Count > 0 && !string.IsNullOrEmpty(currentCollection))
                {
                    ProcessRecordsForCollection(currentCollection, records);
                    records.Clear();
                }

                // new collection marker
                currentCollection = line[1..];
                continue;
            }

            // add line to be processed using CsvHelper
            records.Add([line]);
        }

        // process any remaining records
        if (records.Count > 0 && !string.IsNullOrEmpty(currentCollection))
            ProcessRecordsForCollection(currentCollection, records);
    }

    private void ProcessRecordsForCollection(string collectionName, List<string[]> records)
    {
        switch (collectionName)
        {
            case MongoItem.COLLECTION:
                ProcessRecords<MockItem>(collectionName, records);
                break;
            case MongoHistoryItem.COLLECTION:
                ProcessRecords<MockHistoryItem>(collectionName, records);
                break;
            case MongoPart.COLLECTION:
                ProcessRecords<MockPart>(collectionName, records);
                break;
            case MongoHistoryPart.COLLECTION:
                ProcessRecords<MockHistoryPart>(collectionName, records);
                break;
            default:
                throw new ArgumentException($"Unknown collection: {collectionName}");
        }
    }

    private static BsonDocument ParseJsonContent(string jsonContent)
    {
        if (string.IsNullOrEmpty(jsonContent)) return [];

        try
        {
            // ensure the JSON is valid by parsing it first
            return BsonDocument.Parse(jsonContent);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error parsing JSON content: {ex.Message}");
            Debug.WriteLine($"Content value: '{jsonContent}'");

            // if parsing fails, use an empty document
            return [];
        }
    }

    private static void PopulatePartDocument(BsonDocument doc, MockPart part)
    {
        doc["_id"] = part.Id;
        doc["itemId"] = part.ItemId;
        doc["typeId"] = part.TypeId;

        if (!string.IsNullOrEmpty(part.RoleId))
        {
            doc["roleId"] = part.RoleId;
        }

        doc["timeCreated"] = part.TimeCreated.ToUniversalTime();
        doc["creatorId"] = part.CreatorId;
        doc["timeModified"] = part.TimeModified.ToUniversalTime();
        doc["userId"] = part.UserId;

        // parse content JSON
        doc["content"] = ParseJsonContent(part.Content);
    }

    private static void PopulateHistoryPartDocument(BsonDocument doc,
        MockHistoryPart part)
    {
        // first populate the base part fields
        PopulatePartDocument(doc, part);

        // then add history-specific fields
        doc["referenceId"] = part.ReferenceId;
        doc["status"] = part.Status;
    }

    private static void PopulateItemDocument(BsonDocument doc, MockItem item)
    {
        doc["_id"] = item.Id;
        doc["title"] = item.Title;
        doc["description"] = item.Description;
        doc["facetId"] = item.FacetId;
        doc["groupId"] = item.GroupId;
        doc["sortKey"] = item.SortKey;
        doc["flags"] = item.Flags;
        doc["timeCreated"] = item.TimeCreated.ToUniversalTime();
        doc["creatorId"] = item.CreatorId;
        doc["timeModified"] = item.TimeModified.ToUniversalTime();
        doc["userId"] = item.UserId;
    }

    private static void PopulateHistoryItemDocument(BsonDocument doc,
        MockHistoryItem item)
    {
        PopulateItemDocument(doc, item);
        doc["referenceId"] = item.ReferenceId;
        doc["status"] = item.Status;
    }

    private void InsertDocuments(string collectionName,
        List<BsonDocument> documents)
    {
        // map collection names to actual MongoDB collection names
        string actualCollectionName = collectionName switch
        {
            "items" => MongoItem.COLLECTION,
            "history_items" => MongoHistoryItem.COLLECTION,
            "parts" => MongoPart.COLLECTION,
            "history_parts" => MongoHistoryPart.COLLECTION,
            _ => collectionName
        };

        IMongoCollection<BsonDocument> collection = Database
            .GetCollection<BsonDocument>(actualCollectionName);
        collection.InsertMany(documents);
    }

    public void Dispose()
    {
        ClearDatabase();
        GC.SuppressFinalize(this);
    }
}

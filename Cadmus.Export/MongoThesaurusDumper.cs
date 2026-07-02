using Cadmus.Mongo;
using Fusi.Tools;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Export;

/// <summary>
/// MongoDB thesaurus dumper.
/// </summary>
public sealed class MongoThesaurusDumper : MongoConsumerBase
{
    private readonly MongoThesaurusDumperOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoThesaurusDumper"/>
    /// </summary>
    /// <param name="options"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public MongoThesaurusDumper(MongoThesaurusDumperOptions options)
    {
        _options = options
            ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Processes a document to handle targetId alias logic.
    /// If the document has a targetId property with a non-null value,
    /// only _id and targetId are kept.
    /// Otherwise, the full document is returned but with targetId removed
    /// if present.
    /// </summary>
    /// <param name="doc">The document to process.</param>
    /// <returns>The processed document.</returns>
    private static BsonDocument ProcessDocument(BsonDocument doc)
    {
        // check if targetId exists and has a non-null, non-undefined value
        if (doc.Contains("targetId") && 
            doc["targetId"] != BsonNull.Value && 
            !doc["targetId"].IsBsonUndefined &&
            !string.IsNullOrEmpty(doc["targetId"].AsString))
        {
            // create a new document with only id and targetId (alias case)
            return new BsonDocument
            {
                { "id", doc["_id"] },
                { "targetId", doc["targetId"] }
            };
        }
        
        // for normal thesauri (with entries):
        // - rename _id into id
        // - remove targetId if present
        // - ensure id comes before entries
        BsonDocument result = new()
        {
            // add id first
            ["id"] = doc["_id"]
        };
        
        // add all other fields except _id and targetId
        foreach (var element in doc)
        {
            if (element.Name != "_id" && element.Name != "targetId")
                result[element.Name] = element.Value;
        }
        
        // rename _id into id in entries and ensure id comes before value
        if (result.Contains("entries") && result["entries"].IsBsonArray)
        {
            BsonArray entries = result["entries"].AsBsonArray;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].IsBsonDocument)
                {
                    BsonDocument entry = entries[i].AsBsonDocument;
                    if (entry.Contains("_id"))
                    {
                        // create new entry with id first, then value
                        BsonDocument newEntry = new()
                        {
                            ["id"] = entry["_id"]
                        };
                        if (entry.Contains("value"))
                        {
                            newEntry["value"] = entry["value"];
                        }
                        entries[i] = newEntry;
                    }
                }
            }
        }
        
        return result;
    }

    /// <summary>
    /// Dumps all the thesauri from the MongoDB database to a JSON file.
    /// </summary>
    /// <param name="cancel">The cancellation token.</param>
    /// <param name="progress">The optional progress reporter.</param>
    /// <returns>Count of thesauri dumped.</returns>
    public async Task<int> DumpAsync(CancellationToken cancel,
        IProgress<ProgressReport>? progress = null)
    {
        EnsureClientCreated(string.Format(_options.ConnectionString,
            _options.DatabaseName));

        ProgressReport? report = progress != null
            ? new ProgressReport()
            : null;

        // get the database and collection
        IMongoDatabase db = Client!.GetDatabase(_options.DatabaseName);
        IMongoCollection<BsonDocument> collection = 
            db.GetCollection<BsonDocument>("thesauri");

        // JSON writer settings
        JsonWriterSettings jsonSettings = new()
        {
            Indent = _options.Indented
        };

        using StreamWriter writer = new(_options.OutputPath, false, Encoding.UTF8);
        
        // write opening bracket for JSON array
        await writer.WriteLineAsync("[");

        int count = 0;

        // first, try to read the "model-types@en" document if it exists
        BsonDocument? modelTypesDoc = await collection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", "model-types@en"))
            .FirstOrDefaultAsync(cancel);

        if (modelTypesDoc != null)
        {
            cancel.ThrowIfCancellationRequested();

            BsonDocument processedDoc = ProcessDocument(modelTypesDoc);
            string json = processedDoc.ToJson(jsonSettings);
            await writer.WriteAsync(json);
            count++;

            if (report != null)
            {
                report.Message = "model-types@en";
                report.Count = count;
                progress?.Report(report);
            }
        }

        // then read all other documents, sorted by _id, excluding "model-types@en"
        using IAsyncCursor<BsonDocument> cursor = await collection
            .Find(Builders<BsonDocument>.Filter.Ne("_id", "model-types@en"))
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
            .ToCursorAsync(cancel);

        while (await cursor.MoveNextAsync(cancel))
        {
            foreach (BsonDocument doc in cursor.Current)
            {
                cancel.ThrowIfCancellationRequested();

                if (++count > 1) await writer.WriteLineAsync(",");

                BsonDocument processedDoc = ProcessDocument(doc);
                string json = processedDoc.ToJson(jsonSettings);
                await writer.WriteAsync(json);

                if (report != null)
                {
                    report.Message = doc["_id"].AsString;
                    report.Count = count;
                    progress?.Report(report);
                }
            }
        }

        // write closing bracket for JSON array
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("]");

        return count;
    }
}

/// <summary>
/// Options for the <see cref="MongoThesaurusDumper"/>.
/// </summary>
public class MongoThesaurusDumperOptions
{
    /// <summary>
    /// The connection string to the MongoDB server, where <c>{0}</c> is
    /// the placeholder for the database name.
    /// </summary>
    public string ConnectionString { get; set; } =
        "mongodb://localhost:27017/{0}";

    /// <summary>
    /// The database name to use.
    /// </summary>
    public string DatabaseName { get; set; } = "cadmus";

    /// <summary>
    /// The output JSON file path.
    /// </summary>
    public string OutputPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory), "thesauri.json");

    /// <summary>
    /// True to indent the output JSON.
    /// </summary>
    public bool Indented { get; set; }
}

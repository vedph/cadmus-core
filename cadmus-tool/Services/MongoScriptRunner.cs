using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Cadmus.Cli.Services;

/// <summary>
/// Mongo script runner. This supports a subset of Mongo commands:
/// <para>
/// 1. Collection Operations:
/// - data manipulation: find, findOne, insertOne, insertMany, updateOne,
/// updateMany, replaceOne, deleteOne, deleteMany.
///	- collection administration: drop, createIndex, dropIndex, countDocuments.
///	</para>
///	<para>
///	2. Database Operations:
/// - administration: createCollection, dropDatabase, getCollectionNames,
/// listCollections.
/// - information: stats, runCommand.
/// </para>
/// <para>
/// 3. Connection Operations:
/// - use [database] - switches to the specified database.
/// </para>
/// <para>
/// 4. Collection Access Methods:
/// - db.collection - directly access a collection with dot notation.
/// - db.getCollection("name") - access a collection by name.
/// </para>
/// </summary>
public sealed class MongoScriptRunner
{
    private IMongoDatabase _database;
    private readonly MongoClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoScriptRunner"/> class.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="databaseName">Name of the database.</param>
    /// <exception cref="ArgumentNullException">connectionString or databaseName
    /// </exception>
    public MongoScriptRunner(string connectionString, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        ArgumentNullException.ThrowIfNull(databaseName);

        _client = new MongoClient(connectionString);
        _database = _client.GetDatabase(databaseName);
    }

    private static List<string> SplitScriptIntoCommands(string script)
    {
        // split script into commands, handling multiline commands and comments
        return [.. script
            .Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(cmd => cmd.Trim())
            .Where(cmd => !string.IsNullOrWhiteSpace(cmd) &&
                          !cmd.TrimStart().StartsWith("//"))];
    }

    private async Task<string> ExecuteDatabaseCommandAsync(string command)
    {
        // handle db.getCollection("name") command
        if (command.StartsWith("db.getCollection(", StringComparison.OrdinalIgnoreCase))
        {
            int startIndex = command.IndexOf('(') + 1;
            int endIndex = command.IndexOf(')');
            string collectionName = command[startIndex..endIndex].Trim('\"', '\'');

            // extract the remaining command after the collection name
            string remainingCommand = command[(endIndex + 1)..].TrimStart('.');

            return await ExecuteCollectionCommandAsync(collectionName,
                remainingCommand);
        }
        // handle db.name commands
        else if (command.StartsWith("db.", StringComparison.OrdinalIgnoreCase)
            && command.Contains('.'))
        {
            string collectionName = command.Split('.')[1].Split('(')[0].Trim();
            string remainingCommand = command[(command.IndexOf(collectionName)
                + collectionName.Length + 1)..];

            return await ExecuteCollectionCommandAsync(
                collectionName, remainingCommand);
        }
        // handle db.* commands
        else if (command.StartsWith("db.createCollection(",
            StringComparison.OrdinalIgnoreCase))
        {
            string collectionName = ExtractJsonFromCommand(command)
                .Trim('\"', '\'');
            await _database.CreateCollectionAsync(collectionName);
            return $"Collection '{collectionName}' created";
        }
        else if (command.StartsWith("db.dropDatabase(",
            StringComparison.OrdinalIgnoreCase))
        {
            await _client.DropDatabaseAsync(
                _database.DatabaseNamespace.DatabaseName);
            return
                $"Database '{_database.DatabaseNamespace.DatabaseName}' dropped";
        }
        else if (command.StartsWith("db.getCollectionNames(",
                    StringComparison.OrdinalIgnoreCase) ||
                 command.StartsWith("db.listCollections(",
                    StringComparison.OrdinalIgnoreCase))
        {
            IAsyncCursor<string> collections =
                await _database.ListCollectionNamesAsync();
            List<string> collectionList = await collections.ToListAsync();
            return string.Join(", ", collectionList);
        }
        else if (command.StartsWith("db.runCommand(",
            StringComparison.OrdinalIgnoreCase))
        {
            BsonDocument commandDoc = BsonDocument.Parse(
                ExtractJsonFromCommand(command));
            BsonDocument result =
                await _database.RunCommandAsync<BsonDocument>(commandDoc);
            return result.ToJson();
        }
        else if (command.StartsWith("db.stats(",
            StringComparison.OrdinalIgnoreCase))
        {
            BsonDocument stats = await _database.RunCommandAsync<BsonDocument>(
                new BsonDocument("dbStats", 1));
            return stats.ToJson();
        }

        throw new NotSupportedException($"Command not supported: {command}");
    }

    private async Task<string> ExecuteCollectionCommandAsync(string collectionName, string command)
    {
        IMongoCollection<BsonDocument> collection =
            _database.GetCollection<BsonDocument>(collectionName);

        if (command.StartsWith("find("))
        {
            string filterJson = ExtractJsonFromCommand(command);
            BsonDocument filter = string.IsNullOrWhiteSpace(filterJson)
                ? [] : BsonDocument.Parse(filterJson);

            List<BsonDocument> results = await collection.Find(filter).ToListAsync();
            return $"Found {results.Count} documents";
        }
        else if (command.StartsWith("findOne("))
        {
            string filterJson = ExtractJsonFromCommand(command);
            BsonDocument filter = string.IsNullOrWhiteSpace(filterJson)
                ? [] : BsonDocument.Parse(filterJson);

            BsonDocument? result = await collection.Find(filter)
                .FirstOrDefaultAsync();
            return result != null
                ? $"Document found: {result.ToJson()}" : "No document found";
        }
        else if (command.StartsWith("insertOne("))
        {
            BsonDocument document = BsonDocument.Parse(
                ExtractJsonFromCommand(command));
            await collection.InsertOneAsync(document);
            return "Document inserted successfully";
        }
        else if (command.StartsWith("insertMany("))
        {
            string jsonArray = ExtractJsonFromCommand(command);
            List<BsonDocument> documents = BsonSerializer
                .Deserialize<List<BsonDocument>>(jsonArray);
            await collection.InsertManyAsync(documents);
            return $"{documents.Count} documents inserted successfully";
        }
        else if (command.StartsWith("updateOne("))
        {
            string[] parts = ExtractParametersFromCommand(command);
            BsonDocument filter = BsonDocument.Parse(parts[0]);
            BsonDocument update = BsonDocument.Parse(parts[1]);

            UpdateResult result = await collection.UpdateOneAsync(filter, update);
            return $"Updated {result.ModifiedCount} document(s)";
        }
        else if (command.StartsWith("updateMany("))
        {
            string[] parts = ExtractParametersFromCommand(command);
            BsonDocument filter = BsonDocument.Parse(parts[0]);
            BsonDocument update = BsonDocument.Parse(parts[1]);

            UpdateResult result = await collection.UpdateManyAsync(filter, update);
            return $"Updated {result.ModifiedCount} of {result.MatchedCount} " +
                "matched document(s)";
        }
        else if (command.StartsWith("replaceOne("))
        {
            string[] parts = ExtractParametersFromCommand(command);
            BsonDocument filter = BsonDocument.Parse(parts[0]);
            BsonDocument replacement = BsonDocument.Parse(parts[1]);

            ReplaceOneResult result = await collection.ReplaceOneAsync(filter,
                replacement);
            return $"Replaced {result.ModifiedCount} document(s)";
        }
        else if (command.StartsWith("deleteOne("))
        {
            BsonDocument filter = BsonDocument.Parse(
                ExtractJsonFromCommand(command));
            DeleteResult result = await collection.DeleteOneAsync(filter);
            return $"Deleted {result.DeletedCount} document(s)";
        }
        else if (command.StartsWith("deleteMany("))
        {
            BsonDocument filter = BsonDocument.Parse(
                ExtractJsonFromCommand(command));
            DeleteResult result = await collection.DeleteManyAsync(filter);
            return $"Deleted {result.DeletedCount} document(s)";
        }
        else if (command.StartsWith("countDocuments("))
        {
            string filterJson = ExtractJsonFromCommand(command);
            BsonDocument filter = string.IsNullOrWhiteSpace(filterJson)
                ? [] : BsonDocument.Parse(filterJson);

            long count = await collection.CountDocumentsAsync(filter);
            return $"Count: {count}";
        }
        else if (command.StartsWith("drop("))
        {
            await _database.DropCollectionAsync(collectionName);
            return $"Collection '{collectionName}' dropped";
        }
        else if (command.StartsWith("createIndex("))
        {
            string[] parts = ExtractParametersFromCommand(command);
            BsonDocument keys = BsonDocument.Parse(parts[0]);

            CreateIndexOptions? options = null;
            if (parts.Length > 1)
            {
                BsonDocument optionsDoc = BsonDocument.Parse(parts[1]);
                options = new CreateIndexOptions();

                if (optionsDoc.TryGetValue("unique", out BsonValue? uniqueValue))
                {
                    options.Unique = uniqueValue.AsBoolean;
                }

                if (optionsDoc.TryGetValue("name", out BsonValue? nameValue))
                {
                    options.Name = nameValue.AsString;
                }

                if (optionsDoc.TryGetValue("sparse", out BsonValue? sparseValue))
                {
                    options.Sparse = sparseValue.AsBoolean;
                }
            }

            string indexName = await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(keys, options));
            return $"Index '{indexName}' created";
        }
        else if (command.StartsWith("dropIndex("))
        {
            string indexName = ExtractJsonFromCommand(command).Trim('\"', '\'');
            await collection.Indexes.DropOneAsync(indexName);
            return $"Index '{indexName}' dropped";
        }

        throw new NotSupportedException($"Command not supported: {command}");
    }

    private async Task<MongoCommandExecutionResult> ExecuteSingleCommandAsync(
        string command)
    {
        MongoCommandExecutionResult commandResult = new() { OriginalCommand = command };
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            // determine command type and execute accordingly
            if (command.StartsWith("db.", StringComparison.OrdinalIgnoreCase))
            {
                commandResult.Result = await ExecuteDatabaseCommandAsync(command);
            }
            else if (command.StartsWith("use ", StringComparison.OrdinalIgnoreCase))
            {
                string databaseName = command[4..].Trim();
                _database = _client.GetDatabase(databaseName);
                commandResult.Result = $"Switched to database {databaseName}";
            }
            else
            {
                // for other types of commands, attempt direct execution
                BsonDocument bsonCommand = BsonDocument.Parse(command);
                BsonDocument result =
                    await _database.RunCommandAsync<BsonDocument>(bsonCommand);
                commandResult.Result = result.ToJson();
            }

            commandResult.Success = true;
        }
        catch (Exception ex)
        {
            commandResult.Success = false;
            commandResult.ErrorMessage = ex.Message;
            commandResult.FullErrorDetails = ex.ToString();
        }
        finally
        {
            stopwatch.Stop();
            commandResult.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return commandResult;
    }

    private static string[] ExtractParametersFromCommand(string command)
    {
        string paramsString = ExtractJsonFromCommand(command);

        // find the index of the first comma that's not inside a nested
        // object/array
        int bracketCount = 0;
        int commaIndex = -1;

        for (int i = 0; i < paramsString.Length; i++)
        {
            char c = paramsString[i];
            if (c == '{' || c == '[')
            {
                bracketCount++;
            }
            else if (c == '}' || c == ']')
            {
                bracketCount--;
            }
            else if (c == ',' && bracketCount == 0)
            {
                commaIndex = i;
                break;
            }
        }

        if (commaIndex == -1)
        {
            return [paramsString];
        }

        return
        [
            paramsString[..commaIndex].Trim(),
            paramsString[(commaIndex + 1)..].Trim()
        ];
    }

    private static string ExtractJsonFromCommand(string command)
    {
        // extract JSON content from command
        int startIndex = command.IndexOf('(') + 1;
        int endIndex = command.LastIndexOf(')');
        return command[startIndex..endIndex].Trim();
    }

    /// <summary>
    /// Executes a MongoDB script and returns detailed execution results.
    /// </summary>
    /// <param name="script">The MongoDB script to execute</param>
    /// <returns>A detailed execution result object</returns>
    public async Task<MongoScriptExecutionResult> RunScriptAsync(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            throw new ArgumentException("Script cannot be null or empty.",
                nameof(script));
        }

        MongoScriptExecutionResult result = new();
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            // trim and split the script into individual commands
            List<string> commands = SplitScriptIntoCommands(script);
            result.CommandCount = commands.Count;

            foreach (string command in commands)
            {
                MongoCommandExecutionResult commandResult =
                    await ExecuteSingleCommandAsync(command);
                result.CommandResults.Add(commandResult);
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.FullErrorDetails = ex.ToString();
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }
}

/// <summary>
/// The result of executing a MongoDB script.
/// </summary>
public class MongoScriptExecutionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the script succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the full error details.
    /// </summary>
    public string? FullErrorDetails { get; set; }

    /// <summary>
    /// Gets or sets the execution time ms.
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the commands count.
    /// </summary>
    public int CommandCount { get; set; }

    /// <summary>
    /// Gets or sets the command results.
    /// </summary>
    public List<MongoCommandExecutionResult> CommandResults { get; set; } = [];
}

public class MongoCommandExecutionResult
{
    /// <summary>
    /// Gets or sets the original command.
    /// </summary>
    public required string OriginalCommand { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the command succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the result.
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the full error details.
    /// </summary>
    public string? FullErrorDetails { get; set; }

    /// <summary>
    /// Gets or sets the execution time ms.
    /// </summary>
    public long ExecutionTimeMs { get; set; }
}

using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System;
using System.Collections.Concurrent;

namespace Cadmus.Mongo;

/// <summary>
/// A base class for Mongo consumer code.
/// </summary>
public abstract class MongoConsumerBase
{
    // MongoClient is thread-safe, and the driver recommends a single
    // instance per connection string for the whole process: each client
    // has its own connection pool and server monitoring
    private static readonly ConcurrentDictionary<string, MongoClient> _clients
        = new(StringComparer.Ordinal);

    private string? _currentConnString;

    /// <summary>
    /// The Mongo client. This gets set by <see cref="EnsureClientCreated(string)"/>
    /// and kept until the received connection string changes. Clients are
    /// shared by all the consumers using the same connection string.
    /// </summary>
    protected MongoClient? Client { get; private set; }

    static MongoConsumerBase()
    {
        // camel case everything:
        // https://stackoverflow.com/questions/19521626/mongodb-convention-packs/19521784#19521784
        // (registered once: each registration is appended to a global list)
        ConventionPack pack = new()
        {
            new CamelCaseElementNameConvention()
        };
        ConventionRegistry.Register("camel case", pack, _ => true);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoConsumerBase"/> class.
    /// </summary>
    protected MongoConsumerBase()
    {
    }

    /// <summary>
    /// Gets the name of the database from the specified source.
    /// </summary>
    /// <param name="source">The source (connection string).</param>
    /// <returns>Database name.</returns>
    /// <exception cref="ArgumentNullException">source</exception>
    protected static string GetDatabaseName(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new MongoUrl(source).DatabaseName;
    }

    /// <summary>
    /// Ensures that <see cref="Client"/> is created for the specified
    /// source.
    /// </summary>
    /// <param name="source">The source (connection string).</param>
    /// <exception cref="ArgumentNullException">source</exception>
    protected void EnsureClientCreated(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (Client != null && _currentConnString == source) return;

        Client = _clients.GetOrAdd(source, s => new MongoClient(s));
        _currentConnString = source;
    }
}

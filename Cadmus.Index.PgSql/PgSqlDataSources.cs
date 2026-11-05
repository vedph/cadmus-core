using Npgsql;
using System;
using System.Collections.Concurrent;

namespace Cadmus.Index.PgSql;

/// <summary>
/// Process-wide cache of PostgreSQL data sources, one per connection string.
/// Index readers and writers are usually created for each operation, so
/// they share a data source (and thus its connections pool) rather than
/// creating their own.
/// </summary>
internal static class PgSqlDataSources
{
    private static readonly ConcurrentDictionary<string,
        Lazy<NpgsqlDataSource>> _sources = new();

    /// <summary>
    /// Gets the data source for the specified connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>Data source.</returns>
    /// <exception cref="InvalidOperationException">no connection string
    /// </exception>
    public static NpgsqlDataSource Get(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "No connection string configured for the PostgreSQL index");
        }
        return _sources.GetOrAdd(connectionString,
            cs => new Lazy<NpgsqlDataSource>(() => NpgsqlDataSource.Create(cs)))
            .Value;
    }
}

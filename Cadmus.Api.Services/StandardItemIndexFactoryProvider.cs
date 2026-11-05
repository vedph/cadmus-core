using Cadmus.Index.Config;
using Cadmus.Index.PgSql;
using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;

namespace Cadmus.Api.Services;

/// <summary>
/// Standard item index factory provider. This provides factories for the
/// PostgreSQL index, whose components are tagged
/// <c>item-index-writer.pgsql</c> and <c>item-index-reader.pgsql</c>;
/// the legacy tags <c>item-index-writer.ef-pg</c> and
/// <c>item-index-reader.ef-pg</c> are still supported.
/// Factories are cached by profile, as building them is expensive.
/// </summary>
/// <seealso cref="IItemIndexFactoryProvider" />
public sealed class StandardItemIndexFactoryProvider :
    IItemIndexFactoryProvider
{
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<string, Lazy<ItemIndexFactory>>
        _factories = new();

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="StandardItemIndexFactoryProvider"/> class.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <exception cref="ArgumentNullException">connectionString</exception>
    public StandardItemIndexFactoryProvider(string connectionString)
    {
        _connectionString = connectionString ??
            throw new ArgumentNullException(nameof(connectionString));
    }

    private static IHost GetHost(string config)
    {
        return new HostBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                ItemIndexFactory.ConfigureServices(services,
                    // Cadmus.Index.PgSql
                    typeof(PgSqlItemIndexWriter).Assembly,
                    // Cadmus.Index.Ef.PgSql (legacy tags)
#pragma warning disable CS0618 // Type or member is obsolete
                    typeof(Index.Ef.PgSql.EfPgSqlItemIndexWriter).Assembly);
#pragma warning restore CS0618 // Type or member is obsolete
            })
            // extension method from Fusi library
            .AddInMemoryJson(config)
            .Build();
    }

    /// <summary>
    /// Gets the index factory for the specified profile.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Factory.</returns>
    /// <exception cref="ArgumentNullException">profile</exception>
    public ItemIndexFactory GetFactory(string profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return _factories.GetOrAdd(profile, p => new Lazy<ItemIndexFactory>(
            () => new ItemIndexFactory(GetHost(p), _connectionString))).Value;
    }
}

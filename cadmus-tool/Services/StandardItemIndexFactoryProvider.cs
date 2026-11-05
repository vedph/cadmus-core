using Cadmus.Index.Config;
using Cadmus.Index.PgSql;
using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Microsoft.Extensions.Hosting;
using System;

namespace Cadmus.Cli.Services;

/// <summary>
/// Standard item index writer factory provider.
/// </summary>
/// <seealso cref="IItemIndexFactoryProvider" />
public sealed class StandardItemIndexFactoryProvider :
    IItemIndexFactoryProvider
{
    private readonly string _connectionString;

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
    /// Gets the part/fragment seeders factory.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Factory.</returns>
    /// <exception cref="ArgumentNullException">profile</exception>
    public ItemIndexFactory GetFactory(string profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ItemIndexFactory(GetHost(profile), _connectionString);
    }
}

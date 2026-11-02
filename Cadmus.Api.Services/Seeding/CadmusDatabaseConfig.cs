using Microsoft.Extensions.Configuration;
using System;
using System.Globalization;

namespace Cadmus.Api.Services.Seeding;

/// <summary>
/// A database target, defined by a connection string template with
/// placeholder <c>{0}</c> for the database name, and the database name.
/// </summary>
/// <param name="Template">The connection string template.</param>
/// <param name="Name">The database name.</param>
public sealed record DatabaseTarget(string Template, string Name)
{
    /// <summary>
    /// Gets the connection string to this database.
    /// </summary>
    public string ConnectionString =>
        string.Format(CultureInfo.InvariantCulture, Template, Name);

    /// <summary>
    /// Returns a string that represents this instance.
    /// </summary>
    /// <returns>Database name.</returns>
    public override string ToString() => Name;
}

/// <summary>
/// Cadmus API databases configuration. This is the single source of truth
/// for the names and connection strings of the Cadmus databases, used both
/// when registering services and when creating databases at startup, so
/// that what gets created is exactly what gets used.
/// </summary>
public static class CadmusDatabaseConfig
{
    /// <summary>
    /// The suffix appended to <c>DatabaseNames:Data</c> to build the
    /// graph database name.
    /// </summary>
    public const string GRAPH_SUFFIX = "-graph";

    private static string GetRequired(string key,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Missing required configuration value: {key}");
        }
        return value;
    }

    private static string GetDataName(IConfiguration config) =>
        GetRequired("DatabaseNames:Data", config["DatabaseNames:Data"]);

    /// <summary>
    /// Gets the main data (MongoDB) database from <c>ConnectionStrings:Default</c>
    /// and <c>DatabaseNames:Data</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>Target.</returns>
    /// <exception cref="ArgumentNullException">config</exception>
    /// <exception cref="InvalidOperationException">missing configuration
    /// </exception>
    public static DatabaseTarget GetDataDatabase(IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new DatabaseTarget(
            GetRequired("ConnectionStrings:Default",
                config.GetConnectionString("Default")),
            GetDataName(config));
    }

    /// <summary>
    /// Gets the item index (PostgreSQL) database from
    /// <c>ConnectionStrings:Index</c> and <c>DatabaseNames:Data</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>Target.</returns>
    /// <exception cref="ArgumentNullException">config</exception>
    /// <exception cref="InvalidOperationException">missing configuration
    /// </exception>
    public static DatabaseTarget GetIndexDatabase(IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new DatabaseTarget(
            GetRequired("ConnectionStrings:Index",
                config.GetConnectionString("Index")),
            GetDataName(config));
    }

    /// <summary>
    /// Gets the graph (PostgreSQL) database from <c>ConnectionStrings:Graph</c>
    /// (falling back to <c>ConnectionStrings:Index</c> if not found), whose
    /// name is <c>DatabaseNames:Data</c> plus suffix <c>-graph</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>Target.</returns>
    /// <exception cref="ArgumentNullException">config</exception>
    /// <exception cref="InvalidOperationException">missing configuration
    /// </exception>
    public static DatabaseTarget GetGraphDatabase(IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new DatabaseTarget(
            GetRequired("ConnectionStrings:Graph",
                config.GetConnectionString("Graph")
                ?? config.GetConnectionString("Index")),
            GetDataName(config) + GRAPH_SUFFIX);
    }

    /// <summary>
    /// Gets the authentication (PostgreSQL) database from
    /// <c>ConnectionStrings:Auth</c> and <c>DatabaseNames:Auth</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>Target.</returns>
    /// <exception cref="ArgumentNullException">config</exception>
    /// <exception cref="InvalidOperationException">missing configuration
    /// </exception>
    public static DatabaseTarget GetAuthDatabase(IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new DatabaseTarget(
            GetRequired("ConnectionStrings:Auth",
                config.GetConnectionString("Auth")),
            GetRequired("DatabaseNames:Auth",
                config["DatabaseNames:Auth"]));
    }
}

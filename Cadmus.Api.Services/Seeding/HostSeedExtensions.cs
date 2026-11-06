using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Storage;
using Cadmus.Graph;
using Cadmus.Graph.Ef.PgSql;
using Cadmus.Index;
using Cadmus.Index.Config;
using Cadmus.Index.PgSql;
using Cadmus.Mongo;
using MongoDB.Driver;
using Cadmus.Seed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Cadmus.Api.Services.Seeding;

/// <summary>
/// Cadmus databases initializer extension to <see cref="IHost"/>.
/// This ensures that all the databases required by the configuration exist,
/// creating them when missing:
/// <list type="bullet">
/// <item><description>the item index database (PostgreSQL), when
/// <c>Indexing:IsEnabled</c> is true;</description></item>
/// <item><description>the graph database (PostgreSQL), when
/// <c>Indexing:IsGraphEnabled</c> is true;</description></item>
/// <item><description>the main data database (MongoDB), which is also seeded
/// from the profile at <c>Seed:ProfileSource</c> with its settings, mock
/// items (<c>Seed:ItemCount</c>) and imports.</description></item>
/// </list>
/// Each step is independent and idempotent, so that a database is created
/// whenever it is missing, whatever the state of the other ones. The
/// authentication database is initialized separately (see
/// <see cref="AuthDatabaseInitializer"/>).
/// </summary>
public static class HostSeedExtensions
{
    private static ILogger GetLogger(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(HostSeedExtensions));

    #region Index and graph
    /// <summary>
    /// Ensures that the item index database exists when indexing is enabled
    /// (<c>Indexing:IsEnabled</c>), retrying on transient connection errors.
    /// If the database already exists, its schema is brought up to date
    /// (this upgrades in place databases created by older versions).
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <exception cref="ArgumentNullException">serviceProvider</exception>
    public static Task EnsureIndexDatabaseAsync(
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        IConfiguration config =
            serviceProvider.GetRequiredService<IConfiguration>();
        ILogger logger = GetLogger(serviceProvider);

        if (!config.GetValue<bool>("Indexing:IsEnabled"))
        {
            logger.LogInformation("Indexing disabled: index database skipped");
            return Task.CompletedTask;
        }

        DatabaseTarget target = CadmusDatabaseConfig.GetIndexDatabase(config);
        logger.LogInformation("Ensuring index database {Database}...", target);

        return SeedRetryPolicy.Create(logger, "Index database").ExecuteAsync(
            async () =>
            {
                if (!PgSqlDatabaseInitializer.EnsureDatabase(target,
                    PgSqlIndexSchema.GetSql(), null, logger))
                {
                    // the schema script is idempotent: on an up-to-date
                    // database this changes nothing
                    logger.LogInformation(
                        "Updating index database {Database} schema...", target);
                    await PgSqlIndexSchema.ApplyAsync(target.ConnectionString);
                }
            });
    }

    /// <summary>
    /// Ensures that the graph database exists when the graph is enabled
    /// (<c>Indexing:IsGraphEnabled</c>), retrying on transient connection
    /// errors. If the database already exists, its schema is brought up to
    /// date (this upgrades in place databases created by older versions,
    /// adding the missing indexes and replacing procedures).
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="graphSql">The optional SQL code to seed the graph
    /// database with preset data when it gets created. This is usually
    /// provided by project-specific implementations.</param>
    /// <exception cref="ArgumentNullException">serviceProvider</exception>
    public static Task EnsureGraphDatabaseAsync(
        IServiceProvider serviceProvider, string? graphSql = null)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        IConfiguration config =
            serviceProvider.GetRequiredService<IConfiguration>();
        ILogger logger = GetLogger(serviceProvider);

        if (!config.GetValue<bool>("Indexing:IsGraphEnabled"))
        {
            logger.LogInformation("Graph disabled: graph database skipped");
            return Task.CompletedTask;
        }

        DatabaseTarget target = CadmusDatabaseConfig.GetGraphDatabase(config);
        logger.LogInformation("Ensuring graph database {Database}...", target);

        return SeedRetryPolicy.Create(logger, "Graph database").ExecuteAsync(
            async () =>
            {
                if (!PgSqlDatabaseInitializer.EnsureDatabase(target,
                    EfPgSqlGraphRepository.GetSchema(), graphSql, logger))
                {
                    // the update script is idempotent: on an up-to-date
                    // database this changes nothing
                    logger.LogInformation(
                        "Updating graph database {Database} schema...", target);
                    await PgSqlGraphSchema.ApplyAsync(target.ConnectionString);
                }
            });
    }
    #endregion

    #region Data
    private static async Task SeedItemsAsync(IServiceProvider serviceProvider,
        IConfiguration config, PartSeederFactory factory,
        ICadmusRepository repository, int count, ILogger logger)
    {
        // index writer if indexing is enabled
        IItemIndexWriter? indexWriter = null;
        if (config.GetValue<bool>("Indexing:IsEnabled"))
        {
            ItemIndexFactory? indexFactory = await ItemIndexHelper
                .GetIndexFactoryAsync(config, serviceProvider);
            indexWriter = indexFactory?.GetItemIndexWriter();
            if (indexWriter == null)
            {
                logger.LogWarning("No index writer in profile: " +
                    "seeded items will not be indexed");
            }
        }

        // graph updater if graph is enabled
        GraphUpdater? graphUpdater = null;
        if (config.GetValue<bool>("Indexing:IsGraphEnabled"))
        {
            IGraphRepository graphRepository =
                serviceProvider.GetRequiredService<IGraphRepository>();
            graphRepository.Cache = serviceProvider.GetService<IMemoryCache>();
            graphUpdater = serviceProvider.GetService<GraphUpdater>()
                ?? new GraphUpdater(graphRepository);
        }

        logger.LogInformation("Seeding {Count} mock items...", count);
        CadmusSeeder seeder = new(factory);

        int n = 0;
        foreach (IItem item in seeder.GetItems(count))
        {
            n++;
            logger.LogDebug("Seeding item {N}/{Count}: {Item} " +
                "({PartCount} parts)", n, count, item, item.Parts.Count);

            await repository.AddItemAsync(item, true);
            foreach (IPart part in item.Parts)
                await repository.AddPartAsync(part, true);

            if (indexWriter != null) await indexWriter.WriteItemAsync(item);
            if (graphUpdater != null)
            {
                await graphUpdater.UpdateAsync(item);
                foreach (IPart part in item.Parts)
                    await graphUpdater.UpdateAsync(item, part);
            }

            if (n % 10 == 0 || n == count)
                logger.LogInformation("Seeded {N}/{Count} items", n, count);
        }
    }

    private static void SeedSettings(ICadmusRepository repository,
        string profileContent, ILogger logger)
    {
        using JsonDocument doc = JsonDocument.Parse(profileContent);

        // settings are in the root/settings property, which is a single object
        // where each property is an object setting: the property name is the
        // setting key, and the property value is the setting value.
        // By convention each setting refers to an editor and its ID is the
        // editor's type ID optionally followed by its role ID prefixed by an
        // underscore. For instance, categories editor's settings are under
        // it.vedph.categories, and the role-specific settings are under
        // it.vedph.categories_role.
        if (!doc.RootElement.TryGetProperty("settings",
            out JsonElement settingsElem))
        {
            logger.LogInformation("No settings in profile");
            return;
        }

        int count = 0;
        foreach (JsonProperty settingProp in settingsElem.EnumerateObject())
        {
            string json = settingProp.Value.GetRawText();
            if (!string.IsNullOrEmpty(json))
            {
                logger.LogDebug("Seeding setting {Setting}", settingProp.Name);
                repository.AddSetting(settingProp.Name, json);
                count++;
            }
        }
        logger.LogInformation("Seeded {Count} settings", count);
    }

    private static async Task<string?> LoadProfileAsync(
        ResourceLoaderService loaderService, string profileSource,
        ILogger logger)
    {
        logger.LogInformation("Loading seed profile from {Source} ({Resolved})",
            profileSource, loaderService.ResolveSource(profileSource));

        await using Stream? stream = await loaderService.LoadAsync(profileSource);
        if (stream == null) return null;

        using StreamReader reader = new(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static async Task FillDataDatabaseAsync(
        IServiceProvider serviceProvider, IConfiguration config,
        ResourceLoaderService loaderService, string profileContent,
        ILogger logger)
    {
        IPartSeederFactoryProvider factoryProvider =
            serviceProvider.GetRequiredService<IPartSeederFactoryProvider>();
        PartSeederFactory factory = factoryProvider.GetFactory(profileContent);

        ICadmusRepository repository = serviceProvider
            .GetRequiredService<IRepositoryProvider>().CreateRepository();

        // settings
        SeedSettings(repository, profileContent, logger);

        // mock items
        int count = config.GetValue<int>("Seed:ItemCount");
        if (count > 0)
        {
            await SeedItemsAsync(serviceProvider, config, factory, repository,
                count, logger);
        }
        else
        {
            logger.LogInformation("Seed:ItemCount is 0: no mock items seeded");
        }

        // imports
        IList<string>? sources = factory.GetImports();
        if (sources?.Count > 0)
        {
            PartImporter importer = new(repository);
            foreach (string source in sources)
            {
                foreach (string resolved in SourceRangeResolver.Resolve(source))
                {
                    logger.LogInformation("Importing from {Source}...",
                        resolved);
                    await using Stream jsonStream =
                        await loaderService.LoadAsync(resolved)
                        ?? throw new InvalidOperationException(
                            $"Import source not found: {resolved}");
                    importer.Import(jsonStream);
                }
            }
        }
    }

    private static async Task SeedDataDatabaseCoreAsync(
        IServiceProvider serviceProvider, IConfiguration config,
        DatabaseTarget target, ILogger logger)
    {
        string connString = target.ConnectionString;

        MongoDatabaseManager manager = new();
        if (manager.DatabaseExists(connString))
        {
            logger.LogInformation("Data database {Database} already exists",
                target);

            // upgrade it with the indexes added by later versions
            try
            {
                manager.EnsureIndexes(connString);
            }
            catch (MongoException ex)
            {
                logger.LogWarning(ex, "Unable to update the indexes of " +
                    "data database {Database}: {Error}", target, ex.Message);
            }
            return;
        }

        // the database is created and seeded from the profile
        string? profileSource = config["Seed:ProfileSource"];
        if (string.IsNullOrEmpty(profileSource))
        {
            logger.LogWarning("Data database {Database} does not exist, " +
                "but no Seed:ProfileSource is configured to create it", target);
            return;
        }

        ResourceLoaderService loaderService = new(serviceProvider);
        string profileContent = await LoadProfileAsync(loaderService,
            profileSource, logger)
            ?? throw new InvalidOperationException(
                $"Seed profile not found at {profileSource}");

        DataProfile profile = new JsonDataProfileSerializer()
            .Read(profileContent);
        string? error = profile.Validate();
        if (error != null)
            logger.LogWarning("Seed profile validation error: {Error}", error);

        logger.LogInformation("Creating data database {Database}...", target);
        manager.CreateDatabase(connString, profile);

        // fill the database we just created: on failure drop it, so that
        // a retry or the next startup starts from scratch rather than
        // finding an existing but incomplete database and skipping it
        try
        {
            await FillDataDatabaseAsync(serviceProvider, config, loaderService,
                profileContent, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding data database {Database}: " +
                "removing it", target);
            try
            {
                manager.DeleteDatabase(connString);
            }
            catch (Exception dropEx)
            {
                logger.LogCritical(dropEx,
                    "Unable to remove incomplete data database {Database}: " +
                    "drop it manually before restarting", target);
            }
            throw;
        }

        logger.LogInformation("Data database {Database} created and seeded",
            target);
    }

    /// <summary>
    /// Ensures that the main data (MongoDB) database exists, creating and
    /// seeding it from the profile at <c>Seed:ProfileSource</c> if it does
    /// not, retrying on transient connection errors. As mock items are
    /// added to the index and graph when enabled, call this after
    /// <see cref="EnsureIndexDatabaseAsync"/> and
    /// <see cref="EnsureGraphDatabaseAsync"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <exception cref="ArgumentNullException">serviceProvider</exception>
    public static Task SeedDataDatabaseAsync(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        IConfiguration config =
            serviceProvider.GetRequiredService<IConfiguration>();
        ILogger logger = GetLogger(serviceProvider);

        DatabaseTarget target = CadmusDatabaseConfig.GetDataDatabase(config);
        logger.LogInformation("Ensuring data database {Database}...", target);

        return SeedRetryPolicy.Create(logger, "Data database").ExecuteAsync(
            () => SeedDataDatabaseCoreAsync(serviceProvider, config, target,
                logger));
    }
    #endregion

    /// <summary>
    /// Ensures that all the Cadmus databases required by the configuration
    /// exist, creating (and when required seeding) the missing ones: the
    /// item index database, the graph database, and the main data database.
    /// Call this before running the host, so that no request reaches a
    /// missing database. Any error is logged and rethrown, as the API cannot
    /// work without its databases.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="graphSql">The optional SQL code to seed the graph
    /// database with preset data when it gets created. This is usually
    /// provided by project-specific implementations.</param>
    /// <returns>The received host, to allow concatenation.</returns>
    /// <exception cref="ArgumentNullException">host</exception>
    public static async Task<IHost> SeedAsync(this IHost host,
        string? graphSql = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        using IServiceScope scope = host.Services.CreateScope();
        IServiceProvider serviceProvider = scope.ServiceProvider;
        IConfiguration config =
            serviceProvider.GetRequiredService<IConfiguration>();
        ILogger logger = GetLogger(serviceProvider);

        try
        {
            logger.LogInformation("Initializing Cadmus databases " +
                "(indexing: {IsIndexEnabled}, graph: {IsGraphEnabled})...",
                config.GetValue<bool>("Indexing:IsEnabled"),
                config.GetValue<bool>("Indexing:IsGraphEnabled"));

            // legacy optional delay, to allow DB services to start
            // (transient connection errors are retried anyway)
            int delay = config.GetValue<int>("Seed:IndexDelay");
            if (delay > 0)
            {
                logger.LogInformation("Waiting {Delay} seconds " +
                    "(Seed:IndexDelay)...", delay);
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }

            await EnsureIndexDatabaseAsync(serviceProvider);
            await EnsureGraphDatabaseAsync(serviceProvider, graphSql);
            await SeedDataDatabaseAsync(serviceProvider);

            logger.LogInformation("Cadmus databases initialized");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error initializing Cadmus databases: {Error}",
                ex.Message);
            throw;
        }
        return host;
    }
}

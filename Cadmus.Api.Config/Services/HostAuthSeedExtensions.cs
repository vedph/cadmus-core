using Cadmus.Api.Services.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Cadmus.Api.Config.Services;

/// <summary>
/// Database initializer extension to <see cref="IHost"/>.
/// See https://stackoverflow.com/questions/45148389/how-to-seed-in-entity-framework-core-2.
/// </summary>
public static class HostAuthSeedExtensions
{
    private static Task SeedAsync(IServiceProvider serviceProvider,
        ILogger logger)
    {
        AuthDatabaseInitializer initializer = new(serviceProvider);

        // retry on transient connection errors (e.g. DB not yet started)
        return SeedRetryPolicy.Create(logger, "Auth database")
            .ExecuteAsync(initializer.SeedAsync);
    }

    /// <summary>
    /// Seeds the database.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <returns>The received host, to allow concatenation.</returns>
    /// <exception cref="ArgumentNullException">serviceProvider</exception>
    public static async Task<IHost> SeedAuthAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        IServiceProvider serviceProvider = scope.ServiceProvider;
        ILogger logger = serviceProvider
            .GetRequiredService<ILoggerFactory>()!
            .CreateLogger(typeof(HostAuthSeedExtensions));

        try
        {
            IConfiguration config =
                serviceProvider.GetRequiredService<IConfiguration>();

            int delay = config.GetValue<int>("Seed:Delay");
            if (delay > 0)
            {
                logger.LogInformation("Waiting {Delay} seconds...", delay);
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }
            else
            {
                logger.LogInformation("No seed delay set.");
            }

            await SeedAsync(serviceProvider, logger);
            logger.LogInformation("Auth database initialized");
            return host;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error seeding auth database: {Error}",
                ex.Message);
            throw;
        }
    }
}

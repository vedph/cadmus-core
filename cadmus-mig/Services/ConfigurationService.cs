using Microsoft.Extensions.Configuration;

namespace Cadmus.Migration.Cli.Services;

internal static class ConfigurationService
{
    private static IConfiguration? _configuration;

    public static IConfiguration Configuration
    {
        get
        {
            _configuration ??= new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            return _configuration;
        }
    }
}

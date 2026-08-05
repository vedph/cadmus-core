using Cadmus.Cli.Core;
using Cadmus.Export.Config;
using Cadmus.Export.Json;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Extensions.Logging;
using System.IO;
using System.Reflection;

namespace Cadmus.Cli.Services;

internal static class CliAppContext
{
    private static IConfiguration? _configuration;
    private static Microsoft.Extensions.Logging.ILogger? _logger;

    public static IConfiguration Configuration
    {
        get
        {
            _configuration ??= new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .Build();
            return _configuration;
        }
    }

    public static Microsoft.Extensions.Logging.ILogger Logger
    {
        get
        {
            if (_logger is null)
            {
                // https://github.com/serilog/serilog-sinks-file
                string logFilePath = Path.Combine(
                    Path.GetDirectoryName(
                        Assembly.GetExecutingAssembly().Location) ?? "",
                        "cadmus-tool-log.txt");
                Log.Logger = new LoggerConfiguration()
#if DEBUG
                    .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
#endif
                    .Enrich.FromLogContext()
                    .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                _logger = new SerilogLoggerFactory(Log.Logger).CreateLogger("");
            }
            return _logger;
        }
    }

    /// <summary>
    /// Gets the preview factory provider with the specified plugin tag
    /// (assuming that the plugin has a (single) implementation of
    /// <see cref="ICadmusRenderingFactoryProvider"/>).
    /// </summary>
    /// <param name="pluginTag">The tag of the component in its plugin,
    /// or null to use the standard preview factory provider.</param>
    /// <returns>The provider.</returns>
    public static ICadmusRenderingFactoryProvider? GetPreviewFactoryProvider(
        string? pluginTag = null)
    {
        if (pluginTag == null)
            return new StandardRenderingFactoryProvider();

        return PluginFactoryProvider
            .GetFromTag<ICadmusRenderingFactoryProvider>(pluginTag);
    }

    /// <summary>
    /// Gets the JSON exporter factory provider with the specified plugin tag.
    /// </summary>
    /// <param name="pluginTag">The tag of the component in its plugin,
    /// or null to use the standard JSON exporter factory provider.</param>
    /// <returns>The provider.</returns>
    public static IJsonExporterFactoryProvider? GetJsonExporterFactoryProvider(
        string? pluginTag = null)
    {
        if (pluginTag == null)
            return new StandardJsonExporterFactoryProvider();

        return PluginFactoryProvider
            .GetFromTag<IJsonExporterFactoryProvider>(pluginTag);
    }
}
